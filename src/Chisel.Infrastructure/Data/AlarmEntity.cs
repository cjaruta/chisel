namespace Chisel.Infrastructure.Data;

public class AlarmEntity
{
    public Guid Id { get; set; }
    public TimeOnly Time { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    // AlarmDays flags; 0 is a one-time alarm.
    public int Days { get; set; }
    public DateOnly? LastFiredOn { get; set; }
}
