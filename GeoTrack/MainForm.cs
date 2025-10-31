using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeoTrack.Models;

namespace GeoTrack;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;

    private readonly BindingList<GeoMessageView> _visibleMessages = new();
    private readonly List<GeoMessageView> _allMessages = new();
    private readonly Dictionary<string, DeviceStatusInfo> _deviceStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TcpClientWorker> _workers = new();
    private readonly BuggyRepository _buggyRepository = new();

    private DeviceConfigDto? _currentConfig;
    private AppBehaviorConfigDto _appBehavior = new();
    private CancellationTokenSource? _appCts;
    private HttpClient? _httpClient;
    private ExternalAppTokenManager? _tokenManager;
    private ExternalAppSender? _externalSender;
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
        messagesGrid.AutoGenerateColumns = false;
        messagesGrid.DataSource = _visibleMessages;

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
            HeaderText = "Lat",
            DataPropertyName = nameof(GeoMessageView.Latitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });
       
        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Lng",
            DataPropertyName = nameof(GeoMessageView.Longitude),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });
        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Sats",
            DataPropertyName = nameof(GeoMessageView.Sats),
            Width = 110,
            DefaultCellStyle = { Format = "F5" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Speed (kph)",
            DataPropertyName = nameof(GeoMessageView.SpeedKph),
            Width = 110,
            DefaultCellStyle = { Format = "F1" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Heading",
            DataPropertyName = nameof(GeoMessageView.HeadingDeg),
            Width = 90,
            DefaultCellStyle = { Format = "F0" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Battery (%)",
            DataPropertyName = nameof(GeoMessageView.BatteryPct),
            Width = 110,
            DefaultCellStyle = { Format = "F0" }
        });

        messagesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Status",
            DataPropertyName = nameof(GeoMessageView.Status),
            Width = 110
        });
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

            if (config.Devices.Count > 0)
            {
                _appCts = new CancellationTokenSource();

                foreach (var device in config.Devices)
                {
                    RegisterDevice(device);
                }
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

        var tasks = new List<Task>();
        tasks.AddRange(_workers.Select(worker => worker.Completion ?? Task.CompletedTask));
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

        foreach (var worker in _workers)
        {
            worker.MessageReceived -= Worker_MessageReceived;
            worker.StatusChanged -= Worker_StatusChanged;
            worker.LogGenerated -= OnLogMessage;
        }
        _workers.Clear();

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

    private void RegisterDevice(DeviceEntryDto device)
    {
        if (_appCts == null)
        {
            return;
        }

        _deviceStatuses[device.StationId] = new DeviceStatusInfo("Pending", DateTime.UtcNow);
        UpdateDeviceStatus(device.StationId);
        UpdateConnectionSummary();

        var worker = new TcpClientWorker(device, CreateReconnectSettings(device));
        worker.MessageReceived += Worker_MessageReceived;
        worker.StatusChanged += Worker_StatusChanged;
        worker.LogGenerated += OnLogMessage;
        _workers.Add(worker);
        worker.Start(_appCts.Token);
    }

    private ReconnectSettings CreateReconnectSettings(DeviceEntryDto device)
    {
        var deviceReconnect = device.Reconnect;
        var globalReconnect = _currentConfig?.Reconnect;

        int initialSeconds = deviceReconnect?.InitialDelaySeconds
            ?? globalReconnect?.InitialDelaySeconds
            ?? 10;
        if (initialSeconds <= 0)
        {
            initialSeconds = 10;
        }

        int maxSeconds = deviceReconnect?.MaxDelaySeconds
            ?? globalReconnect?.MaxDelaySeconds
            ?? 60;
        if (maxSeconds < initialSeconds)
        {
            maxSeconds = initialSeconds;
        }

        var useExponential = deviceReconnect?.UseExponentialBackoff
            ?? globalReconnect?.UseExponentialBackoff
            ?? true;

        return new ReconnectSettings(
            TimeSpan.FromSeconds(initialSeconds),
            TimeSpan.FromSeconds(maxSeconds),
            useExponential);
    }

    private void PopulateDeviceFilter(DeviceConfigDto config)
    {
        deviceFilterComboBox.Items.Clear();
        deviceFilterComboBox.Items.Add("All stations");

        foreach (var device in config.Devices)
        {
            deviceFilterComboBox.Items.Add(device.StationId);
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
        return new AggregatePayloadDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Metrics = new AggregateMetricsDto
            {
                TotalDevices = snapshot.Count,
                ActiveDevices = snapshot.Count
            },
            Buggies = snapshot.ToList()
        };
    }

    private void Worker_MessageReceived(object? sender, GeoMessageReceivedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, GeoMessageReceivedEventArgs>(Worker_MessageReceived), sender, e);
            return;
        }

        _buggyRepository.Update(e.Device.StationId, e.Message);

        var view = GeoMessageView.FromMessage(e.Message, e.Device.StationId);
        _allMessages.Add(view);

        if (ShouldDisplay(view.StationId))
        {
            _visibleMessages.Add(view);
        }
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

        ApplyFilter();
    }

    private void ApplyFilter()
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

        _visibleMessages.RaiseListChangedEvents = true;
        _visibleMessages.ResetBindings();
    }

    private bool ShouldDisplay(string stationId)
    {
        return string.IsNullOrEmpty(_selectedStationFilter) || string.Equals(stationId, _selectedStationFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void ExternalStatusChanged(object? sender, ExternalAppStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, ExternalAppStatusChangedEventArgs>(ExternalStatusChanged), sender, e);
            return;
        }

        UpdateExternalStatus($"{e.Status} ({e.Timestamp.ToLocalTime():HH:mm:ss})");
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
        _deviceStatuses.Clear();
        statusListView.Items.Clear();
        connectionStatusLabel.Text = "Connected: 0 / 0 devices";
        UpdateExternalStatus("Disabled");
        _buggyRepository.Clear();
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

    private sealed record GeoMessageView
    {
        public required string StationId { get; init; }
        public required string DeviceId { get; init; }
        public required string Status { get; init; }
        public required string Time { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Sats { get; set; }
        public double SpeedKph { get; init; }
        public double HeadingDeg { get; init; }
        public double BatteryPct { get; init; }

        public static GeoMessageView FromMessage(GeoMessageDto message, string stationId)
        {
            return new GeoMessageView
            {
                StationId = stationId,
                DeviceId = message.DeviceId,
                Status = message.Status,
                Latitude = message.Latitude,
                Longitude = message.Longitude,
                Sats = message.Sats,
                SpeedKph = message.SpeedKph,
                HeadingDeg = message.HeadingDeg,
                BatteryPct = message.BatteryPct,
                Time = message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }
}
