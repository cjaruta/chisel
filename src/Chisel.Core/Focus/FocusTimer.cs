namespace Chisel.Core.Focus;

public enum FocusRun { Idle, Running, Paused }

// Everything about the timer that is saved. Elapsed is the time already spent in this phase before the current run began (all of
// it while paused or idle); RunStartedUtc is the wall-clock moment the current run began, so a running timer can carry on after a
// restart. FocusDoneInCycle counts the focus sessions since the last long break; SessionsToday counts those finished on SessionsDay.
public sealed record FocusTimerState(
    FocusPhase Phase, FocusRun Run, TimeSpan Elapsed, DateTimeOffset? RunStartedUtc,
    int FocusDoneInCycle, int SessionsToday, DateOnly SessionsDay)
{
    public static FocusTimerState Initial { get; } = new(FocusPhase.Focus, FocusRun.Idle, TimeSpan.Zero, null, 0, 0, default);
}

// A phase that ran out: which one, what comes next, how long ago it actually ended, and whether it counted as a focus session.
public readonly record struct FocusPhaseEnd(FocusPhase Ended, FocusPhase Next, TimeSpan Late, bool Counted);

// The Pomodoro cycle. Nothing here counts ticks: the time remaining is always worked out from timestamps, so it stays right however
// late or seldom Update is called. Within a run the monotonic clock is used (it ignores changes to the wall clock); if the wall
// clock has moved clearly further, the machine slept with a counter that stood still, and the wall clock's span is used instead.
// Across a restart only the wall clock is left, so a saved running timer resumes from RunStartedUtc.
public sealed class FocusTimer
{
    // A phase that ended longer ago than this (the PC slept, or the app was closed) does not start the next phase by itself.
    public static readonly TimeSpan AutoStartGrace = TimeSpan.FromMinutes(1);

    // A running timer saved longer ago than this is reset rather than resumed.
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(12);

    // How far the wall clock may run ahead of the monotonic one before it is taken as sleep.
    private static readonly TimeSpan SleepSlack = TimeSpan.FromSeconds(2);

    private readonly TimeProvider _time;
    private FocusTimerState _state;
    private long _runStart; // the monotonic timestamp the current run began at (only meaningful while running)

    public FocusTimer(TimeProvider time, FocusSettings settings, FocusTimerState? saved = null)
    {
        _time = time;
        Settings = settings;
        _state = saved ?? FocusTimerState.Initial;
        if (_state.Run == FocusRun.Running)
        {
            Restore();
        }
    }

    public FocusSettings Settings { get; set; }

    public FocusTimerState State => _state;

    public FocusPhase Phase => _state.Phase;

    public FocusRun Run => _state.Run;

    public int FocusDoneInCycle => _state.FocusDoneInCycle;

    public TimeSpan Duration => Settings.Duration(_state.Phase);

    public TimeSpan Elapsed
    {
        get
        {
            var elapsed = RawElapsed();
            return elapsed < Duration ? elapsed : Duration;
        }
    }

    public TimeSpan Remaining => Duration - Elapsed;

    // The focus sessions finished today (local time); none once the day has changed.
    public int SessionsToday => _state.SessionsDay == Today(_time.GetUtcNow()) ? _state.SessionsToday : 0;

    public void Start()
    {
        if (_state.Run == FocusRun.Idle)
        {
            Begin(TimeSpan.Zero);
        }
        else if (_state.Run == FocusRun.Paused)
        {
            Resume();
        }
    }

    public void Pause()
    {
        if (_state.Run == FocusRun.Running)
        {
            _state = _state with { Run = FocusRun.Paused, Elapsed = Elapsed, RunStartedUtc = null };
        }
    }

    public void Resume()
    {
        if (_state.Run == FocusRun.Paused)
        {
            Begin(TimeSpan.Zero);
        }
    }

    // On to the next phase without counting this one; it waits to be started.
    public void Skip()
    {
        var done = _state.Phase == FocusPhase.LongBreak ? 0 : _state.FocusDoneInCycle;
        _state = _state with { Phase = Next(_state.Phase, done), Run = FocusRun.Idle, Elapsed = TimeSpan.Zero, RunStartedUtc = null, FocusDoneInCycle = done };
    }

    // Back to the start of the cycle: the first focus session, not running. Today's count is kept.
    public void Reset() => _state = _state with
    {
        Phase = FocusPhase.Focus, Run = FocusRun.Idle, Elapsed = TimeSpan.Zero, RunStartedUtc = null, FocusDoneInCycle = 0,
    };

    // Ends the phase if its time is up (counting a focus session on the day it ended) and moves to the next one, which starts by
    // itself only when auto-start is on and the end was just now. Null when the phase is still going.
    public FocusPhaseEnd? Update()
    {
        if (_state.Run != FocusRun.Running)
        {
            return null;
        }
        var raw = RawElapsed();
        var duration = Duration;
        if (raw < duration)
        {
            return null;
        }
        var late = raw - duration;
        var now = _time.GetUtcNow();
        var ended = _state.Phase;
        var counted = ended == FocusPhase.Focus;
        var done = ended == FocusPhase.LongBreak ? 0 : _state.FocusDoneInCycle + (counted ? 1 : 0);
        var next = Next(ended, done);
        _state = _state with { Phase = next, Run = FocusRun.Idle, Elapsed = TimeSpan.Zero, RunStartedUtc = null, FocusDoneInCycle = done };
        if (counted)
        {
            var day = Today(now - late);
            _state = _state with { SessionsToday = (_state.SessionsDay == day ? _state.SessionsToday : 0) + 1, SessionsDay = day };
        }
        if (Settings.AutoStartNext && late < AutoStartGrace)
        {
            Begin(late);
        }
        return new FocusPhaseEnd(ended, next, late, counted);
    }

    private FocusPhase Next(FocusPhase phase, int doneInCycle) => phase switch
    {
        FocusPhase.Focus => doneInCycle >= Settings.SessionsBeforeLongBreak ? FocusPhase.LongBreak : FocusPhase.ShortBreak,
        _ => FocusPhase.Focus,
    };

    // Starts running now, as though it had started `ago` earlier.
    private void Begin(TimeSpan ago)
    {
        _runStart = _time.GetTimestamp() - Ticks(ago);
        _state = _state with { Run = FocusRun.Running, RunStartedUtc = _time.GetUtcNow() - ago };
    }

    private void Restore()
    {
        var away = _state.RunStartedUtc is { } at ? _time.GetUtcNow() - at : TimeSpan.Zero;
        if (away > StaleAfter)
        {
            Reset();
            return;
        }
        _runStart = _time.GetTimestamp() - Ticks(away > TimeSpan.Zero ? away : TimeSpan.Zero);
    }

    private TimeSpan RawElapsed()
    {
        if (_state.Run != FocusRun.Running)
        {
            return _state.Elapsed;
        }
        var run = _time.GetElapsedTime(_runStart);
        if (_state.RunStartedUtc is { } at && _time.GetUtcNow() - at is var wall && wall > run + SleepSlack)
        {
            run = wall;
        }
        return _state.Elapsed + run;
    }

    private long Ticks(TimeSpan span) => (long)(span.TotalSeconds * _time.TimestampFrequency);

    private DateOnly Today(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _time.LocalTimeZone).DateTime);
}
