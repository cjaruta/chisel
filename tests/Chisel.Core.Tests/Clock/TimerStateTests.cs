using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class TimerStateTests
{
    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Five = TimeSpan.FromMinutes(5);

    [Fact]
    public void Idle_ShowsTheFullDuration()
    {
        var t = TimerState.Idle.WithDuration(Five);
        Assert.Equal(TimerStatus.Idle, t.Status(T0));
        Assert.Equal(Five, t.Remaining(T0));
    }

    [Fact]
    public void Start_CountsDown_FromAbsoluteTime()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0);
        Assert.Equal(TimerStatus.Running, t.Status(T0.AddMinutes(1)));
        Assert.Equal(TimeSpan.FromMinutes(4), t.Remaining(T0.AddMinutes(1)));
    }

    [Fact]
    public void Pause_FreezesTheRemainingTime_AndResumeContinues()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0).Pause(T0.AddMinutes(2));
        Assert.Equal(TimerStatus.Paused, t.Status(T0.AddHours(1)));
        Assert.Equal(TimeSpan.FromMinutes(3), t.Remaining(T0.AddHours(1)));

        t = t.Start(T0.AddHours(1));
        Assert.Equal(TimeSpan.FromMinutes(2), t.Remaining(T0.AddHours(1).AddMinutes(1)));
    }

    [Fact]
    public void ReachingZero_IsFinished_AndStaysAtZero()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0);
        Assert.Equal(TimerStatus.Finished, t.Status(T0.AddMinutes(5)));
        Assert.Equal(TimeSpan.Zero, t.Remaining(T0.AddMinutes(60)));
        Assert.Equal(T0.AddMinutes(5), t.FinishesAt(T0));
    }

    [Fact]
    public void FinishesAt_AccountsForPausedTime()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0).Pause(T0.AddMinutes(1)).Start(T0.AddMinutes(10));
        Assert.Equal(T0.AddMinutes(14), t.FinishesAt(T0));
    }

    [Fact]
    public void FinishesAt_IsNullUnlessRunning()
    {
        Assert.Null(TimerState.Idle.WithDuration(Five).FinishesAt(T0));
        Assert.Null(TimerState.Idle.WithDuration(Five).Start(T0).Pause(T0.AddSeconds(1)).FinishesAt(T0));
    }

    [Fact]
    public void Reset_GoesBackToIdle_KeepingTheDuration()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0).Reset();
        Assert.Equal(TimerStatus.Idle, t.Status(T0.AddMinutes(1)));
        Assert.Equal(Five, t.Duration);
    }

    [Fact]
    public void Duration_CanOnlyChange_WhileIdle()
    {
        var running = TimerState.Idle.WithDuration(Five).Start(T0);
        Assert.Equal(Five, running.WithDuration(TimeSpan.FromMinutes(9)).Duration);
        Assert.Equal(TimeSpan.Zero, TimerState.Idle.WithDuration(TimeSpan.FromMinutes(-1)).Duration);
        Assert.Equal(TimerState.MaxDuration, TimerState.Idle.WithDuration(TimeSpan.FromDays(10)).Duration);
    }

    [Fact]
    public void ATimerWithNoDuration_CannotStart()
    {
        Assert.Equal(TimerStatus.Idle, TimerState.Idle.Start(T0).Status(T0));
    }

    [Fact]
    public void StartingAFinishedTimer_RestartsItFromTheFullDuration()
    {
        var t = TimerState.Idle.WithDuration(Five).Start(T0).Start(T0.AddMinutes(10));
        Assert.Equal(TimerStatus.Running, t.Status(T0.AddMinutes(10)));
        Assert.Equal(Five, t.Remaining(T0.AddMinutes(10)));
    }
}
