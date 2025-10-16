using System.Text.Json.Serialization;

namespace GeoTrack.Models;

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

    [JsonPropertyName("speedKph")]
    public double SpeedKph { get; set; }

    [JsonPropertyName("headingDeg")]
    public double HeadingDeg { get; set; }

    [JsonPropertyName("batteryPct")]
    public double BatteryPct { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
