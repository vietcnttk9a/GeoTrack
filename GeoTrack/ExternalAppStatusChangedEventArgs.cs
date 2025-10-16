namespace GeoTrack;

public sealed class ExternalAppStatusChangedEventArgs : EventArgs
{
    public ExternalAppStatusChangedEventArgs(string status, DateTime timestamp)
    {
        Status = status;
        Timestamp = timestamp;
    }

    public string Status { get; }

    public DateTime Timestamp { get; }
}
