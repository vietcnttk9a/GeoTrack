namespace GeoTrack.Config;

public sealed class TrackingOptions
{
    public double WindowSeconds { get; set; } = 10.0;

    public double MaxDistanceStationaryMeters { get; set; } = 10.0;

    public double SpeedThresholdMovingMps { get; set; } = 1.0;

    public double SpeedThresholdStationaryMps { get; set; } = 0.5;

    public int ConfirmCount { get; set; } = 2;

    public double OutlierJumpMeters { get; set; } = 200.0;

    public int MinPoints { get; set; } = 3;
}
