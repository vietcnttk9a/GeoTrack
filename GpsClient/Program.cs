using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GpsClient;

internal sealed class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ConfigOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly (double Lat, double Lng)[] HoleCoordinates =
    {
        (20.97381234081408, 105.39408802986146),
        (20.97266026779571, 105.39425969123842),
        (20.971327859231653, 105.39372324943544),
        (20.96854278643389, 105.39591193199158),
        (20.970255912893922, 105.39561152458192),
        (20.97381234081408, 105.39484977722168)
    };

    private static async Task Main()
    {
        var config = await LoadConfigAsync().ConfigureAwait(false);

        if (config.Devices.Count == 0)
        {
            Console.WriteLine("No devices configured. Update gpsclient.config.json and restart.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, config.SendIntervalSeconds));

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Stopping client...");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var simulationStates = CreateInitialSimulationStates(config.Devices);
        var random = new Random();

        while (!cts.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                Console.WriteLine($"Connecting to {config.ServerHost}:{config.ServerPort} ...");
                client = new TcpClient();
                await client.ConnectAsync(config.ServerHost, config.ServerPort, cts.Token).ConfigureAwait(false);
                client.NoDelay = true;
                Console.WriteLine("Connected to GeoTrack server.");

                await RunSendLoopAsync(client, config, simulationStates, random, interval, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        client.Close();
                        client.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (cts.IsCancellationRequested)
            {
                break;
            }

            Console.WriteLine("Retrying in 3 seconds...");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine("GpsClient stopped.");
    }

    private static async Task RunSendLoopAsync(
        TcpClient client,
        GpsClientConfig config,
        Dictionary<string, DeviceSimulationState> simulationStates,
        Random random,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        await using var networkStream = client.GetStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var messages = new List<GeoReading>(config.Devices.Count);

            foreach (var deviceId in config.Devices)
            {
                var (lat, lng) = GetNextPosition(deviceId, simulationStates);
                var message = new GeoReading
                {
                    Id = deviceId,
                    Datetime = now.ToString("O"),
                    Lat = lat,
                    Lng = lng,
                    Sats = random.Next(8, 16)
                };

                messages.Add(message);
            }

            var json = JsonSerializer.Serialize(messages, JsonOptions) + "\n";
            var buffer = Encoding.UTF8.GetBytes(json);

            try
            {
                await networkStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                Console.WriteLine($"Disconnected while sending: {ex.Message}");
                break;
            }

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task<GpsClientConfig> LoadConfigAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "gpsclient.config.json");

        if (!File.Exists(path))
        {
            Console.WriteLine($"Configuration file not found at '{path}'. Using defaults.");
            var fallback = new GpsClientConfig();
            fallback.Normalize();
            return fallback;
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var config = await JsonSerializer.DeserializeAsync<GpsClientConfig>(stream, ConfigOptions).ConfigureAwait(false)
                         ?? new GpsClientConfig();
            config.Normalize();
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse configuration file: {ex.Message}. Using defaults.");
            var fallback = new GpsClientConfig();
            fallback.Normalize();
            return fallback;
        }
    }

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

        for (var i = 0; i < deviceIds.Count; i++)
        {
            var id = deviceIds[i];
            var stopTicks = 6 + i;

            dict[id] = new DeviceSimulationState
            {
                CurrentHoleIndex = 0,
                IsStopping = true,
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

        if (state.IsStopping)
        {
            state.RemainingStopTicks--;

            if (state.RemainingStopTicks <= 0)
            {
                state.IsStopping = false;
                state.MoveStepIndex = 0;
            }

            return from;
        }

        var toIndex = (state.CurrentHoleIndex + 1) % HoleCoordinates.Length;
        var to = HoleCoordinates[toIndex];

        const int MoveStepsBetweenHoles = 6;

        var step = Math.Min(state.MoveStepIndex, MoveStepsBetweenHoles);
        var t = (double)step / MoveStepsBetweenHoles;

        var lat = from.Lat + (to.Lat - from.Lat) * t;
        var lng = from.Lng + (to.Lng - from.Lng) * t;

        state.MoveStepIndex++;

        if (state.MoveStepIndex > MoveStepsBetweenHoles)
        {
            state.CurrentHoleIndex = toIndex;
            state.IsStopping = true;
            state.RemainingStopTicks = state.StopTicks;
            state.MoveStepIndex = 0;
        }

        return (lat, lng);
    }

    private sealed class GpsClientConfig
    {
        public string ServerHost { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 5099;
        public int SendIntervalSeconds { get; set; } = 5;
        public List<string> Devices { get; set; } = new() { "001", "002", "003" };

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(ServerHost))
            {
                ServerHost = "127.0.0.1";
            }

            if (ServerPort <= 0)
            {
                ServerPort = 5099;
            }

            if (SendIntervalSeconds <= 0)
            {
                SendIntervalSeconds = 5;
            }

            Devices = Devices
                .Select(d => d?.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private sealed class GeoReading
    {
        public string Id { get; set; } = string.Empty;
        public string Datetime { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public int Sats { get; set; }
    }
}
