using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.Core.Tests.Clock;

public class StopwatchStateTests
{
    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_IsIdleAtZero()
    {
        var s = StopwatchState.Idle;
        Assert.False(s.IsRunning);
        Assert.Equal(TimeSpan.Zero, s.Elapsed(T0));
    }

    [Fact]
    public void Running_CountsFromTheStartTimestamp()
    {
        var s = StopwatchState.Idle.Start(T0);
        Assert.True(s.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(90), s.Elapsed(T0.AddSeconds(90)));
    }

    [Fact]
    public void Pause_FreezesTheTime_AndResumeContinuesFromIt()
    {
        var s = StopwatchState.Idle.Start(T0).Pause(T0.AddSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(10), s.Elapsed(T0.AddMinutes(5)));

        s = s.Start(T0.AddMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(25), s.Elapsed(T0.AddMinutes(5).AddSeconds(15)));
    }

    [Fact]
    public void Starting_WhileRunning_ChangesNothing()
    {
        var s = StopwatchState.Idle.Start(T0);
        Assert.Equal(s, s.Start(T0.AddSeconds(5)));
    }

    [Fact]
    public void Laps_RecordTheTotalAndTheSplit_AndOnlyWhileRunning()
    {
        var s = StopwatchState.Idle.Lap(T0);
        Assert.Empty(s.Laps);

        s = StopwatchState.Idle.Start(T0).Lap(T0.AddSeconds(10)).Lap(T0.AddSeconds(25));
        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(25)], s.Laps);
        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)], s.Splits());
    }

    [Fact]
    public void Reset_ClearsTimeAndLaps()
    {
        var s = StopwatchState.Idle.Start(T0).Lap(T0.AddSeconds(3)).Reset();
        Assert.Equal(StopwatchState.Idle, s);
    }

    [Fact]
    public void AClockMovedBackwards_NeverShowsNegativeTime()
    {
        var s = StopwatchState.Idle.Start(T0);
        Assert.Equal(TimeSpan.Zero, s.Elapsed(T0.AddMinutes(-5)));
    }

    [Fact]
    public void Works_WithAFakeTimeProvider_AcrossASleep()
    {
        var time = new FakeTimeProvider(T0);
        var s = StopwatchState.Idle.Start(time.GetUtcNow());
        time.Advance(TimeSpan.FromHours(2)); // the machine slept; the timestamps still tell the truth
        Assert.Equal(TimeSpan.FromHours(2), s.Elapsed(time.GetUtcNow()));
    }
}
