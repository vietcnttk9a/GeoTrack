namespace GeoTrack;

public sealed class LogMessageEventArgs : EventArgs
{
    public LogMessageEventArgs(string source, string message, DateTime timestamp)
    {
        Source = source;
        Message = message;
        Timestamp = timestamp;
    }

    public string Source { get; }

    public string Message { get; }

    public DateTime Timestamp { get; }
}
