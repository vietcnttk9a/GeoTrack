using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GeoTrack;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;

    private readonly BindingList<GeoMessageView> _visibleMessages = new();
    private readonly List<GeoMessageView> _allMessages = new();
    private readonly Dictionary<string, DeviceStatusInfo> _deviceStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TcpClientWorker> _workers = new();
    private readonly Dictionary<string, GeoMessage> _latestMessages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient = new();
    private ExternalAppTokenManager? _tokenManager;
    private ExternalAppSender? _externalSender;
    private string? _selectedDeviceFilter;

    public MainForm()
    {
        InitializeComponent();
        InitializeGrids();
        FormClosed += MainForm_FormClosed;
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

    private async void MainForm_Load(object sender, EventArgs e)
    {
        await ReloadConfigurationAsync();
    }

    private async Task ReloadConfigurationAsync()
    {
        reloadConfigButton.Enabled = false;

        await StopExternalIntegrationAsync();
        await StopWorkersAsync();
        ClearUiState();

        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "devices.config.json");
            var config = await ConfigLoader.LoadAsync(configPath);

            PopulateDeviceFilter(config);

            foreach (var device in config.Devices)
            {
                var reconnectInterval = GetReconnectInterval(config, device);
                RegisterDevice(device, reconnectInterval);
            }

            await InitializeExternalAppAsync(config);
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

    private void RegisterDevice(DeviceEntry device, TimeSpan reconnectInterval)
    {
        _deviceStatuses[device.DeviceId] = new DeviceStatusInfo("Pending", DateTime.UtcNow);
        UpdateDeviceStatus(device.DeviceId);
        UpdateConnectionSummary();

        var worker = new TcpClientWorker(device, reconnectInterval);
        worker.MessageReceived += Worker_MessageReceived;
        worker.StatusChanged += Worker_StatusChanged;
        worker.LogGenerated += OnLogMessage;
        _workers.Add(worker);

        worker.Start();
    }

    private async Task StopWorkersAsync()
    {
        foreach (var worker in _workers)
        {
            worker.MessageReceived -= Worker_MessageReceived;
            worker.StatusChanged -= Worker_StatusChanged;
            worker.LogGenerated -= OnLogMessage;
            worker.Stop();
        }

        var completions = _workers.Select(worker => worker.Completion ?? Task.CompletedTask).ToArray();
        _workers.Clear();

        try
        {
            await Task.WhenAll(completions);
        }
        catch
        {
            // Ignored - stopping workers may throw due to cancellation.
        }
    }

    private void ClearUiState()
    {
        _allMessages.Clear();
        _visibleMessages.Clear();
        _deviceStatuses.Clear();
        statusListView.Items.Clear();
        connectionStatusLabel.Text = "Connected: 0 / 0 devices";
        UpdateExternalStatus("Disabled");
        lock (_latestMessages)
        {
            _latestMessages.Clear();
        }
    }

    private void PopulateDeviceFilter(DeviceConfig config)
    {
        deviceFilterComboBox.Items.Clear();
        deviceFilterComboBox.Items.Add("All devices");

        foreach (var device in config.Devices)
        {
            deviceFilterComboBox.Items.Add(device.DeviceId);
        }

        if (deviceFilterComboBox.Items.Count > 0)
        {
            deviceFilterComboBox.SelectedIndex = 0;
        }
    }

    private void Worker_StatusChanged(object? sender, DeviceStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, DeviceStatusChangedEventArgs>(Worker_StatusChanged), sender, e);
            return;
        }

        _deviceStatuses[e.Device.DeviceId] = new DeviceStatusInfo(e.Status, e.Timestamp);
        UpdateDeviceStatus(e.Device.DeviceId);
        UpdateConnectionSummary();
    }

    private void Worker_MessageReceived(object? sender, GeoMessageReceivedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, GeoMessageReceivedEventArgs>(Worker_MessageReceived), sender, e);
            return;
        }

        var view = GeoMessageView.FromMessage(e.Message);
        _allMessages.Add(view);

        lock (_latestMessages)
        {
            _latestMessages[e.Message.DeviceId] = e.Message;
        }

        if (ShouldDisplay(view.DeviceId))
        {
            _visibleMessages.Add(view);
        }
    }

    private bool ShouldDisplay(string deviceId)
    {
        return string.IsNullOrEmpty(_selectedDeviceFilter) || string.Equals(deviceId, _selectedDeviceFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateConnectionSummary()
    {
        var connected = _deviceStatuses.Values.Count(status => string.Equals(status.Status, "Connected", StringComparison.OrdinalIgnoreCase));
        var total = _deviceStatuses.Count;
        connectionStatusLabel.Text = $"Connected: {connected} / {total} devices";
    }

    private void UpdateDeviceStatus(string deviceId)
    {
        if (!_deviceStatuses.TryGetValue(deviceId, out var status))
        {
            return;
        }

        var existingItem = statusListView.Items.Cast<ListViewItem>().FirstOrDefault(item => string.Equals(item.Name, deviceId, StringComparison.OrdinalIgnoreCase));
        if (existingItem == null)
        {
            existingItem = new ListViewItem(deviceId)
            {
                Name = deviceId
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

    private async void ReloadConfigButton_Click(object? sender, EventArgs e)
    {
        await ReloadConfigurationAsync();
    }

    private void DeviceFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (deviceFilterComboBox.SelectedIndex <= 0)
        {
            _selectedDeviceFilter = null;
        }
        else
        {
            _selectedDeviceFilter = deviceFilterComboBox.SelectedItem?.ToString();
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _visibleMessages.RaiseListChangedEvents = false;
        _visibleMessages.Clear();

        foreach (var message in _allMessages)
        {
            if (ShouldDisplay(message.DeviceId))
            {
                _visibleMessages.Add(message);
            }
        }

        _visibleMessages.RaiseListChangedEvents = true;
        _visibleMessages.ResetBindings();
    }

    private async Task StopExternalIntegrationAsync()
    {
        if (_externalSender != null)
        {
            _externalSender.LogGenerated -= OnLogMessage;
            _externalSender.StatusChanged -= ExternalStatusChanged;
            await _externalSender.StopAsync();
            _externalSender = null;
        }

        if (_tokenManager != null)
        {
            _tokenManager.LogGenerated -= OnLogMessage;
            _tokenManager.StatusChanged -= ExternalStatusChanged;
            _tokenManager.Dispose();
            _tokenManager = null;
        }

        UpdateExternalStatus("Disabled");
    }

    private async Task InitializeExternalAppAsync(DeviceConfig config)
    {
        if (config.ExternalApp == null)
        {
            UpdateExternalStatus("Disabled");
            return;
        }

        if (!Uri.TryCreate(config.ExternalApp.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"BaseUrl không hợp lệ: {config.ExternalApp.BaseUrl}");
        }

        if (_httpClient.BaseAddress == null || _httpClient.BaseAddress != baseUri)
        {
            _httpClient.BaseAddress = baseUri;
        }

        _tokenManager = new ExternalAppTokenManager(_httpClient, config.ExternalApp);
        _tokenManager.LogGenerated += OnLogMessage;
        _tokenManager.StatusChanged += ExternalStatusChanged;

        _externalSender = new ExternalAppSender(_httpClient, config.ExternalApp, _tokenManager, CreateTelemetryPayload);
        _externalSender.LogGenerated += OnLogMessage;
        _externalSender.StatusChanged += ExternalStatusChanged;

        UpdateExternalStatus("Initializing...");

        try
        {
            await _tokenManager.EnsureAuthenticatedAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogInfo("ExternalApp", $"Initial login failed: {ex.Message}");
            UpdateExternalStatus("Authentication failed");
        }

        try
        {
            _externalSender.Start();
            LogInfo("ExternalApp", "Telemetry sender started.");
        }
        catch (Exception ex)
        {
            LogInfo("ExternalApp", $"Không thể khởi động sender: {ex.Message}");
            UpdateExternalStatus("Sender error");
        }
    }

    private object CreateTelemetryPayload()
    {
        GeoMessage[] snapshot;

        lock (_latestMessages)
        {
            snapshot = _latestMessages.Values.Select(message => message).ToArray();
        }

        var devices = snapshot.Select(message => new
        {
            message.DeviceId,
            message.Latitude,
            message.Longitude,
            message.SpeedKph,
            message.HeadingDeg,
            message.BatteryPct,
            message.Status,
            Timestamp = message.Timestamp
        }).ToList();

        return new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Devices = devices
        };
    }

    private static TimeSpan GetReconnectInterval(DeviceConfig config, DeviceEntry device)
    {
        var seconds = device.ReconnectIntervalSeconds ?? config.ReconnectIntervalSeconds ?? 10;
        if (seconds <= 0)
        {
            seconds = 10;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private void ExternalStatusChanged(object? sender, ExternalAppStatusChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<object?, ExternalAppStatusChangedEventArgs>(ExternalStatusChanged), sender, e);
            return;
        }

        UpdateExternalStatus($"{e.Status} ({e.Timestamp.ToLocalTime():HH:mm:ss})");
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

    private async void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        await StopExternalIntegrationAsync();
        await StopWorkersAsync();
        _httpClient.Dispose();
    }

    private sealed record DeviceStatusInfo(string Status, DateTime Timestamp);

    private sealed record GeoMessageView
    {
        public required string DeviceId { get; init; }
        public required string Status { get; init; }
        public required string Time { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double SpeedKph { get; init; }
        public double HeadingDeg { get; init; }
        public double BatteryPct { get; init; }

        public static GeoMessageView FromMessage(GeoMessage message)
        {
            return new GeoMessageView
            {
                DeviceId = message.DeviceId,
                Status = message.Status,
                Latitude = message.Latitude,
                Longitude = message.Longitude,
                SpeedKph = message.SpeedKph,
                HeadingDeg = message.HeadingDeg,
                BatteryPct = message.BatteryPct,
                Time = message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }
}
