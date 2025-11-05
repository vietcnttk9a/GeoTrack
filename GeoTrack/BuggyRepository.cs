using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GeoTrack.Models;

namespace GeoTrack;

public sealed class BuggyRepository
{
    private readonly ConcurrentDictionary<string, BuggyState> _buggies =
        new(StringComparer.OrdinalIgnoreCase);

    // Cửa sổ thời gian 10 giây để lọc nhiễu
    private static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(10);

    public void Update(string stationId, GeoMessageDto message)
    {
        if (string.IsNullOrWhiteSpace(stationId))
        {
            stationId = message.DeviceId;
        }

        var deviceId = string.IsNullOrWhiteSpace(message.DeviceId)
            ? stationId
            : message.DeviceId;

        var state = _buggies.GetOrAdd(deviceId, _ => new BuggyState());
        state.AddSample(stationId, message, WindowSize);
    }

    public IReadOnlyCollection<BuggyDto> Snapshot()
    {
        return _buggies.Values
            .Select(state => state.Filtered)
            .Where(dto => dto != null)
            .Cast<BuggyDto>()
            .ToList();
    }

    public void Clear()
    {
        _buggies.Clear();
    }
}