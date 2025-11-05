using System;
using System.Collections.Generic;
using System.Linq;
using GeoTrack.Models;

namespace GeoTrack;

internal static class BuggyNoiseFilter
{
    private const double MaxReasonableSpeedMetersPerSecond = 50.0;
    private const double EarthRadiusMeters = 6_371_000.0;

    public static GeoSample? SelectBestSample(IReadOnlyList<GeoSample> window, BuggyDto? lastFiltered)
    {
        if (window == null || window.Count == 0)
        {
            return null;
        }

        var ordered = window
            .OrderByDescending(s => s.Message.Sats)
            .ThenByDescending(s => s.Message.Timestamp)
            .ToList();

        foreach (var candidate in ordered)
        {
            if (!IsOutlier(candidate, lastFiltered))
            {
                return candidate;
            }
        }

        return ordered[0];
    }

    public static BuggyDto MapToBuggyDto(GeoSample sample, BuggyDto? previous)
    {
        var msg = sample.Message;

        return new BuggyDto
        {
            StationId = sample.StationId,
            DeviceId = msg.DeviceId,
            Latitude = msg.Latitude,
            Longitude = msg.Longitude,
            Sats = msg.Sats,
            Status = previous?.Status ?? string.Empty,
            IdleDurationSeconds = previous?.IdleDurationSeconds ?? 0,
            Timestamp = msg.Timestamp
        };
    }

    private static bool IsOutlier(GeoSample candidate, BuggyDto? lastFiltered)
    {
        if (lastFiltered == null)
        {
            return false;
        }

        var dtSeconds = (candidate.Message.Timestamp - lastFiltered.Timestamp).TotalSeconds;
        if (dtSeconds <= 0)
        {
            return false;
        }

        var distance = GetDistanceMeters(
            lastFiltered.Latitude,
            lastFiltered.Longitude,
            candidate.Message.Latitude,
            candidate.Message.Longitude);

        var speedMps = distance / dtSeconds;
        return speedMps > MaxReasonableSpeedMetersPerSecond;
    }

    public static double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var lat1Rad = DegreesToRadians(lat1);
        var lon1Rad = DegreesToRadians(lon1);
        var lat2Rad = DegreesToRadians(lat2);
        var lon2Rad = DegreesToRadians(lon2);

        var dLat = lat2Rad - lat1Rad;
        var dLon = lon2Rad - lon1Rad;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = EarthRadiusMeters * c;

        return distance;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
