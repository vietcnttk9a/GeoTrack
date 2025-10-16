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
    private readonly TimeSpan _reconnectDelay;

    public TcpClientWorker(DeviceEntry device, TimeSpan reconnectDelay)
    {
        Device = device;
        _reconnectDelay = reconnectDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : reconnectDelay;
    }

    public DeviceEntry Device { get; }

    public event EventHandler<GeoMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<DeviceStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;

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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UpdateStatus("Connecting");
                Log($"Connecting to {Device.Host}:{Device.Port}");

                using var client = new TcpClient();
                await client.ConnectAsync(Device.Host, Device.Port, cancellationToken);
                UpdateStatus("Connected");
                Log("Connection established.");

                await using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                    {
                        UpdateStatus("Disconnected");
                        Log("Connection closed by remote host.");
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
                        Log($"Invalid JSON payload: {jsonException.Message}");
                        UpdateStatus("Connected");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Stopped");
                Log("Worker stopped.");
                break;
            }
            catch (Exception ex)
            {
                UpdateStatus("Disconnected");
                Log($"Connection error: {ex.Message}");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                UpdateStatus("Reconnecting...");
                Log($"Reconnecting in {(int)_reconnectDelay.TotalSeconds} seconds...");

                try
                {
                    await Task.Delay(_reconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    UpdateStatus("Stopped");
                    Log("Reconnect cancelled.");
                    break;
                }
            }
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

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, new LogMessageEventArgs(Device.DeviceId, message, DateTime.UtcNow));
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
