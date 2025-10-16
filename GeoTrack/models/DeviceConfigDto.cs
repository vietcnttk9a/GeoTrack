using System.Text.Json.Serialization;

namespace GeoTrack.Models;

public sealed class DeviceConfigDto
{
    [JsonPropertyName("devices")]
    public List<DeviceEntryDto> Devices { get; set; } = new();

    [JsonPropertyName("externalApp")]
    public ExternalAppConfigDto? ExternalApp { get; set; }

    [JsonPropertyName("reconnect")]
    public ReconnectConfigDto? Reconnect { get; set; }

    [JsonPropertyName("appBehavior")]
    public AppBehaviorConfigDto? AppBehavior { get; set; }
}

public sealed class DeviceEntryDto
{
    [JsonPropertyName("stationId")]
    public string StationId { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("reconnect")]
    public ReconnectConfigDto? Reconnect { get; set; }
}

public sealed class ExternalAppConfigDto
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("sendIntervalSeconds")]
    public int SendIntervalSeconds { get; set; } = 5;

    [JsonPropertyName("endpoints")]
    public ExternalAppEndpointsConfigDto Endpoints { get; set; } = new();

    [JsonPropertyName("http")]
    public ExternalAppHttpConfigDto Http { get; set; } = new();
}

public sealed class ExternalAppEndpointsConfigDto
{
    [JsonPropertyName("loginPath")]
    public string LoginPath { get; set; } = string.Empty;

    [JsonPropertyName("refreshPath")]
    public string RefreshPath { get; set; } = string.Empty;

    [JsonPropertyName("aggregatePath")]
    public string AggregatePath { get; set; } = string.Empty;
}

public sealed class ExternalAppHttpConfigDto
{
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 10;

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; } = 3;

    [JsonPropertyName("retryDelaySeconds")]
    public int RetryDelaySeconds { get; set; } = 2;
}

public sealed class ReconnectConfigDto
{
    [JsonPropertyName("initialDelaySeconds")]
    public int InitialDelaySeconds { get; set; } = 10;

    [JsonPropertyName("maxDelaySeconds")]
    public int MaxDelaySeconds { get; set; } = 60;

    [JsonPropertyName("useExponentialBackoff")]
    public bool UseExponentialBackoff { get; set; } = true;
}

public sealed class AppBehaviorConfigDto
{
    [JsonPropertyName("runInBackgroundWhenHidden")]
    public bool RunInBackgroundWhenHidden { get; set; } = true;

    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; }

    [JsonPropertyName("tray")]
    public TrayConfigDto Tray { get; set; } = new();
}

public sealed class TrayConfigDto
{
    [JsonPropertyName("showTrayIcon")]
    public bool ShowTrayIcon { get; set; } = true;

    [JsonPropertyName("balloonOnBackground")]
    public bool BalloonOnBackground { get; set; }
}
