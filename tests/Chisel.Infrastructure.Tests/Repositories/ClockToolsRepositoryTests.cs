using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;
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
        Assert.Equal(ClockSlotsConfig.Default, state.Slots);
        Assert.Equal(TimerState.Idle, state.Timer);
        Assert.Equal(StopwatchState.Idle, state.Stopwatch);
    }

    [Fact]
    public void Save_RoundTripsTheSlotsAndARunningTimerAndStopwatch_ReplacingTheOldRow()
    {
        var state = new ClockToolsState(
            new ClockSlotsConfig([
                ClockSlotConfig.Scrolling(ClockTool.Clock, ClockTool.Alarm),
                ClockSlotConfig.Pinned(ClockTool.Timer),
                ClockSlotConfig.Scrolling(ClockTool.Stopwatch, ClockTool.Empty)]),
            TimerState.Idle.WithDuration(TimeSpan.FromMinutes(5)).Start(T0).Pause(T0.AddMinutes(1)).Start(T0.AddMinutes(3)),
            StopwatchState.Idle.Start(T0).Lap(T0.AddSeconds(10)).Lap(T0.AddSeconds(25)));
        using (var context = _fixture.CreateContext())
        {
            var repository = new ClockToolsRepository(context);
            repository.Save(new ClockToolsState(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle));
            repository.Save(state);
        }

        using var again = _fixture.CreateContext();
        var loaded = new ClockToolsRepository(again).Get();
        Assert.Equal(state.Slots, loaded.Slots);
        Assert.Equal(state.Timer, loaded.Timer);
        Assert.Equal(state.Stopwatch.Banked, loaded.Stopwatch.Banked);
        Assert.Equal(state.Stopwatch.StartedAt, loaded.Stopwatch.StartedAt);
        Assert.Equal(state.Stopwatch.Laps, loaded.Stopwatch.Laps);
        Assert.Single(again.ClockTools);
    }

    [Fact]
    public void ABrokenSlotsValue_FallsBackToTheDefaultSlots()
    {
        using var context = _fixture.CreateContext();
        context.ClockTools.Add(new ClockToolsEntity { SlotsJson = "not json" });
        context.SaveChanges();
        Assert.Equal(ClockSlotsConfig.Default, new ClockToolsRepository(context).Get().Slots);
    }

    public void Dispose() => _fixture.Dispose();
}
