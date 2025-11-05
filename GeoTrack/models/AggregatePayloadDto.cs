// using System.Text.Json.Serialization;
//
// namespace GeoTrack.Models;
//
// public sealed class AggregatePayloadDto
// {
//     [JsonPropertyName("timestamp")]
//     public DateTimeOffset Timestamp { get; set; }
//
//     [JsonPropertyName("metrics")]
//     public AggregateMetricsDto Metrics { get; set; } = new();
//
//     [JsonPropertyName("buggies")]
//     public List<BuggyDto> Buggies { get; set; } = new();
// }
//
// public sealed class AggregateMetricsDto
// {
//     [JsonPropertyName("totalDevices")]
//     public int TotalDevices { get; set; }
//
//     [JsonPropertyName("activeDevices")]
//     public int ActiveDevices { get; set; }
// }
