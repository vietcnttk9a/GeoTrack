using System.Text.Json.Serialization;

namespace GeoTrack;

public sealed class DeviceConfig
{
    [JsonPropertyName("devices")]
    public List<DeviceEntry> Devices { get; set; } = new();
}

public sealed class DeviceEntry
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; }
}
