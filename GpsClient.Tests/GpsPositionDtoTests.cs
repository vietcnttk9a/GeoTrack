using System;
using System.Text.Json;
using GpsClient.Models;
using Xunit;

namespace GpsClient.Tests;

public class GpsPositionDtoTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    [Fact]
    public void Serialize_UsesNewFieldNames()
    {
        var dto = new GpsPositionDto
        {
            Id = "BUGGY-001",
            Datetime = new DateTime(2025, 10, 16, 14, 28, 5, DateTimeKind.Utc),
            Lat = 10.7626227,
            Lng = 106.6601725,
            Sats = 12
        };

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.Contains("\"id\":\"BUGGY-001\"", json);
        Assert.Contains("\"datetime\":\"2025-10-16T14:28:05Z\"", json);
        Assert.Contains("\"lat\":10.7626227", json);
        Assert.Contains("\"lng\":106.6601725", json);
        Assert.Contains("\"sats\":12", json);
    }

    [Fact]
    public void Serialize_DoesNotEmitLegacyFields()
    {
        var dto = new GpsPositionDto
        {
            Id = "BUGGY-001",
            Datetime = DateTime.UtcNow,
            Lat = 1,
            Lng = 2,
            Sats = 3
        };

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.DoesNotContain("speedKph", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("headingDeg", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("batteryPct", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deviceId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timestamp", json, StringComparison.OrdinalIgnoreCase);
    }
}
