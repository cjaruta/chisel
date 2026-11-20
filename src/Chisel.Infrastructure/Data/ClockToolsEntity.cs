namespace Chisel.Infrastructure.Data;

// The one saved clock-tools row (Id is always 1). Times are ticks and timestamps; laps are a JSON list of ticks.
public class ClockToolsEntity
{
    public int Id { get; set; } = 1;
    public bool AlarmEnabled { get; set; } = true;
    public bool TimerEnabled { get; set; } = true;
    public bool StopwatchEnabled { get; set; } = true;
    public int Strip { get; set; }
    public int Mode { get; set; }
    public long TimerDurationTicks { get; set; }
    public long TimerBankedTicks { get; set; }
    public DateTimeOffset? TimerStartedAt { get; set; }
    public long StopwatchBankedTicks { get; set; }
    public DateTimeOffset? StopwatchStartedAt { get; set; }
    public string StopwatchLapsJson { get; set; } = "[]";
}
