using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GeoTrack.Domain;

public sealed class Position
{
    public double Lat { get; init; }
    public double Lng { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class DeviceWindow
{
    public LinkedList<Position> Positions { get; } = new();

    public DeviceStatus CurrentStatus { get; set; } = DeviceStatus.Unknown;

    public Dictionary<DeviceStatus, int> ConfirmCounters { get; } = new()
    {
        { DeviceStatus.Stationary, 0 },
        { DeviceStatus.Moving, 0 },
        { DeviceStatus.Idle, 0 }
    };
}

public interface IDeviceWindowStore
{
    DeviceWindow Get(string deviceId);
}

public sealed class InMemoryDeviceWindowStore : IDeviceWindowStore
{
    private readonly ConcurrentDictionary<string, DeviceWindow> _store = new(StringComparer.OrdinalIgnoreCase);

    public DeviceWindow Get(string deviceId)
    {
        return _store.GetOrAdd(deviceId, _ => new DeviceWindow());
    }
}
