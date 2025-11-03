using System;
using System.Collections.Concurrent;
using System.Linq;
using GeoTrack.Domain;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class BuggyRepository
{
    private readonly ConcurrentDictionary<string, BuggyDto> _buggies = new(StringComparer.OrdinalIgnoreCase);

    public void Update(string stationId, GeoMessageDto message, DeviceStatus status)
    {
        if (string.IsNullOrWhiteSpace(stationId))
        {
            stationId = message.Id;
        }

        var buggy = new BuggyDto
        {
            StationId = stationId,
            Id = string.IsNullOrWhiteSpace(message.Id) ? stationId : message.Id,
            Lat = message.Lat,
            Lng = message.Lng,
            Sats = message.Sats,
            Status = status.ToString(),
            Datetime = message.Datetime
        };

        _buggies[buggy.Id] = buggy;
    }

    public IReadOnlyCollection<BuggyDto> Snapshot()
    {
        return _buggies.Values
            .Select(buggy => new BuggyDto
            {
                StationId = buggy.StationId,
                Id = buggy.Id,
                Lat = buggy.Lat,
                Lng = buggy.Lng,
                Sats = buggy.Sats,
                Status = buggy.Status,
                Datetime = buggy.Datetime
            })
            .ToList();
    }

    public void Clear() => _buggies.Clear();
}
