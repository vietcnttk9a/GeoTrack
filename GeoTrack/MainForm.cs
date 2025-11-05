using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeoTrack.Models;

namespace GeoTrack;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;
    private const int MaxRawMessages = 100_000;
    private const int MaxTelemetryHistory = 20_000;

    private readonly BindingList<GeoMessageView> _visibleMessages = new();
    private readonly List<GeoMessageView> _allMessages = new();
    private readonly BindingList<BuggySnapshotView> _buggySnapshots = new();
    private readonly BindingList<TelemetrySendHistoryView> _telemetryHistory = new();
    private readonly Dictionary<string, DeviceStatusInfo> _deviceStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly BuggyRepository _buggyRepository = new();


    private int _pendingTelemetryEntriesCount;
    private string _lastExternalSendStatus = string.Empty;
    private DeviceConfigDto? _currentConfig;
    private AppBehaviorConfigDto _appBehavior = new();
    private CancellationTokenSource? _appCts;
    private HttpClient? _httpClient;
    private ExternalAppTokenManager? _tokenManager;
    private ExternalAppSender? _externalSender;
    private TcpServer? _tcpServer;
    private string? _selectedStationFilter;
    private bool _allowClose;

    public MainForm()
    {
        InitializeComponent();
        InitializeGrids();
        InitializeTrayMenuState();
        FormClosed += MainForm_FormClosed;
    }

    private void InitializeTrayMenuState()
    {
        toggleSendingMenuItem.Enabled = false;
        trayNotifyIcon.Visible = false;
    }

    private void InitializeGrids()
    {
        InitializeMessageGrid();
        InitializeSnapshotGrid(filteredGrid, _buggySnapshots, includeExternalSend: false);
        InitializeSnapshotGrid(telemetryGrid, _telemetryHistory, includeExternalSend: true);
    }

    private void InitializeMessageGrid()
    {
        messagesGrid.AutoGenerateColumns = false;
        messagesGrid.DataSource = _visibleMessages;
        messagesGrid.Columns.Clear();

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Time",
            DataPropertyName = nameof(GeoMessageView.Time),
            Width = 160
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Station",
            DataPropertyName = nameof(GeoMessageView.StationId),
            Width = 120
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Device",
            DataPropertyName = nameof(GeoMessageView.DeviceId),
            Width = 120
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Latitude",
            DataPropertyName = nameof(GeoMessageView.Latitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Longitude",
            DataPropertyName = nameof(GeoMessageView.Longitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Sats",
            DataPropertyName = nameof(GeoMessageView.Sats),
            Width = 90,
            DefaultCellStyle = { Format = "F0" }
        });
    }

    private static void InitializeSnapshotGrid(DataGridView grid, IBindingList dataSource, bool includeExternalSend)
    {
        grid.AutoGenerateColumns = false;
        grid.DataSource = dataSource;
        grid.Columns.Clear();

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Time",
            DataPropertyName = nameof(BuggySnapshotView.Time),
            Width = 160
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Station",
            DataPropertyName = nameof(BuggySnapshotView.Station),
            Width = 120
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Device",
            DataPropertyName = nameof(BuggySnapshotView.Device),
            Width = 120
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Latitude",
            DataPropertyName = nameof(BuggySnapshotView.Latitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Longitude",
            DataPropertyName = nameof(BuggySnapshotView.Longitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Sats",
            DataPropertyName = nameof(BuggySnapshotView.Sats),
            Width = 90,
            DefaultCellStyle = { Format = "F0" }
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Status",
            DataPropertyName = nameof(BuggySnapshotView.Status),
            Width = 90
        });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "IdleDuration (s)",
            DataPropertyName = nameof(BuggySnapshotView.IdleDurationSeconds),
            Width = 130
        });

        if (includeExternalSend)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "External Send",
                DataPropertyName = nameof(BuggySnapshotView.ExternalSendStatus),
                Width = 180
            });
        }
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        await ReloadConfigurationAsync();
    }

    private async Task ReloadConfigurationAsync()
    {
        reloadConfigButton.Enabled = false;

        await StopAllBackgroundOperationsAsync();
        ClearUiState();

        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "devices.config.json");
            var config = await ConfigLoader.LoadAsync(configPath).ConfigureAwait(true);
            _currentConfig = config;
            _appBehavior = config.AppBehavior ?? new AppBehaviorConfigDto();

            EnsureTrayIcon();

            PopulateDeviceFilter(config);

            if (_appCts == null)
            {
                _appCts = new CancellationTokenSource();
            }

            var listenIpString = config.Server?.ListenIp;
            if (string.IsNullOrWhiteSpace(listenIpString))
            {
                listenIpString = "0.0.0.0";
            }

            if (!IPAddress.TryParse(listenIpString, out var listenIp))
            {
                LogInfo("TcpServer", $"Invalid listen IP '{listenIpString}', fallback to 0.0.0.0");
                listenIp = IPAddress.Any;
            }

            var listenPort = config.Server?.ListenPort ?? 5099;
            if (listenPort <= 0)
            {
                LogInfo("TcpServer", $"Invalid listen port '{listenPort}', fallback to 5099");
                listenPort = 5099;
            }

            _tcpServer = new TcpServer(listenIp, listenPort);
            _tcpServer.LogGenerated += OnLogMessage;
            _tcpServer.MessageReceived += Worker_MessageReceived;
            _tcpServer.ClientConnected += Worker_StatusChanged;
            _tcpServer.ClientDisconnected += Worker_StatusChanged;

            try
            {
                _tcpServer.Start(_appCts.Token);
                LogInfo("TcpServer", $"Server started on {listenIp}:{listenPort}");
            }
            catch (Exception ex)
            {
                LogInfo("TcpServer", $"Cannot start server: {ex.Message}");
            }

            await InitializeExternalAppAsync(config).ConfigureAwait(true);

            if (_appBehavior.RunInBackgroundWhenHidden && _appBehavior.StartMinimized)
            {
                BeginInvoke(new Action(() => HideToTray("GeoTrack đang chạy nền.")));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            reloadConfigButton.Enabled = true;
        }
    }

    private async Task StopAllBackgroundOperationsAsync()
    {
        _buggyRepository.Clear();

        if (_appCts != null && !_appCts.IsCancellationRequested)
        {
            _appCts.Cancel();
        }

        if (_tcpServer != null)
        {
            try
            {
                await _tcpServer.StopAsync().ConfigureAwait(true);
            }
            catch
            {
                // Ignored
            }

            _tcpServer.LogGenerated -= OnLogMessage;
            _tcpServer.MessageReceived -= Worker_MessageReceived;
            _tcpServer.ClientConnected -= Worker_StatusChanged;
            _tcpServer.ClientDisconnected -= Worker_StatusChanged;
            _tcpServer.Dispose();
            _tcpServer = null;
        }

        var tasks = new List<Task>();
        if (_externalSender?.Completion != null)
        {
            tasks.Add(_externalSender.Completion);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(true);
        }
        catch
        {
            // Ignored
        }

        if (_externalSender != null)
        {
            _externalSender.LogGenerated -= OnLogMessage;
            _externalSender.StatusChanged -= ExternalStatusChanged;
            _externalSender = null;
        }

        if (_tokenManager != null)
        {
            _tokenManager.LogGenerated -= OnLogMessage;
            _tokenManager.StatusChanged -= ExternalStatusChanged;
            _tokenManager.Dispose();
            _tokenManager = null;
        }

        _httpClient?.Dispose();
        _httpClient = null;

        if (_appCts != null)
        {
            _appCts.Dispose();
            _appCts = null;
        }

        toggleSendingMenuItem.Enabled = false;
        UpdateTrayMenu();
    }

    private void PopulateDeviceFilter(DeviceConfigDto config)
    {
        deviceFilterComboBox.Items.Clear();
        deviceFilterComboBox.Items.Add("All stations");

        foreach (var device in config.Devices)
        {
            EnsureStationFilterContains(device.StationId);
        }

        if (deviceFilterComboBox.Items.Count > 0)
        {
            deviceFilterComboBox.SelectedIndex = 0;
        }
    }

    private async Task InitializeExternalAppAsync(DeviceConfigDto config)
    {
        if (config.ExternalApp == null)
        {
            UpdateExternalStatus("Disabled");
            return;
        }

        var externalConfig = config.ExternalApp;
        externalConfig.Endpoints ??= new ExternalAppEndpointsConfigDto();
        externalConfig.Http ??= new ExternalAppHttpConfigDto();

        _httpClient = HttpClientFactory.Create(externalConfig.Http);

        if (Uri.TryCreate(externalConfig.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }

        if (_appCts == null)
        {
            _appCts = new CancellationTokenSource();
        }

        _tokenManager = new ExternalAppTokenManager(_httpClient, externalConfig);
        _tokenManager.LogGenerated += OnLogMessage;
        _tokenManager.StatusChanged += ExternalStatusChanged;

        _externalSender = new ExternalAppSender(_httpClient, externalConfig, _tokenManager, CreateTelemetryPayload);
        _externalSender.LogGenerated += OnLogMessage;
        _externalSender.StatusChanged += ExternalStatusChanged;

        toggleSendingMenuItem.Enabled = true;
        UpdateTrayMenu();

        UpdateExternalStatus("Initializing...");

        try
        {
            await _tokenManager.EnsureAuthenticatedAsync(_appCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogInfo("ExternalApp", $"Initial login failed: {ex.Message}");
            UpdateExternalStatus("Authentication failed");
        }

        if (_appCts is { Token: var token })
        {
            try
            {
                _externalSender.Start(token);
            }
            catch (Exception ex)
            {
                LogInfo("ExternalApp", $"Không thể khởi động sender: {ex.Message}");
                UpdateExternalStatus("Sender error");
            }
        }
    }

    private AggregatePayloadDto CreateTelemetryPayload()
    {
        var snapshot = _buggyRepository.Snapshot();
        var timestamp = DateTimeOffset.UtcNow;
        var snapshotList = snapshot.ToList();

        // Đẩy việc update history về UI thread
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() =>
                {
                    RecordTelemetryHistory(snapshotList, timestamp);
                }));
            }
            catch
            {
                // Trong trường hợp form đã dispose khi app đang shutdown thì bỏ qua
            }
        }
        else
        {
            RecordTelemetryHistory(snapshotList, timestamp);
        }

        return new AggregatePayloadDto
        {
            Timestamp = timestamp,
            Metrics = new AggregateMetricsDto
            {
                TotalDevices = snapshotList.Count,
                ActiveDevices = snapshotList.Count
            },
            Buggies = snapshotList
        };
    }


    private void Worker_MessageReceived(object? sender, GeoMessageReceivedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, GeoMessageReceivedEventArgs>(Worker_MessageReceived), sender, e);
            return;
        }

        var messageView = GeoMessageView.FromMessage(e.Message, e.Device);
        AppendRawMessage(messageView);

        _buggyRepository.Update(e.Device.StationId, e.Message);
        RefreshBuggySnapshotList();
    }

    private void Worker_StatusChanged(object? sender, DeviceStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, DeviceStatusChangedEventArgs>(Worker_StatusChanged), sender, e);
            return;
        }

        _deviceStatuses[e.Device.StationId] = new DeviceStatusInfo(e.Status, e.Timestamp);
        UpdateDeviceStatus(e.Device.StationId);
        UpdateConnectionSummary();
    }

    private void UpdateDeviceStatus(string stationId)
    {
        EnsureStationFilterContains(stationId);

        if (!_deviceStatuses.TryGetValue(stationId, out var status))
        {
            return;
        }

        var existingItem = statusListView.Items.Cast<ListViewItem>().FirstOrDefault(item => string.Equals(item.Name, stationId, StringComparison.OrdinalIgnoreCase));
        if (existingItem == null)
        {
            existingItem = new ListViewItem(stationId)
            {
                Name = stationId
            };
            existingItem.SubItems.Add(status.Status);
            existingItem.SubItems.Add(status.Timestamp.ToLocalTime().ToString("HH:mm:ss"));
            statusListView.Items.Add(existingItem);
        }
        else
        {
            existingItem.SubItems[1].Text = status.Status;
            existingItem.SubItems[2].Text = status.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        }
    }

    private void UpdateConnectionSummary()
    {
        var connected = _deviceStatuses.Values.Count(status => string.Equals(status.Status, "Connected", StringComparison.OrdinalIgnoreCase));
        var total = _deviceStatuses.Count;
        connectionStatusLabel.Text = $"Connected: {connected} / {total} devices";
    }

    private void EnsureStationFilterContains(string? stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
        {
            return;
        }

        var exists = deviceFilterComboBox.Items
            .Cast<object>()
            .Skip(1)
            .Any(item => string.Equals(item?.ToString(), stationId, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            deviceFilterComboBox.Items.Add(stationId);
        }
    }

    private void DeviceFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (deviceFilterComboBox.SelectedIndex <= 0)
        {
            _selectedStationFilter = null;
        }
        else
        {
            _selectedStationFilter = deviceFilterComboBox.SelectedItem?.ToString();
        }

        ApplyMessageFilter();
        RefreshBuggySnapshotList();
    }

    private bool ShouldDisplay(string stationId)
    {
        return string.IsNullOrEmpty(_selectedStationFilter) || string.Equals(stationId, _selectedStationFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void AppendRawMessage(GeoMessageView view)
    {
        _allMessages.Insert(0, view);
        TrimAllMessages();

        if (ShouldDisplay(view.StationId))
        {
            _visibleMessages.Insert(0, view);
            TrimVisibleMessages();
        }
    }

    private void TrimAllMessages()
    {
        if (_allMessages.Count <= MaxRawMessages)
        {
            return;
        }

        var overflow = _allMessages.Count - MaxRawMessages;
        if (overflow <= 0)
        {
            return;
        }

        var startIndex = Math.Max(0, _allMessages.Count - overflow);
        var removed = _allMessages.GetRange(startIndex, overflow);
        _allMessages.RemoveRange(startIndex, overflow);

        if (removed.Count == 0)
        {
            return;
        }

        _visibleMessages.RaiseListChangedEvents = false;
        foreach (var item in removed)
        {
            var index = _visibleMessages.IndexOf(item);
            if (index >= 0)
            {
                _visibleMessages.RemoveAt(index);
            }
        }

        _visibleMessages.RaiseListChangedEvents = true;
        _visibleMessages.ResetBindings();
    }


    private void TrimVisibleMessages()
    {
        if (_visibleMessages.Count <= MaxRawMessages)
        {
            return;
        }

        _visibleMessages.RaiseListChangedEvents = false;
        while (_visibleMessages.Count > MaxRawMessages)
        {
            // xoá bản ghi cũ nhất ở cuối
            _visibleMessages.RemoveAt(_visibleMessages.Count - 1);
        }

        _visibleMessages.RaiseListChangedEvents = true;
        _visibleMessages.ResetBindings();
    }


    private void RecordTelemetryHistory(IReadOnlyList<BuggyDto> snapshot, DateTimeOffset timestamp)
    {
        if (snapshot.Count == 0)
        {
            _pendingTelemetryEntriesCount = 0;
            return;
        }

        var timeText = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        // batch mới nhất sẽ nằm ở đầu list
        foreach (var buggy in snapshot)
        {
            var entry = TelemetrySendHistoryView.FromBuggyDto(buggy, timeText);
            _telemetryHistory.Insert(0, entry);
        }

        _pendingTelemetryEntriesCount = snapshot.Count;
        TrimTelemetryHistory();
    }



    private void UpdatePendingTelemetryHistory(string statusText)
    {
        if (_pendingTelemetryEntriesCount <= 0)
        {
            return;
        }

        var maxIndex = Math.Min(_pendingTelemetryEntriesCount, _telemetryHistory.Count);
        for (var i = 0; i < maxIndex; i++)
        {
            _telemetryHistory[i].ExternalSendStatus = statusText;
            _telemetryHistory.ResetItem(i);
        }

        _pendingTelemetryEntriesCount = 0;
    }


    private void TrimTelemetryHistory()
    {
        while (_telemetryHistory.Count > MaxTelemetryHistory)
        {
            // xoá bản ghi cũ nhất (đang nằm ở cuối)
            _telemetryHistory.RemoveAt(_telemetryHistory.Count - 1);
        }
    }


    private void ApplyMessageFilter()
    {
        _visibleMessages.RaiseListChangedEvents = false;
        _visibleMessages.Clear();

        foreach (var message in _allMessages)
        {
            if (ShouldDisplay(message.StationId))
            {
                _visibleMessages.Add(message);
            }
        }

        TrimVisibleMessages();

        _visibleMessages.RaiseListChangedEvents = true;
        _visibleMessages.ResetBindings();
    }

    private void ExternalStatusChanged(object? sender, ExternalAppStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, ExternalAppStatusChangedEventArgs>(ExternalStatusChanged), sender, e);
            return;
        }

        var statusText = $"{e.Status} ({e.Timestamp.ToLocalTime():HH:mm:ss})";
        UpdateExternalStatus(statusText);
        _lastExternalSendStatus = statusText;

        if (_pendingTelemetryEntriesCount > 0 && e.Status.StartsWith("Telemetry", StringComparison.OrdinalIgnoreCase))
        {
            UpdatePendingTelemetryHistory(statusText);
        }

        RefreshBuggySnapshotList();
        UpdateTrayMenu();
    }

    private void UpdateExternalStatus(string status)
    {
        externalStatusLabel.Text = $"External app: {status}";
    }

    private void OnLogMessage(object? sender, LogMessageEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, LogMessageEventArgs>(OnLogMessage), sender, e);
            return;
        }

        AppendLog(e);
    }

    private void AppendLog(LogMessageEventArgs e)
    {
        var line = $"[{e.Timestamp.ToLocalTime():HH:mm:ss}] [{e.Source}] {e.Message}";

        if (logTextBox.TextLength > 0)
        {
            logTextBox.AppendText(Environment.NewLine);
        }

        logTextBox.AppendText(line);

        var lines = logTextBox.Lines;
        if (lines.Length > MaxLogLines)
        {
            logTextBox.Lines = lines.Skip(lines.Length - MaxLogLines).ToArray();
        }

        logTextBox.SelectionStart = logTextBox.TextLength;
        logTextBox.SelectionLength = 0;
        logTextBox.ScrollToCaret();
    }

    private void LogInfo(string source, string message)
    {
        OnLogMessage(this, new LogMessageEventArgs(source, message, DateTime.UtcNow));
    }

    private void ReloadConfigButton_Click(object? sender, EventArgs e)
    {
        _ = ReloadConfigurationAsync();
    }

    private void ClearUiState()
    {
        _allMessages.Clear();
        _visibleMessages.Clear();
        _buggySnapshots.Clear();
        _telemetryHistory.Clear();
        _deviceStatuses.Clear();
        statusListView.Items.Clear();
        connectionStatusLabel.Text = "Connected: 0 / 0 devices";
        UpdateExternalStatus("Disabled");
        _buggyRepository.Clear();
        _lastExternalSendStatus = string.Empty;
        _pendingTelemetryEntriesCount = 0;
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _appBehavior.RunInBackgroundWhenHidden)
        {
            HideToTray("GeoTrack đang chạy nền.");
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing && _appBehavior.RunInBackgroundWhenHidden)
        {
            e.Cancel = true;
            HideToTray("GeoTrack tiếp tục chạy nền.");
        }
    }

    private async void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        trayNotifyIcon.Visible = false;
        trayNotifyIcon.Dispose();
        await StopAllBackgroundOperationsAsync();
    }

    private void OpenMenuItem_Click(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void ToggleSendingMenuItem_Click(object? sender, EventArgs e)
    {
        if (_externalSender == null)
        {
            return;
        }

        if (_externalSender.IsPaused)
        {
            _externalSender.Resume();
        }
        else
        {
            _externalSender.Pause();
        }

        UpdateTrayMenu();
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        _allowClose = true;
        trayNotifyIcon.Visible = false;
        Close();
    }

    private void TrayNotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void UpdateTrayMenu()
    {
        if (_externalSender == null)
        {
            toggleSendingMenuItem.Text = "Pause Sending";
            toggleSendingMenuItem.Enabled = false;
        }
        else
        {
            toggleSendingMenuItem.Enabled = true;
            toggleSendingMenuItem.Text = _externalSender.IsPaused ? "Resume Sending" : "Pause Sending";
        }
    }

    private void EnsureTrayIcon()
    {
        trayNotifyIcon.Icon ??= Icon ?? SystemIcons.Application;
        trayNotifyIcon.Visible = _appBehavior.RunInBackgroundWhenHidden && _appBehavior.Tray.ShowTrayIcon && !Visible;
    }

    private void HideToTray(string? message)
    {
        if (!_appBehavior.RunInBackgroundWhenHidden)
        {
            return;
        }

        EnsureTrayIcon();
        if (_appBehavior.Tray.ShowTrayIcon)
        {
            trayNotifyIcon.Visible = true;
            if (_appBehavior.Tray.BalloonOnBackground && !string.IsNullOrWhiteSpace(message))
            {
                trayNotifyIcon.BalloonTipTitle = Text;
                trayNotifyIcon.BalloonTipText = message;
                trayNotifyIcon.ShowBalloonTip(3000);
            }
        }

        Hide();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();

        if (!_appBehavior.Tray.ShowTrayIcon)
        {
            trayNotifyIcon.Visible = false;
        }
    }

    private sealed record DeviceStatusInfo(string Status, DateTime Timestamp);

    private System.Windows.Forms.Timer? _snapshotRefreshTimer;
    private bool _snapshotRefreshPending;

    private void EnsureSnapshotRefreshTimer()
    {
        if (_snapshotRefreshTimer != null) return;

        _snapshotRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 200 // ms, tối đa 5 lần/giây
        };
        _snapshotRefreshTimer.Tick += (_, _) =>
        {
            if (!_snapshotRefreshPending) return;
            _snapshotRefreshPending = false;
            RefreshBuggySnapshotListInternal();
        };
        _snapshotRefreshTimer.Start();
    }

    private void RefreshBuggySnapshotList()
    {
        EnsureSnapshotRefreshTimer();
        _snapshotRefreshPending = true;
    }

    private void RefreshBuggySnapshotListInternal()
    {
        var snapshot = _buggyRepository.Snapshot();
        var filtered = snapshot
            .Where(b => ShouldDisplay(b.StationId))
            .OrderByDescending(b => b.Timestamp)
            .Select(b => BuggySnapshotView.FromBuggyDto(b, _lastExternalSendStatus))
            .ToList();

        _buggySnapshots.RaiseListChangedEvents = false;
        _buggySnapshots.Clear();

        foreach (var item in filtered)
        {
            _buggySnapshots.Add(item);
        }

        _buggySnapshots.RaiseListChangedEvents = true;
        _buggySnapshots.ResetBindings();
    }

    

    private sealed class GeoMessageView
    {
        public required string Time { get; init; }
        public required string StationId { get; init; }
        public required string DeviceId { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Sats { get; init; }

        public static GeoMessageView FromMessage(GeoMessageDto message, DeviceEntryDto device)
        {
            return new GeoMessageView
            {
                Time = message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                StationId = device.StationId,
                DeviceId = message.DeviceId,
                Latitude = message.Latitude,
                Longitude = message.Longitude,
                Sats = message.Sats
            };
        }
    }

    private sealed class BuggySnapshotView
    {
        public required string Time { get; init; }
        public required string Station { get; init; }
        public required string Device { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Sats { get; init; }
        public string Status { get; init; } = string.Empty;
        public int IdleDurationSeconds { get; init; }
        public string ExternalSendStatus { get; init; } = string.Empty;

        public static BuggySnapshotView FromBuggyDto(BuggyDto buggy, string externalSendStatus)
        {
            return new BuggySnapshotView
            {
                Time = buggy.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Station = buggy.StationId,
                Device = buggy.DeviceId,
                Latitude = buggy.Latitude,
                Longitude = buggy.Longitude,
                Sats = buggy.Sats,
                Status = buggy.Status,
                IdleDurationSeconds = buggy.IdleDurationSeconds,
                ExternalSendStatus = externalSendStatus
            };
        }
    }

    private sealed class TelemetrySendHistoryView
    {
        public required string Time { get; init; }
        public required string Station { get; init; }
        public required string Device { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Sats { get; init; }
        public string Status { get; init; } = string.Empty;
        public int IdleDurationSeconds { get; init; }
        public string ExternalSendStatus { get; set; } = string.Empty;

        public static TelemetrySendHistoryView FromBuggyDto(BuggyDto buggy, string timeText)
        {
            return new TelemetrySendHistoryView
            {
                Time = timeText,
                Station = buggy.StationId,
                Device = buggy.DeviceId,
                Latitude = buggy.Latitude,
                Longitude = buggy.Longitude,
                Sats = buggy.Sats,
                Status = buggy.Status,
                IdleDurationSeconds = buggy.IdleDurationSeconds,
                ExternalSendStatus = "Pending"
            };
        }
    }
}
