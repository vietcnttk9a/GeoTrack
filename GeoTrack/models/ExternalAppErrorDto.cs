using System.Text.Json.Serialization;

namespace GeoTrack.Models;

public sealed class ExternalAppErrorDto
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
