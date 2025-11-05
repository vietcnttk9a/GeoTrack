using System;
using System.Collections.Generic;
using System.Linq;
using GeoTrack.Models;

namespace GeoTrack;

internal sealed class BuggyState
{
    private readonly object _syncRoot = new();
    private readonly List<GeoSample> _samples = new();

    private const string IdleStatus = "Idle";
    private const string MovingStatus = "Moving";
    private const double MovementThresholdMeters = 10.0;

    private DateTime _lastMovementTimestampUtc;
    private bool _hasMovementTimestamp;

    /// <summary>
    /// Vị trí đã qua lọc nhiễu hiện tại của buggy này.
    /// </summary>
    public BuggyDto? Filtered { get; private set; }

    /// <summary>
    /// Thêm một sample mới từ 1 trạm phát cho buggy này và cập nhật Filtered.
    /// </summary>
    public BuggyDto? AddSample(string stationId, GeoMessageDto message, TimeSpan windowSize)
    {
        lock (_syncRoot)
        {
            // Nếu deviceId trống thì fallback về stationId
            if (string.IsNullOrWhiteSpace(message.DeviceId))
            {
                message.DeviceId = stationId;
            }

            var sample = new GeoSample(stationId, message);
            _samples.Add(sample);

            var newestTimestamp = _samples.Max(s => s.Message.Timestamp);

            // Xoá các sample quá cũ khỏi cửa sổ thời gian
            var threshold = newestTimestamp - windowSize;
            _samples.RemoveAll(s => s.Message.Timestamp < threshold);

            var best = BuggyNoiseFilter.SelectBestSample(_samples, Filtered);
            if (best == null)
            {
                return Filtered;
            }

            newestTimestamp = _samples.Max(s => s.Message.Timestamp);
            var totalDistance = CalculateTotalDistanceMeters();
            var status = totalDistance >= MovementThresholdMeters ? MovingStatus : IdleStatus;
            var idleDurationSeconds = 0;

            if (status == MovingStatus)
            {
                _lastMovementTimestampUtc = newestTimestamp;
                _hasMovementTimestamp = true;
            }
            else
            {
                if (!_hasMovementTimestamp)
                {
                    _lastMovementTimestampUtc = newestTimestamp;
                    _hasMovementTimestamp = true;
                }

                var idleSpan = newestTimestamp - _lastMovementTimestampUtc;
                if (idleSpan > TimeSpan.Zero)
                {
                    idleDurationSeconds = (int)Math.Round(idleSpan.TotalSeconds);
                }
            }

            var dto = BuggyNoiseFilter.MapToBuggyDto(best, Filtered);
            dto.Status = status;
            dto.IdleDurationSeconds = idleDurationSeconds;

            Filtered = dto;
            return Filtered;
        }
    }

    private double CalculateTotalDistanceMeters()
    {
        if (_samples.Count < 2)
        {
            return 0;
        }

        var ordered = _samples
            .OrderBy(s => s.Message.Timestamp)
            .ToList();

        double total = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1];
            var current = ordered[i];
            total += BuggyNoiseFilter.GetDistanceMeters(
                previous.Message.Latitude,
                previous.Message.Longitude,
                current.Message.Latitude,
                current.Message.Longitude);
        }

        return total;
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _samples.Clear();
            Filtered = null;
            _hasMovementTimestamp = false;
            _lastMovementTimestampUtc = default;
        }
    }
}

/// <summary>
/// Một sample từ 1 trạm phát cho 1 buggy tại 1 thời điểm.
/// </summary>
internal sealed record GeoSample(string StationId, GeoMessageDto Message);
