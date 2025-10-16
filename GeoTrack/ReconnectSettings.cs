using System;

namespace GeoTrack;

public sealed record ReconnectSettings(TimeSpan InitialDelay, TimeSpan MaxDelay, bool UseExponentialBackoff)
{
    public TimeSpan EnsureValidDelay(TimeSpan? current)
    {
        var delay = current ?? InitialDelay;
        if (delay <= TimeSpan.Zero)
        {
            delay = InitialDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : InitialDelay;
        }

        if (MaxDelay > TimeSpan.Zero && delay > MaxDelay)
        {
            delay = MaxDelay;
        }

        return delay;
    }

    public TimeSpan NextDelay(TimeSpan previous)
    {
        if (!UseExponentialBackoff)
        {
            return EnsureValidDelay(previous);
        }

        var next = previous <= TimeSpan.Zero ? InitialDelay : TimeSpan.FromMilliseconds(previous.TotalMilliseconds * 2);
        return EnsureValidDelay(next);
    }
}
