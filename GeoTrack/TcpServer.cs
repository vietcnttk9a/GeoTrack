using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class TcpServer : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<Guid, ClientContext> _clients = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public TcpServer(IPAddress ipAddress, int port)
    {
        _listener = new TcpListener(ipAddress, port);
    }

    public event EventHandler<GeoMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<LogMessageEventArgs>? LogGenerated;
    public event EventHandler<DeviceStatusChangedEventArgs>? ClientConnected;
    public event EventHandler<DeviceStatusChangedEventArgs>? ClientDisconnected;

    public void Start(CancellationToken cancellationToken)
    {
        if (_acceptLoop != null && !_acceptLoop.IsCompleted)
        {
            throw new InvalidOperationException("Server already started.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        Log($"Server listening on {_listener.LocalEndpoint}");
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
    }

    public async Task StopAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }

        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignored
        }

        foreach (var kvp in _clients)
        {
            try
            {
                kvp.Value.Client.Close();
                kvp.Value.Client.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        _clients.Clear();

        if (_acceptLoop != null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch
            {
                // ignored
            }

            _acceptLoop = null;
        }

        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // ignored
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var context = CreateClientContext(client);
                if (!_clients.TryAdd(context.Id, context))
                {
                    try
                    {
                        context.Client.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }

                    continue;
                }

                Log($"Client connected: {context.StationId}");
                ClientConnected?.Invoke(this, new DeviceStatusChangedEventArgs(context.CreateDeviceEntry(), "Connected", DateTime.UtcNow));

                _ = Task.Run(() => HandleClientAsync(context, cancellationToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            Log($"Accept loop error: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(ClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(context.Client.GetStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line == null)
                {
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
                        if (string.IsNullOrWhiteSpace(message.DeviceId))
                        {
                            message.DeviceId = context.StationId;
                        }

                        var device = context.CreateDeviceEntry();
                        MessageReceived?.Invoke(this, new GeoMessageReceivedEventArgs(message, device));
                    }
                }
                catch (JsonException jsonException)
                {
                    Log($"Invalid JSON from {context.StationId}: {jsonException.Message}");
                }
            }
        }
        catch (IOException)
        {
            // connection closed
        }
        catch (ObjectDisposedException)
        {
            // connection disposed
        }
        catch (Exception ex)
        {
            Log($"Client {context.StationId} error: {ex.Message}");
        }
        finally
        {
            CleanupClient(context);
        }
    }

    private void CleanupClient(ClientContext context)
    {
        if (_clients.TryRemove(context.Id, out _))
        {
            try
            {
                context.Client.Close();
                context.Client.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        Log($"Client disconnected: {context.StationId}");
        ClientDisconnected?.Invoke(this, new DeviceStatusChangedEventArgs(context.CreateDeviceEntry(), "Disconnected", DateTime.UtcNow));
    }

    private ClientContext CreateClientContext(TcpClient client)
    {
        var id = Guid.NewGuid();
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        var stationId = endpoint?.ToString() ?? id.ToString();
        var host = endpoint?.Address.ToString() ?? string.Empty;
        var port = endpoint?.Port ?? 0;

        client.NoDelay = true;

        return new ClientContext(id, client, stationId, host, port);
    }

    private void Log(string message)
    {
        LogGenerated?.Invoke(this, new LogMessageEventArgs("TcpServer", message, DateTime.UtcNow));
    }

    private sealed class ClientContext
    {
        public ClientContext(Guid id, TcpClient client, string stationId, string host, int port)
        {
            Id = id;
            Client = client;
            StationId = stationId;
            Host = host;
            Port = port;
        }

        public Guid Id { get; }
        public TcpClient Client { get; }
        public string StationId { get; }
        public string Host { get; }
        public int Port { get; }

        public DeviceEntryDto CreateDeviceEntry()
        {
            return new DeviceEntryDto
            {
                StationId = StationId,
                Host = Host,
                Port = Port
            };
        }
    }
}
