using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GpsClient;

internal sealed class Program
{
    private const double MinLatitude = 8.18;
    private const double MaxLatitude = 23.39;
    private const double MinLongitude = 102.14;
    private const double MaxLongitude = 109.46;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static async Task Main(string[] args)
    {
        var options = CommandLineOptions.Parse(args);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested. Closing listener...");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var listener = new TcpListener(IPAddress.Loopback, options.Port);
        listener.Start();

        Console.WriteLine($"GPS device {options.DeviceId} listening on 127.0.0.1:{options.Port}");
        Console.WriteLine("Press Ctrl+C to exit.");

        while (!cts.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine("Waiting for GeoTrack to connect...");
                using var client = await listener.AcceptTcpClientAsync(cts.Token);
                Console.WriteLine("GeoTrack connected. Streaming location updates...");

                await StreamLocationsAsync(client, options, cts.Token);
                Console.WriteLine("Connection closed. Returning to listening state.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while handling connection: {ex.Message}");
            }
        }

        listener.Stop();
    }

    private static async Task StreamLocationsAsync(TcpClient client, CommandLineOptions options, CancellationToken cancellationToken)
    {
        client.NoDelay = true;

        await using var networkStream = client.GetStream();
        using var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
        var random = new Random();

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = new GeoReading
            {
                DeviceId = options.DeviceId,
                Timestamp = DateTime.UtcNow,
                Latitude = NextDouble(random, MinLatitude, MaxLatitude),
                Longitude = NextDouble(random, MinLongitude, MaxLongitude),
                SpeedKph = Math.Round(NextDouble(random, 0, 120), 2),
                HeadingDeg = Math.Round(NextDouble(random, 0, 360), 2),
                BatteryPct = Math.Round(NextDouble(random, 25, 100), 1),
                Status = PickStatus(random)
            };

            var json = JsonSerializer.Serialize(new[] { message }, JsonOptions);

            try
            {
                await writer.WriteLineAsync(json);
            }
            catch (IOException)
            {
                Console.WriteLine("GeoTrack disconnected.");
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

    private static double NextDouble(Random random, double minValue, double maxValue)
    {
        return minValue + (random.NextDouble() * (maxValue - minValue));
    }

    private static string PickStatus(Random random)
    {
        return random.Next(0, 3) switch
        {
            0 => "OK",
            1 => "MOVING",
            _ => "IDLE"
        };
    }

    private sealed class CommandLineOptions
    {
        private CommandLineOptions(string deviceId, int port)
        {
            DeviceId = deviceId;
            Port = port;
        }

        public string DeviceId { get; }
        public int Port { get; }

        public static CommandLineOptions Parse(string[] args)
        {
            var deviceId = "GPS-0001";
            var port = 5001;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    if (int.TryParse(args[++index], out var parsedPort))
                    {
                        port = parsedPort;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid port value '{args[index]}'. Using default {port}.");
                    }
                }
                else if (string.Equals(arg, "--deviceId", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    deviceId = args[++index];
                }
                else
                {
                    Console.WriteLine($"Unrecognized argument '{arg}'.");
                }
            }

            return new CommandLineOptions(deviceId, port);
        }
    }

    private sealed class GeoReading
    {
        public string DeviceId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public double Lat
        {
            get => Latitude;
            set => Latitude = value;
        }

        public double Lng
        {
            get => Longitude;
            set => Longitude = value;
        }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKph { get; set; }
        public double HeadingDeg { get; set; }
        public double BatteryPct { get; set; }
        public string Status { get; set; } = "OK";
    }
}
