using System.Text.Json.Serialization;

namespace GeoTrack.Models;

public sealed class AggregatePayloadDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("metrics")]
    public AggregateMetricsDto Metrics { get; set; } = new();

    [JsonPropertyName("buggies")]
    public List<BuggyDto> Buggies { get; set; } = new();
}


public sealed class AggregateMetricsDto
{
    [JsonPropertyName("totalDevices")]
    public int TotalDevices { get; set; }

    [JsonPropertyName("activeDevices")]
    public int ActiveDevices { get; set; }
}
public sealed class BuggyDto
{
    [JsonPropertyName("stationId")]
    public string StationId { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
    [JsonPropertyName("sats")]
    public double Sats { get; set; }


    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("idleDurationSeconds")]
    public int IdleDurationSeconds { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}


public class CommonResultDto<T>
{
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errorDetail")]
    public CommonResultDetailDto? ErrorDetail { get; set; }

    [JsonPropertyName("notification")]
    public CommonResultDetailDto? Notification { get; set; }

    [JsonPropertyName("errors")]
    public List<ValidateInputDto> Errors { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class CommonResultDetailDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("paramMessage")]
    public string[]? ParamMessage { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public class ValidateInputDto
{
    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
}


public sealed class DeviceConfigDto
{
    [JsonPropertyName("server")]
    public ServerConfigDto? Server { get; set; }

    [JsonPropertyName("devices")]
    public List<DeviceEntryDto> Devices { get; set; } = new();

    [JsonPropertyName("externalApp")]
    public ExternalAppConfigDto? ExternalApp { get; set; }

    [JsonPropertyName("reconnect")]
    public ReconnectConfigDto? Reconnect { get; set; }

    [JsonPropertyName("appBehavior")]
    public AppBehaviorConfigDto? AppBehavior { get; set; }
}

public sealed class ServerConfigDto
{
    [JsonPropertyName("listenIp")]
    public string ListenIp { get; set; } = "0.0.0.0";

    [JsonPropertyName("listenPort")]
    public int ListenPort { get; set; } = 5099;
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

    [JsonPropertyName("seccretToken")]
    public string SeccretToken { get; set; } = string.Empty;

    [JsonPropertyName("sendIntervalSeconds")]
    public int SendIntervalSeconds { get; set; } = 5;

    [JsonPropertyName("endpoints")]
    public ExternalAppEndpointsConfigDto Endpoints { get; set; } = new();

    [JsonPropertyName("http")]
    public ExternalAppHttpConfigDto Http { get; set; } = new();

    [JsonPropertyName("socketUrl")]
    public string SocketUrl { get; set; } = string.Empty;
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


public sealed class ExternalAppErrorDto
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}


public sealed class GeoMessageDto
{
    [JsonPropertyName("id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("datetime")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lng")]
    public double Longitude { get; set; }

    [JsonPropertyName("sats")]
    public double Sats { get; set; }
}
public sealed class TokenResponseDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expireInSeconds")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
}

public sealed class SocketIoMessageInputDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("long")]
    public double Longitude { get; set; }

    [JsonPropertyName("sats")]
    public double Sats { get; set; }
}
