namespace Chisel.Infrastructure.Data;

// The one saved clock-tools row (Id is always 1). Times are ticks and timestamps; laps and slots are JSON.
public class ClockToolsEntity
{
    public int Id { get; set; } = 1;
    // The slots of the clock widget as a JSON list; empty or unreadable means the default slots.
    public string SlotsJson { get; set; } = string.Empty;
    public long TimerDurationTicks { get; set; }
    public long TimerBankedTicks { get; set; }
    public DateTimeOffset? TimerStartedAt { get; set; }
    public long StopwatchBankedTicks { get; set; }
    public DateTimeOffset? StopwatchStartedAt { get; set; }
    public string StopwatchLapsJson { get; set; } = "[]";
}
