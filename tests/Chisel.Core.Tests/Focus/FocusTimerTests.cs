using Chisel.Core.Focus;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.Core.Tests.Focus;

public class FocusTimerTests
{
    // 2000-01-01 09:00 UTC; the clock's local zone is UTC, so local midnight is 00:00 UTC.
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Start);

    private FocusTimer Timer(FocusSettings? settings = null, FocusTimerState? saved = null) => new(_clock, settings ?? FocusSettings.Default, saved);

    private static TimeSpan Min(double m) => TimeSpan.FromMinutes(m);

    // Runs the current phase to its end and lets the timer notice.
    private FocusPhaseEnd Finish(FocusTimer t)
    {
        if (t.Run != FocusRun.Running)
        {
            t.Start();
        }
        _clock.Advance(t.Remaining);
        return Assert.IsType<FocusPhaseEnd>(t.Update());
    }

    [Fact]
    public void Remaining_ComesFromTimestamps_NotFromCountingUpdates()
    {
        var t = Timer();
        Assert.Equal((FocusPhase.Focus, FocusRun.Idle, Min(25)), (t.Phase, t.Run, t.Remaining));

        t.Start();
        _clock.Advance(Min(6.5));

        Assert.Equal(Min(18.5), t.Remaining);
        Assert.Null(t.Update());
    }

    [Fact]
    public void PauseAndResume_KeepTheTimeExactly()
    {
        var t = Timer();
        t.Start();
        _clock.Advance(Min(5));
        t.Pause();
        _clock.Advance(TimeSpan.FromHours(3));

        Assert.Equal((FocusRun.Paused, Min(20)), (t.Run, t.Remaining));
        t.Resume();
        _clock.Advance(TimeSpan.FromSeconds(150.25));
        Assert.Equal(Min(20) - TimeSpan.FromSeconds(150.25), t.Remaining);
    }

    [Fact]
    public void AFocusSession_EndsInAShortBreak_AndTheNthInALongBreak_ThenTheCycleStartsAgain()
    {
        var t = Timer(FocusSettings.Default with { SessionsBeforeLongBreak = 3 });
        var phases = new List<FocusPhase>();
        for (var i = 0; i < 7; i++)
        {
            var end = Finish(t);
            phases.Add(end.Next);
            Assert.Equal(FocusRun.Idle, t.Run); // auto-start is off
        }

        Assert.Equal(new[]
        {
            FocusPhase.ShortBreak, FocusPhase.Focus, FocusPhase.ShortBreak, FocusPhase.Focus, FocusPhase.LongBreak, FocusPhase.Focus, FocusPhase.ShortBreak,
        }, phases);
        Assert.Equal(4, t.SessionsToday);
        Assert.Equal(1, t.FocusDoneInCycle);
    }

    [Fact]
    public void AutoStart_BeginsTheNextPhaseAtTheMomentThePreviousEnded()
    {
        var t = Timer(FocusSettings.Default with { AutoStartNext = true });
        t.Start();
        _clock.Advance(Min(25) + TimeSpan.FromSeconds(3));

        var end = t.Update();

        Assert.Equal(new FocusPhaseEnd(FocusPhase.Focus, FocusPhase.ShortBreak, TimeSpan.FromSeconds(3), Counted: true), end);
        Assert.Equal((FocusRun.Running, Min(5) - TimeSpan.FromSeconds(3)), (t.Run, t.Remaining));
    }

    [Fact]
    public void ASleepGap_EndsThePhaseOnce_AndDoesNotAutoStartTheNextOne()
    {
        var t = Timer(FocusSettings.Default with { AutoStartNext = true });
        t.Start();
        _clock.Advance(Min(10));
        _clock.Advance(TimeSpan.FromHours(2)); // asleep: no updates at all

        var end = t.Update();

        Assert.Equal((FocusPhase.Focus, FocusPhase.ShortBreak, true), (end!.Value.Ended, end.Value.Next, end.Value.Counted));
        Assert.Equal(Min(105), end.Value.Late);
        Assert.Equal((FocusPhase.ShortBreak, FocusRun.Idle, Min(5)), (t.Phase, t.Run, t.Remaining));
        Assert.Null(t.Update());
    }

    [Fact]
    public void ASleepGap_StillCounts_WhenTheMonotonicClockStoodStill()
    {
        var clock = new SleepyClock(Start);
        var t = new FocusTimer(clock, FocusSettings.Default);
        t.Start();
        clock.Sleep(TimeSpan.FromHours(1)); // the wall clock moves, the timestamp does not

        Assert.Equal(TimeSpan.Zero, t.Remaining);
        Assert.Equal(FocusPhase.ShortBreak, t.Update()!.Value.Next);
    }

    [Fact]
    public void AWallClockSetBack_DoesNotAddOrRemoveTime()
    {
        var clock = new SleepyClock(Start);
        var t = new FocusTimer(clock, FocusSettings.Default);
        t.Start();
        clock.Run(Min(4));
        clock.SetWall(Start - TimeSpan.FromHours(5));

        Assert.Equal(Min(21), t.Remaining);
    }

    [Fact]
    public void Skip_MovesOnWithoutCounting_AndResetGoesBackToTheFirstFocus()
    {
        var t = Timer();
        Finish(t); // focus done: 1 today, now a short break
        t.Start();
        _clock.Advance(Min(1));

        t.Skip();
        Assert.Equal((FocusPhase.Focus, FocusRun.Idle, Min(25)), (t.Phase, t.Run, t.Remaining));
        t.Skip();
        Assert.Equal(FocusPhase.ShortBreak, t.Phase);
        Assert.Equal(1, t.SessionsToday);

        t.Start();
        _clock.Advance(Min(2));
        t.Reset();
        Assert.Equal((FocusPhase.Focus, FocusRun.Idle, Min(25), 0, 1), (t.Phase, t.Run, t.Remaining, t.FocusDoneInCycle, t.SessionsToday));
    }

    [Fact]
    public void SessionsToday_StartOverAtLocalMidnight()
    {
        _clock.SetUtcNow(new DateTimeOffset(2000, 1, 1, 23, 20, 0, TimeSpan.Zero));
        var t = Timer();
        Finish(t);
        Assert.Equal(1, t.SessionsToday);

        _clock.Advance(Min(20)); // 00:05 on Jan 2
        Assert.Equal(0, t.SessionsToday);

        t.Skip();
        Finish(t);
        Assert.Equal(1, t.SessionsToday);
        Assert.Equal(new DateOnly(2000, 1, 2), t.State.SessionsDay);
    }

    [Fact]
    public void ARunningTimer_ResumesFromItsSavedState_ByTheWallClock()
    {
        var t = Timer();
        t.Start();
        _clock.Advance(Min(5));
        var saved = t.State;

        _clock.Advance(Min(4)); // the app was closed for four minutes
        var restarted = Timer(saved: saved);

        Assert.Equal((FocusRun.Running, Min(16)), (restarted.Run, restarted.Remaining));
        _clock.Advance(Min(1));
        Assert.Equal(Min(15), restarted.Remaining);
    }

    [Fact]
    public void APhaseThatEndedWhileClosed_IsCompletedOnTheFirstUpdate()
    {
        var t = Timer();
        t.Start();
        var saved = t.State;
        _clock.Advance(TimeSpan.FromHours(2));

        var restarted = Timer(saved: saved);
        var end = restarted.Update();

        Assert.Equal((FocusPhase.ShortBreak, FocusRun.Idle, 1), (restarted.Phase, restarted.Run, restarted.SessionsToday));
        Assert.True(end!.Value.Late > FocusTimer.AutoStartGrace);
    }

    [Fact]
    public void ATimerSavedTooLongAgo_IsResetCleanly()
    {
        var t = Timer();
        Finish(t);
        t.Start();
        var saved = t.State;
        _clock.Advance(FocusTimer.StaleAfter + Min(1));

        var restarted = Timer(saved: saved);

        Assert.Equal((FocusPhase.Focus, FocusRun.Idle, Min(25), 0), (restarted.Phase, restarted.Run, restarted.Remaining, restarted.FocusDoneInCycle));
        Assert.Null(restarted.Update());
    }

    [Fact]
    public void ShorteningTheFocusWhileRunning_EndsItIfItsTimeHasPassed()
    {
        var t = Timer();
        t.Start();
        _clock.Advance(Min(12));

        t.Settings = t.Settings with { FocusMinutes = 10 };

        Assert.Equal(TimeSpan.Zero, t.Remaining);
        Assert.Equal(FocusPhase.ShortBreak, t.Update()!.Value.Next);
    }

    // A clock whose timestamp can stand still while the wall clock moves (as an unbiased counter does in sleep), or move while
    // the wall clock is set back.
    private sealed class SleepyClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _wall = start;
        private long _ticks = 1_000_000;

        public override DateTimeOffset GetUtcNow() => _wall;
        public override long GetTimestamp() => _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Run(TimeSpan span)
        {
            _wall += span;
            _ticks += span.Ticks;
        }

        public void Sleep(TimeSpan span) => _wall += span;

        public void SetWall(DateTimeOffset wall) => _wall = wall;
    }
}
