using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTrack;

public sealed class TcpClientWorker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public TcpClientWorker(DeviceEntry device)
    {
        Device = device;
    }

    public DeviceEntry Device { get; }

    public event EventHandler<GeoMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<DeviceStatusChangedEventArgs>? StatusChanged;

    public Task? Completion => _runningTask;

    public void Start()
    {
        if (_runningTask != null && !_runningTask.IsCompleted)
        {
            throw new InvalidOperationException("Worker is already running.");
        }

        _cts = new CancellationTokenSource();
        _runningTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_cts == null)
        {
            return;
        }

        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdateStatus("Connecting");

            using var client = new TcpClient();
            await client.ConnectAsync(Device.Host, Device.Port, cancellationToken);
            UpdateStatus("Connected");

            await using var networkStream = client.GetStream();
            using var reader = new StreamReader(networkStream, Encoding.UTF8);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    UpdateStatus("Disconnected");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var messages = JsonSerializer.Deserialize<List<GeoMessage>>(line, SerializerOptions);
                    if (messages == null)
                    {
                        continue;
                    }

                    foreach (var message in messages)
                    {
                        if (string.IsNullOrWhiteSpace(message.DeviceId))
                        {
                            message.DeviceId = Device.DeviceId;
                        }

                        OnMessageReceived(message);
                    }
                }
                catch (JsonException jsonException)
                {
                    Console.WriteLine($"[{Device.DeviceId}] Invalid JSON payload: {jsonException.Message}");
                    UpdateStatus("Connected");
                }
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Stopped");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private void UpdateStatus(string status)
    {
        StatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs(Device, status, DateTime.UtcNow));
    }

    private void OnMessageReceived(GeoMessage message)
    {
        MessageReceived?.Invoke(this, new GeoMessageReceivedEventArgs(message, Device));
    }
}

public sealed class GeoMessageReceivedEventArgs : EventArgs
{
    public GeoMessageReceivedEventArgs(GeoMessage message, DeviceEntry device)
    {
        Message = message;
        Device = device;
    }

    public GeoMessage Message { get; }
    public DeviceEntry Device { get; }
}

public sealed class DeviceStatusChangedEventArgs : EventArgs
{
    public DeviceStatusChangedEventArgs(DeviceEntry device, string status, DateTime timestamp)
    {
        Device = device;
        Status = status;
        Timestamp = timestamp;
    }

    public DeviceEntry Device { get; }
    public string Status { get; }
    public DateTime Timestamp { get; }
}
