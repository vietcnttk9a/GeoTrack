using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GeoTrack;

public partial class MainForm : Form
{
    private readonly BindingList<GeoMessageView> _visibleMessages = new();
    private readonly List<GeoMessageView> _allMessages = new();
    private readonly Dictionary<string, DeviceStatusInfo> _deviceStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TcpClientWorker> _workers = new();
    private string? _selectedDeviceFilter;

    public MainForm()
    {
        InitializeComponent();
        InitializeGrids();
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

        await StopWorkersAsync();
        ClearUiState();

        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "devices.config.json");
            var config = await ConfigLoader.LoadAsync(configPath);

            PopulateDeviceFilter(config);

            foreach (var device in config.Devices)
            {
                RegisterDevice(device);
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

    private void RegisterDevice(DeviceEntry device)
    {
        _deviceStatuses[device.DeviceId] = new DeviceStatusInfo("Pending", DateTime.UtcNow);
        UpdateDeviceStatus(device.DeviceId);
        UpdateConnectionSummary();

        var worker = new TcpClientWorker(device);
        worker.MessageReceived += Worker_MessageReceived;
        worker.StatusChanged += Worker_StatusChanged;
        _workers.Add(worker);

        worker.Start();
    }

    private async Task StopWorkersAsync()
    {
        foreach (var worker in _workers)
        {
            worker.MessageReceived -= Worker_MessageReceived;
            worker.StatusChanged -= Worker_StatusChanged;
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
