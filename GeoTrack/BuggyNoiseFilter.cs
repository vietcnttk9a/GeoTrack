using System;
using System.Collections.Generic;
using System.Linq;
using GeoTrack.Models;

namespace GeoTrack;

internal static class BuggyNoiseFilter
{
    // Ngưỡng tốc độ rất cao (≈ 180 km/h) để loại bỏ outlier "teleport"
    private const double MaxReasonableSpeedMetersPerSecond = 50.0;
    private const double EarthRadiusMeters = 6_371_000.0;

    /// <summary>
    /// Chọn sample "tốt" nhất trong cửa sổ hiện tại, có xét outlier so với vị trí trước đó.
    /// </summary>
    public static GeoSample? SelectBestSample(
        IReadOnlyList<GeoSample> window,
        BuggyDto? lastFiltered)
    {
        if (window == null || window.Count == 0)
        {
            return null;
        }

        // 1. Ưu tiên Sats cao hơn, sau đó giờ mới hơn
        var ordered = window
            .OrderByDescending(s => s.Message.Sats)
            .ThenByDescending(s => s.Message.Timestamp)
            .ToList();

        var best = ordered[0];

        if (lastFiltered == null)
        {
            return best;
        }

        // 2. Loại outlier dựa trên tốc độ ước tính giữa lastFiltered và sample mới
        var dtSeconds = (best.Message.Timestamp - lastFiltered.Timestamp).TotalSeconds;
        if (dtSeconds > 0)
        {
            var distance = GetDistanceMeters(
                lastFiltered.Latitude,
                lastFiltered.Longitude,
                best.Message.Latitude,
                best.Message.Longitude);

            var speedMps = distance / dtSeconds;

            if (speedMps > MaxReasonableSpeedMetersPerSecond)
            {
                var nonOutlier = ordered
                    .Skip(1)
                    .FirstOrDefault(s =>
                    {
                        var dt = (s.Message.Timestamp - lastFiltered.Timestamp).TotalSeconds;
                        if (dt <= 0)
                        {
                            return false;
                        }

                        var d = GetDistanceMeters(
                            lastFiltered.Latitude,
                            lastFiltered.Longitude,
                            s.Message.Latitude,
                            s.Message.Longitude);

                        var v = d / dt;
                        return v <= MaxReasonableSpeedMetersPerSecond;
                    });

                if (nonOutlier != null)
                {
                    best = nonOutlier;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Map từ sample đã chọn sang BuggyDto để gửi ra ngoài.
    /// </summary>
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
            // hiện tại bạn chưa compute status phức tạp, nên giữ lại status cũ nếu có
            Status = previous?.Status ?? string.Empty,
            Timestamp = msg.Timestamp
            // SpeedKph / HeadingDeg / BatteryPct vẫn tồn tại trong BuggyDto
            // nhưng không set => sẽ là 0, ExternalApp có thể bỏ qua hoặc tự tính.
        };
    }

    /// <summary>
    /// Tính khoảng cách theo Haversine (mét).
    /// </summary>
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
