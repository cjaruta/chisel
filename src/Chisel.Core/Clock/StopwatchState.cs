namespace Chisel.Core.Clock;

// A stopwatch as plain data. It keeps the time banked before the current run and the timestamp the run started, so the
// elapsed time is always banked + (now - started): it survives a restart or a sleep, and nothing ticks in the model.
public sealed record StopwatchState(TimeSpan Banked, DateTimeOffset? StartedAt, IReadOnlyList<TimeSpan> Laps)
{
    public static StopwatchState Idle { get; } = new(TimeSpan.Zero, null, []);

    public bool IsRunning => StartedAt is not null;

    public TimeSpan Elapsed(DateTimeOffset now) =>
        StartedAt is { } started ? Banked + Max(now - started, TimeSpan.Zero) : Banked;

    public StopwatchState Start(DateTimeOffset now) => IsRunning ? this : this with { StartedAt = now };

    public StopwatchState Pause(DateTimeOffset now) => IsRunning ? this with { Banked = Elapsed(now), StartedAt = null } : this;

    // Records the total elapsed time so far; only while running.
    public StopwatchState Lap(DateTimeOffset now) => IsRunning ? this with { Laps = [.. Laps, Elapsed(now)] } : this;

    public StopwatchState Reset() => Idle;

    // The time of each lap on its own (each total minus the one before).
    public IReadOnlyList<TimeSpan> Splits() => Laps.Select((total, i) => i == 0 ? total : total - Laps[i - 1]).ToList();

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
