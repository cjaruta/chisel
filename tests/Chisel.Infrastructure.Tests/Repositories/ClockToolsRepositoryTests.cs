using Chisel.Core.Clock;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ClockToolsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();
    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Empty_ReturnsTheDefaults()
    {
        using var context = _fixture.CreateContext();
        var state = new ClockToolsRepository(context).Get();
        Assert.Equal(ClockToolsConfig.Default, state.Config);
        Assert.Equal(TimerState.Idle, state.Timer);
        Assert.Equal(StopwatchState.Idle, state.Stopwatch);
        Assert.Equal(ClockMode.Clock, state.Mode);
    }

    [Fact]
    public void Save_RoundTripsTheConfigAndARunningTimerAndStopwatch_ReplacingTheOldRow()
    {
        var state = new ClockToolsState(
            new ClockToolsConfig(AlarmEnabled: false, TimerEnabled: true, StopwatchEnabled: false, StripPlacement.Bottom),
            TimerState.Idle.WithDuration(TimeSpan.FromMinutes(5)).Start(T0).Pause(T0.AddMinutes(1)).Start(T0.AddMinutes(3)),
            StopwatchState.Idle.Start(T0).Lap(T0.AddSeconds(10)).Lap(T0.AddSeconds(25)),
            ClockMode.Timer);
        using (var context = _fixture.CreateContext())
        {
            var repository = new ClockToolsRepository(context);
            repository.Save(new ClockToolsState(ClockToolsConfig.Default, TimerState.Idle, StopwatchState.Idle, ClockMode.Clock));
            repository.Save(state);
        }

        using var again = _fixture.CreateContext();
        var loaded = new ClockToolsRepository(again).Get();
        Assert.Equal(state.Config, loaded.Config);
        Assert.Equal(state.Mode, loaded.Mode);
        Assert.Equal(state.Timer, loaded.Timer);
        Assert.Equal(state.Stopwatch.Banked, loaded.Stopwatch.Banked);
        Assert.Equal(state.Stopwatch.StartedAt, loaded.Stopwatch.StartedAt);
        Assert.Equal(state.Stopwatch.Laps, loaded.Stopwatch.Laps);
        Assert.Single(again.ClockTools);
    }

    public void Dispose() => _fixture.Dispose();
}
