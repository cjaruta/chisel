namespace Chisel.Core.Clock;

public enum TimerStatus { Idle, Running, Paused, Finished }

// A countdown timer as plain data: a duration, the time already used before the current run, and the timestamp the run started.
// Remaining time is computed from absolute timestamps, so it is right after a restart or a sleep; reaching zero is a status the
// caller notices (and acts on once), not something the model does.
public sealed record TimerState(TimeSpan Duration, TimeSpan Banked, DateTimeOffset? StartedAt)
{
    public static readonly TimeSpan MaxDuration = new(99, 59, 59);

    public static TimerState Idle { get; } = new(TimeSpan.Zero, TimeSpan.Zero, null);

    public TimeSpan Elapsed(DateTimeOffset now)
    {
        var run = StartedAt is { } started && now > started ? now - started : TimeSpan.Zero;
        var total = Banked + run;
        return total > Duration ? Duration : total;
    }

    public TimeSpan Remaining(DateTimeOffset now) => Duration - Elapsed(now);

    public TimerStatus Status(DateTimeOffset now)
    {
        if (Duration <= TimeSpan.Zero || (StartedAt is null && Banked == TimeSpan.Zero))
        {
            return TimerStatus.Idle;
        }
        if (Elapsed(now) >= Duration)
        {
            return TimerStatus.Finished;
        }
        return StartedAt is null ? TimerStatus.Paused : TimerStatus.Running;
    }

    // The moment it will reach zero, while it is running.
    public DateTimeOffset? FinishesAt(DateTimeOffset now) =>
        Status(now) == TimerStatus.Running ? StartedAt + (Duration - Banked) : null;

    // The length can only change while the timer is idle. It is kept between zero and MaxDuration.
    public TimerState WithDuration(TimeSpan duration)
    {
        if (StartedAt is not null || Banked != TimeSpan.Zero)
        {
            return this;
        }
        return this with { Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration > MaxDuration ? MaxDuration : duration };
    }

    // Starts, resumes, or (when it already finished) starts again from the full duration. No-op while running or with no duration.
    public TimerState Start(DateTimeOffset now) => Status(now) switch
    {
        TimerStatus.Running => this,
        TimerStatus.Finished => this with { Banked = TimeSpan.Zero, StartedAt = now },
        _ when Duration > TimeSpan.Zero => this with { StartedAt = now },
        _ => this,
    };

    public TimerState Pause(DateTimeOffset now) =>
        Status(now) == TimerStatus.Running ? this with { Banked = Elapsed(now), StartedAt = null } : this;

    public TimerState Reset() => this with { Banked = TimeSpan.Zero, StartedAt = null };
}
