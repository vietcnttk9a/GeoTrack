using System.Text.Json.Serialization;

namespace GeoTrack;

public sealed class DeviceConfig
{
    [JsonPropertyName("devices")]
    public List<DeviceEntry> Devices { get; set; } = new();

    [JsonPropertyName("reconnectIntervalSeconds")]
    public int? ReconnectIntervalSeconds { get; set; }

    [JsonPropertyName("externalApp")]
    public ExternalAppConfig? ExternalApp { get; set; }
}

public sealed class DeviceEntry
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("reconnectIntervalSeconds")]
    public int? ReconnectIntervalSeconds { get; set; }
}

public sealed class ExternalAppConfig
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("sendIntervalSeconds")]
    public int SendIntervalSeconds { get; set; } = 5;
}
