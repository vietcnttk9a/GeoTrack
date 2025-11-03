using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class TcpClientWorker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ReconnectSettings _reconnectSettings;
    private Task? _runningTask;

    public TcpClientWorker(DeviceEntryDto device, ReconnectSettings reconnectSettings)
    {
        Device = device;
        _reconnectSettings = reconnectSettings;
    }

    public DeviceEntryDto Device { get; }

    public event EventHandler<GeoMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<DeviceStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;

    public Task? Completion => _runningTask;

    public void Start(CancellationToken cancellationToken)
    {
        if (_runningTask != null && !_runningTask.IsCompleted)
        {
            throw new InvalidOperationException("Worker is already running.");
        }

        _runningTask = Task.Run(() => RunAsync(cancellationToken), cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = _reconnectSettings.EnsureValidDelay(null);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UpdateStatus("Connecting");
                Log($"Connecting to {Device.Host}:{Device.Port}");

                using var client = new TcpClient();
                await client.ConnectAsync(Device.Host, Device.Port, cancellationToken).ConfigureAwait(false);
                UpdateStatus("Connected");
                Log("Connection established.");

                await using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

                delay = _reconnectSettings.EnsureValidDelay(null);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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
                        var messages = JsonSerializer.Deserialize<List<GeoMessageDto>>(line, SerializerOptions);
                        if (messages == null)
                        {
                            continue;
                        }

                        foreach (var message in messages)
                        {
                            if (string.IsNullOrWhiteSpace(message.Id))
                            {
                                message.Id = Device.StationId;
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            UpdateStatus("Reconnecting...");
            Log($"Reconnecting in {(int)delay.TotalSeconds} seconds...");

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Stopped");
                Log("Reconnect cancelled.");
                break;
            }

            delay = _reconnectSettings.NextDelay(delay);
        }
    }

    private void UpdateStatus(string status)
    {
        StatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs(Device, status, DateTime.UtcNow));
    }

    private void OnMessageReceived(GeoMessageDto message)
    {
        MessageReceived?.Invoke(this, new GeoMessageReceivedEventArgs(message, Device));
    }

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, new LogMessageEventArgs(Device.StationId, message, DateTime.UtcNow));
    }
}

public sealed class GeoMessageReceivedEventArgs : EventArgs
{
    public GeoMessageReceivedEventArgs(GeoMessageDto message, DeviceEntryDto device)
    {
        Message = message;
        Device = device;
    }

    public GeoMessageDto Message { get; }
    public DeviceEntryDto Device { get; }
}

public sealed class DeviceStatusChangedEventArgs : EventArgs
{
    public DeviceStatusChangedEventArgs(DeviceEntryDto device, string status, DateTime timestamp)
    {
        Device = device;
        Status = status;
        Timestamp = timestamp;
    }

    public DeviceEntryDto Device { get; }
    public string Status { get; }
    public DateTime Timestamp { get; }
}
