namespace Chisel.Core.Schedule;

public enum MarkerKind
{
    Checkpoint,
    Reminder
}

public class Marker
{
    public Marker(string label, TimeOnly time, MarkerKind kind)
    {
        Label = label;
        Time = time;
        Kind = kind;
    }

    public string Label { get; }
    public TimeOnly Time { get; }
    public MarkerKind Kind { get; }

    public bool HasPassed(TimeOnly now) => now >= Time;
}