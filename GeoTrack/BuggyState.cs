using System;
using System.Collections.Generic;
using GeoTrack.Models;

namespace GeoTrack;

internal sealed class BuggyState
{
    private readonly object _syncRoot = new();
    private readonly List<GeoSample> _samples = new();

    /// <summary>
    /// Vị trí đã qua lọc nhiễu hiện tại của buggy này.
    /// </summary>
    public BuggyDto? Filtered { get; private set; }

    /// <summary>
    /// Thêm một sample mới từ 1 trạm phát cho buggy này và cập nhật Filtered.
    /// </summary>
    public void AddSample(string stationId, GeoMessageDto message, TimeSpan windowSize)
    {
        lock (_syncRoot)
        {
            var nowUtc = DateTime.UtcNow;

            // Nếu deviceId trống thì fallback về stationId
            if (string.IsNullOrWhiteSpace(message.DeviceId))
            {
                message.DeviceId = stationId;
            }

            _samples.Add(new GeoSample(stationId, message));

            // Xoá các sample quá cũ khỏi cửa sổ thời gian
            var threshold = nowUtc - windowSize;
            _samples.RemoveAll(s => s.Message.Timestamp < threshold);

            var best = BuggyNoiseFilter.SelectBestSample(_samples, Filtered);
            if (best == null)
            {
                return;
            }

            Filtered = BuggyNoiseFilter.MapToBuggyDto(best, Filtered);
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _samples.Clear();
            Filtered = null;
        }
    }
}

/// <summary>
/// Một sample từ 1 trạm phát cho 1 buggy tại 1 thời điểm.
/// </summary>
internal sealed record GeoSample(string StationId, GeoMessageDto Message);