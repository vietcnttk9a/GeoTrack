using System;
using System.Collections.Generic;
using System.Linq;
using GeoTrack.Config;

namespace GeoTrack.Domain;

public sealed class GeoTrackService
{
    private readonly IDeviceWindowStore _store;
    private readonly TrackingOptions _options;
    private readonly Action<string>? _infoLogger;
    private readonly Action<string>? _warningLogger;

    public GeoTrackService(
        IDeviceWindowStore store,
        TrackingOptions options,
        Action<string>? infoLogger = null,
        Action<string>? warningLogger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _infoLogger = infoLogger;
        _warningLogger = warningLogger;
    }

    public DeviceStatus OnPosition(string deviceId, Position position)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        }

        var window = _store.Get(deviceId);
        lock (window)
        {
            if (window.Positions.Count > 0)
            {
                var last = window.Positions.Last!.Value;
                var seconds = (position.Timestamp - last.Timestamp).TotalSeconds;
                if (seconds > 0)
                {
                    var distance = HaversineMeters(last, position);
                    var speed = distance / seconds;
                    if (speed > _options.OutlierJumpMeters)
                    {
                        _warningLogger?.Invoke(
                            $"Dropped outlier for {deviceId}: {distance:F1}m jump over {seconds:F1}s (speed {speed:F2} m/s)");
                        return window.CurrentStatus;
                    }
                }
            }

            window.Positions.AddLast(position);
            Trim(window);
            _infoLogger?.Invoke($"Device {deviceId} window size: {window.Positions.Count}");

            if (window.Positions.Count < _options.MinPoints)
            {
                return window.CurrentStatus;
            }

            var metrics = ComputeMetrics(window.Positions);
            var candidate = Decide(metrics.maxPairDistance, metrics.averageSpeed);

            if (candidate != window.CurrentStatus)
            {
                IncrementCounter(window, candidate);
                if (window.ConfirmCounters[candidate] >= _options.ConfirmCount)
                {
                    var previous = window.CurrentStatus;
                    window.CurrentStatus = candidate;
                    ResetCounters(window);
                    _infoLogger?.Invoke(
                        $"Device {deviceId} status {previous} -> {candidate} (avg {metrics.averageSpeed:F2} m/s, span {metrics.spanSeconds:F1}s, max {metrics.maxPairDistance:F1} m)");
                }
            }
            else
            {
                ResetCounters(window);
            }

            return window.CurrentStatus;
        }
    }

    private void Trim(DeviceWindow window)
    {
        if (window.Positions.Count == 0)
        {
            return;
        }

        var threshold = window.Positions.Last!.Value.Timestamp - TimeSpan.FromSeconds(_options.WindowSeconds);
        while (window.Positions.First != null && window.Positions.First.Value.Timestamp < threshold)
        {
            window.Positions.RemoveFirst();
        }
    }

    private static (double maxPairDistance, double averageSpeed, double spanSeconds) ComputeMetrics(IEnumerable<Position> positions)
    {
        var list = positions as IList<Position> ?? positions.ToList();
        if (list.Count < 2)
        {
            return (0d, 0d, 0d);
        }

        var totalDistance = 0d;
        for (var i = 1; i < list.Count; i++)
        {
            totalDistance += HaversineMeters(list[i - 1], list[i]);
        }

        var maxPair = 0d;
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var distance = HaversineMeters(list[i], list[j]);
                if (distance > maxPair)
                {
                    maxPair = distance;
                }
            }
        }

        var spanSeconds = Math.Max(1.0, (list[^1].Timestamp - list[0].Timestamp).TotalSeconds);
        var averageSpeed = totalDistance / spanSeconds;
        return (maxPair, averageSpeed, spanSeconds);
    }

    private DeviceStatus Decide(double maxPairDistance, double averageSpeed)
    {
        if (maxPairDistance <= _options.MaxDistanceStationaryMeters &&
            averageSpeed <= _options.SpeedThresholdStationaryMps)
        {
            return DeviceStatus.Stationary;
        }

        if (averageSpeed >= _options.SpeedThresholdMovingMps)
        {
            return DeviceStatus.Moving;
        }

        return DeviceStatus.Idle;
    }

    private static void IncrementCounter(DeviceWindow window, DeviceStatus candidate)
    {
        foreach (var key in window.ConfirmCounters.Keys.ToList())
        {
            if (key == candidate)
            {
                window.ConfirmCounters[key] = window.ConfirmCounters[key] + 1;
            }
            else
            {
                window.ConfirmCounters[key] = 0;
            }
        }
    }

    private static void ResetCounters(DeviceWindow window)
    {
        foreach (var key in window.ConfirmCounters.Keys.ToList())
        {
            window.ConfirmCounters[key] = 0;
        }
    }

    private static double HaversineMeters(Position a, Position b)
    {
        const double earthRadius = 6371000d;
        double ToRadians(double angle) => angle * Math.PI / 180d;

        var dLat = ToRadians(b.Lat - a.Lat);
        var dLon = ToRadians(b.Lng - a.Lng);
        var lat1 = ToRadians(a.Lat);
        var lat2 = ToRadians(b.Lat);

        var sinLat = Math.Sin(dLat / 2d);
        var sinLon = Math.Sin(dLon / 2d);
        var h = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;
        var c = 2d * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1d - h));
        return earthRadius * c;
    }
}
