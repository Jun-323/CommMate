namespace CommMate.Models;

public enum DataDirection
{
    Sent,
    Received,
    System
}

public class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public DataDirection Direction { get; init; }
    public string Data { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public override string ToString()
    {
        var dir = Direction switch
        {
            DataDirection.Sent => "TX",
            DataDirection.Received => "RX",
            _ => "--"
        };
        return $"[{FormattedTimestamp}] [{dir}] {Data}";
    }
}
