using System;
using System.Collections.Concurrent;
using System.Linq;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class BuggyRepository
{
    private readonly ConcurrentDictionary<string, BuggyDto> _buggies = new(StringComparer.OrdinalIgnoreCase);

    public void Update(string stationId, GeoMessageDto message)
    {
        if (string.IsNullOrWhiteSpace(stationId))
        {
            stationId = message.DeviceId;
        }

        var buggy = new BuggyDto
        {
            StationId = stationId,
            DeviceId = string.IsNullOrWhiteSpace(message.DeviceId) ? stationId : message.DeviceId,
            Latitude = message.Latitude,
            Longitude = message.Longitude,
            Sats = message.Sats,
            SpeedKph = message.SpeedKph,
            HeadingDeg = message.HeadingDeg,
            BatteryPct = message.BatteryPct,
            Status = message.Status,
            Timestamp = message.Timestamp
        };

        _buggies[buggy.DeviceId] = buggy;
    }

    public IReadOnlyCollection<BuggyDto> Snapshot()
    {
        return _buggies.Values
            .Select(buggy => new BuggyDto
            {
                StationId = buggy.StationId,
                DeviceId = buggy.DeviceId,
                Latitude = buggy.Latitude,
                Longitude = buggy.Longitude,
                SpeedKph = buggy.SpeedKph,
                Sats = buggy.Sats,
                HeadingDeg = buggy.HeadingDeg,
                BatteryPct = buggy.BatteryPct,
                Status = buggy.Status,
                Timestamp = buggy.Timestamp
            })
            .ToList();
    }

    public void Clear() => _buggies.Clear();
}
