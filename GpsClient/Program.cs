using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GpsClient
{
    internal sealed class Program
    {
        // Thời gian giữa hai lần gửi (5s)
        private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(5);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        // Danh sách tọa độ 6 hố (1E -> 6E)
        // Index 0 = hố 1E, 1 = 2E, ...
        private static readonly (double Lat, double Lng)[] HoleCoordinates =
        {
            (20.97381234081408, 105.39408802986146), // 1E
            (20.97266026779571, 105.39425969123842), // 2E
            (20.971327859231653, 105.39372324943544), // 3E
            (20.96854278643389, 105.39591193199158), // 4E
            (20.970255912893922, 105.39561152458192), // 5E
            (20.97381234081408, 105.39484977722168)  // 6E
        };

        private static async Task Main(string[] args)
        {
            var options = CommandLineOptions.Parse(args);

            if (!options.Ports.Any())
            {
                Console.WriteLine("No ports provided. Use --port 5001 or --ports 5001,5002,5003");
                return;
            }

            if (!options.DeviceIds.Any())
            {
                Console.WriteLine("No deviceIds configured. Use --deviceIds 001,002,003");
                return;
            }

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Cancellation requested. Stopping listeners...");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            var listenerTasks = new List<Task>();
            var listeners = new List<TcpListener>();

            var distinctPorts = options.Ports.Distinct().ToList();
            var deviceIdsInfo = string.Join(",", options.DeviceIds);

            foreach (var port in distinctPorts)
            {
                var listener = new TcpListener(options.BindAddress, port);
                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start listener on {options.BindAddress}:{port} - {ex.Message}");
                    continue;
                }

                listeners.Add(listener);
                Console.WriteLine(
                    $"Station listening on {options.BindAddress}:{port} with devices [{deviceIdsInfo}]");

                listenerTasks.Add(RunListenerAsync(listener, options.DeviceIds, cts.Token));
            }

            if (!listenerTasks.Any())
            {
                Console.WriteLine("No listener started. Exiting.");
                return;
            }

            Console.WriteLine("Press Ctrl+C to stop all listeners.");

            try
            {
                await Task.WhenAll(listenerTasks);
            }
            catch (OperationCanceledException)
            {
                // Ignore, shutting down
            }
            finally
            {
                foreach (var listener in listeners)
                {
                    try
                    {
                        listener.Stop();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                Console.WriteLine("All listeners stopped.");
            }
        }

        private static async Task RunListenerAsync(
            TcpListener listener,
            IReadOnlyList<string> deviceIds,
            CancellationToken cancellationToken)
        {
            var localEndPoint = (IPEndPoint)listener.LocalEndpoint;
            var port = localEndPoint.Port;

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    Console.WriteLine(
                        $"[Station] Waiting for GeoTrack to connect on {localEndPoint.Address}:{port} ...");
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException)
                {
                    // listener đã stop
                    break;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    Console.WriteLine($"[Station] Error accepting client on port {port}: {ex.Message}");
                    await Task.Delay(500, cancellationToken).ContinueWith(_ => { }, cancellationToken);
                    continue;
                }

                Console.WriteLine(
                    $"[Station] GeoTrack connected from {client.Client.RemoteEndPoint} on port {port}. Starting stream...");

                _ = HandleClientAsync(client, deviceIds, cancellationToken);
            }

            Console.WriteLine($"[Station] Listener loop on port {port} finished.");
        }

        private static async Task HandleClientAsync(
            TcpClient client,
            IReadOnlyList<string> deviceIds,
            CancellationToken cancellationToken)
        {
            client.NoDelay = true;

            await using var networkStream = client.GetStream();
            using var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

            var random = new Random();

            // Mỗi connection có một bộ state giả lập riêng cho từng thiết bị
            var simulationStates = CreateInitialSimulationStates(deviceIds);

            try
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var now = DateTime.UtcNow;

                    // Mỗi lần gửi: 1 mảng nhiều thiết bị (001,002,003,...)
                    var messages = new List<GeoReading>(deviceIds.Count);
                    foreach (var id in deviceIds)
                    {
                        var (lat, lng) = GetNextPosition(id, simulationStates);

                        var message = new GeoReading
                        {
                            Id = id,                // "001", "002", "003", ...
                            Datetime = now,         // "2025-11-05T14:30:00Z"
                            Lat = lat,
                            Lng = lng,
                            Sats = random.Next(8, 16) // 8–15 vệ tinh
                        };

                        messages.Add(message);
                    }

                    var json = JsonSerializer.Serialize(messages, JsonOptions);

                    try
                    {
                        await writer.WriteLineAsync(json);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("[Station] GeoTrack disconnected (write failed).");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("[Station] Network stream disposed.");
                        break;
                    }

                    try
                    {
                        await Task.Delay(SendInterval, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // ignore
                }

                Console.WriteLine("[Station] Connection closed.");
            }
        }

        // --- Logic giả lập di chuyển qua các hố ------------------------------

        /// <summary>
        /// Mỗi device có 1 state:
        /// - CurrentHoleIndex: đang ở hố nào (0..5)
        /// - IsStopping: đang dừng tại hố hay đang di chuyển
        /// - StopTicks: số lần gửi giữ nguyên tọa độ tại mỗi hố
        ///   (001: 6 tick = 30s, 002: 7 tick = 35s, 003: 8 tick = 40s, ...)
        /// - RemainingStopTicks: còn bao nhiêu tick dừng
        /// - MoveStepIndex: bước nội suy giữa 2 hố
        /// </summary>
        private sealed class DeviceSimulationState
        {
            public int CurrentHoleIndex { get; set; }
            public bool IsStopping { get; set; }
            public int StopTicks { get; set; }
            public int RemainingStopTicks { get; set; }
            public int MoveStepIndex { get; set; }
        }

        private static Dictionary<string, DeviceSimulationState> CreateInitialSimulationStates(IReadOnlyList<string> deviceIds)
        {
            var dict = new Dictionary<string, DeviceSimulationState>(StringComparer.OrdinalIgnoreCase);

            // Mặc định:
            // device 0 (001) -> dừng 30s (6 tick)
            // device 1 (002) -> dừng 35s (7 tick)
            // device 2 (003) -> dừng 40s (8 tick)
            for (var i = 0; i < deviceIds.Count; i++)
            {
                var id = deviceIds[i];
                var stopTicks = 6 + i; // 5s * (6 + i)

                dict[id] = new DeviceSimulationState
                {
                    CurrentHoleIndex = 0,   // bắt đầu từ hố 1E
                    IsStopping = true,      // mới vào hố -> đang dừng
                    StopTicks = stopTicks,
                    RemainingStopTicks = stopTicks,
                    MoveStepIndex = 0
                };
            }

            return dict;
        }

        private static (double Lat, double Lng) GetNextPosition(
            string deviceId,
            Dictionary<string, DeviceSimulationState> states)
        {
            if (!states.TryGetValue(deviceId, out var state))
            {
                // fallback: nếu vì lý do gì đó chưa có state
                state = new DeviceSimulationState
                {
                    CurrentHoleIndex = 0,
                    IsStopping = true,
                    StopTicks = 6,
                    RemainingStopTicks = 6,
                    MoveStepIndex = 0
                };
                states[deviceId] = state;
            }

            var from = HoleCoordinates[state.CurrentHoleIndex];

            // Đang dừng tại hố
            if (state.IsStopping)
            {
                state.RemainingStopTicks--;

                if (state.RemainingStopTicks <= 0)
                {
                    // Hết thời gian dừng -> chuẩn bị di chuyển sang hố tiếp theo
                    state.IsStopping = false;
                    state.MoveStepIndex = 0;
                }

                return from;
            }

            // Đang di chuyển từ hố hiện tại sang hố tiếp theo
            var toIndex = (state.CurrentHoleIndex + 1) % HoleCoordinates.Length;
            var to = HoleCoordinates[toIndex];

            const int MoveStepsBetweenHoles = 6; // 6 bước = 6*5s = 30s di chuyển

            var step = Math.Min(state.MoveStepIndex, MoveStepsBetweenHoles);
            var t = (double)step / MoveStepsBetweenHoles;

            var lat = from.Lat + (to.Lat - from.Lat) * t;
            var lng = from.Lng + (to.Lng - from.Lng) * t;

            state.MoveStepIndex++;

            // Đã đến hố tiếp theo
            if (state.MoveStepIndex > MoveStepsBetweenHoles)
            {
                state.CurrentHoleIndex = toIndex;
                state.IsStopping = true;
                state.RemainingStopTicks = state.StopTicks;
                state.MoveStepIndex = 0;
            }

            return (lat, lng);
        }

        // --- CLI options -----------------------------------------------------

        private sealed class CommandLineOptions
        {
            public List<int> Ports { get; }
            public IPAddress BindAddress { get; }
            public List<string> DeviceIds { get; }

            public CommandLineOptions(
                IEnumerable<int> ports,
                IPAddress bindAddress,
                IEnumerable<string> deviceIds)
            {
                Ports = ports.ToList();
                BindAddress = bindAddress;
                DeviceIds = deviceIds.ToList();
            }

            public static CommandLineOptions Parse(string[] args)
            {
                // default: 3 port 5001,5002,5003, loopback, 3 thiết bị 001/002/003
                var ports = new List<int> { 5001, 5002, 5003 };
                var bindAddress = IPAddress.Loopback;
                var deviceIds = new List<string> { "001", "002", "003" };

                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];

                    if (string.Equals(arg, "--ports", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        var raw = args[++i];
                        ports = ParsePorts(raw);
                    }
                    else if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out var singlePort))
                        {
                            ports = new List<int> { singlePort };
                        }
                    }
                    else if (string.Equals(arg, "--deviceIds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        var raw = args[++i];
                        var ids = raw
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                        if (ids.Any())
                        {
                            deviceIds = ids;
                        }
                    }
                    else if (string.Equals(arg, "--deviceId", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        // Backward compatible: 1 thiết bị
                        var id = args[++i];
                        deviceIds = new List<string> { id };
                    }
                    else if (string.Equals(arg, "--bind", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        var ip = args[++i];
                        if (!IPAddress.TryParse(ip, out var parsed))
                        {
                            Console.WriteLine($"Invalid bind address '{ip}', using loopback.");
                        }
                        else
                        {
                            bindAddress = parsed;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unrecognized argument '{arg}'.");
                    }
                }

                return new CommandLineOptions(ports, bindAddress, deviceIds);
            }

            // Hỗ trợ:
            //  - "5001"
            //  - "5001,5002,5005"
            //  - "5001-5005"
            //  - mix: "5001,5003-5005"
            private static List<int> ParsePorts(string raw)
            {
                var list = new List<int>();

                var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var p in parts)
                {
                    if (p.Contains('-'))
                    {
                        var bounds = p.Split('-', StringSplitOptions.TrimEntries);
                        if (bounds.Length == 2 &&
                            int.TryParse(bounds[0], out var start) &&
                            int.TryParse(bounds[1], out var end))
                        {
                            if (start > end)
                            {
                                (start, end) = (end, start);
                            }

                            for (var x = start; x <= end; x++)
                            {
                                list.Add(x);
                            }
                        }
                    }
                    else if (int.TryParse(p, out var port))
                    {
                        list.Add(port);
                    }
                }

                return list;
            }
        }

        // --- DTO gửi lên GeoTrack --------------------------------------------

        private sealed class GeoReading
        {
            // "id": "001"
            public string Id { get; set; } = string.Empty;

            // "datetime": "2025-11-05T14:30:00Z"
            public DateTime Datetime { get; set; }

            // "lat": ...
            public double Lat { get; set; }

            // "lng": ...
            public double Lng { get; set; }

            // "sats": 12
            public int Sats { get; set; }
        }
    }
}
