using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeoTrack.Models;

namespace GeoTrack
{
    public sealed class TcpServer : IDisposable
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new();
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;

        public TcpServer(IPAddress ipAddress, int port)
        {
            _listener = new TcpListener(ipAddress, port);
        }

        public event EventHandler<GeoMessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<LogMessageEventArgs>? LogGenerated;
        public event EventHandler<DeviceStatusChangedEventArgs>? ClientConnected; // reuse DeviceStatusChangedEventArgs but DeviceEntryDto may be null
        public event EventHandler<DeviceStatusChangedEventArgs>? ClientDisconnected;

        public void Start(CancellationToken cancellationToken)
        {
            if (_acceptLoop != null && !_acceptLoop.IsCompleted)
            {
                throw new InvalidOperationException("Server already started.");
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener.Start();
            Log($"TCP server started on {_listener.LocalEndpoint}");
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                _listener.Stop();
                foreach (var kv in _clients)
                {
                    try { kv.Value.Close(); kv.Value.Dispose(); } catch { }
                }
                _clients.Clear();
                if (_acceptLoop != null) await _acceptLoop.ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    var id = Guid.NewGuid();
                    _clients[id] = client;

                    var remote = client.Client.RemoteEndPoint?.ToString() ?? id.ToString();
                    Log($"Client connected: {remote}");
                    ClientConnected?.Invoke(this, new DeviceStatusChangedEventArgs(new DeviceEntryDto { StationId = remote, Host = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString(), Port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port }, "Connected", DateTime.UtcNow));

                    _ = Task.Run(() => HandleClientAsync(id, client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException) { /* stopped */ }
            catch (Exception ex)
            {
                Log($"Accept loop error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(Guid id, TcpClient client, CancellationToken cancellationToken)
        {
            var remote = client.Client.RemoteEndPoint?.ToString() ?? id.ToString();
            try
            {
                using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        // Expecting line to be a JSON array of GeoMessageDto like existing client
                        var messages = JsonSerializer.Deserialize<List<GeoMessageDto>>(line, SerializerOptions);
                        if (messages == null) continue;

                        foreach (var message in messages)
                        {
                            // If payload contains no DeviceId, we can set DeviceId to remote endpoint (or to a stationId field if provided)
                            if (string.IsNullOrWhiteSpace(message.DeviceId))
                            {
                                // attempt to use a field named "stationId" if client included (not in GeoMessageDto by default)
                                message.DeviceId = remote;
                            }

                            // stationId we will pass as remote endpoint string (client may include stationId in the payload, but keep remote as fallback)
                            var stationId = remote;
                            OnMessageReceived(message, stationId);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Log($"Invalid JSON from {remote}: {jsonEx.Message}");
                    }
                }
            }
            catch (IOException) { /* connection closed */ }
            catch (Exception ex)
            {
                Log($"Client {remote} error: {ex.Message}");
            }
            finally
            {
                try
                {
                    client.Close();
                    client.Dispose();
                }
                catch { }
                _clients.TryRemove(id, out _);
                Log($"Client disconnected: {remote}");
                ClientDisconnected?.Invoke(this, new DeviceStatusChangedEventArgs(new DeviceEntryDto { StationId = remote, Host = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "", Port = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Port ?? 0 }, "Disconnected", DateTime.UtcNow));
            }
        }

        private void OnMessageReceived(GeoMessageDto message, string stationId)
        {
            // Reuse GeoMessageReceivedEventArgs - it expects DeviceEntryDto; create a transient DeviceEntryDto with station info
            var device = new DeviceEntryDto { StationId = stationId, Host = "", Port = 0 };
            MessageReceived?.Invoke(this, new GeoMessageReceivedEventArgs(message, device));
        }

        private void Log(string message)
        {
            LogGenerated?.Invoke(this, new LogMessageEventArgs("TcpServer", message, DateTime.UtcNow));
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            foreach (var kv in _clients)
            {
                try { kv.Value.Close(); kv.Value.Dispose(); } catch { }
            }
            _clients.Clear();
            _cts?.Dispose();
        }
    }
}
