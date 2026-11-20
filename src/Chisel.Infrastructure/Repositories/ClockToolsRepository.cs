using System.Text.Json;
using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class ClockToolsRepository : IClockToolsRepository
{
    private readonly ChiselDbContext _context;

    public ClockToolsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public ClockToolsState Get()
    {
        if (_context.ClockTools.FirstOrDefault() is not { } e)
        {
            return new ClockToolsState(ClockToolsConfig.Default, TimerState.Idle, StopwatchState.Idle, ClockMode.Clock);
        }
        return new ClockToolsState(
            new ClockToolsConfig(e.AlarmEnabled, e.TimerEnabled, e.StopwatchEnabled, (StripPlacement)e.Strip),
            new TimerState(TimeSpan.FromTicks(e.TimerDurationTicks), TimeSpan.FromTicks(e.TimerBankedTicks), e.TimerStartedAt),
            new StopwatchState(TimeSpan.FromTicks(e.StopwatchBankedTicks), e.StopwatchStartedAt, Laps(e.StopwatchLapsJson)),
            (ClockMode)e.Mode);
    }

    public void Save(ClockToolsState state)
    {
        _context.ClockTools.RemoveRange(_context.ClockTools);
        _context.ClockTools.Add(new ClockToolsEntity
        {
            AlarmEnabled = state.Config.AlarmEnabled, TimerEnabled = state.Config.TimerEnabled,
            StopwatchEnabled = state.Config.StopwatchEnabled, Strip = (int)state.Config.Strip, Mode = (int)state.Mode,
            TimerDurationTicks = state.Timer.Duration.Ticks, TimerBankedTicks = state.Timer.Banked.Ticks, TimerStartedAt = state.Timer.StartedAt,
            StopwatchBankedTicks = state.Stopwatch.Banked.Ticks, StopwatchStartedAt = state.Stopwatch.StartedAt,
            StopwatchLapsJson = JsonSerializer.Serialize(state.Stopwatch.Laps.Select(l => l.Ticks)),
        });
        _context.SaveChanges();
    }

    private static IReadOnlyList<TimeSpan> Laps(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<long>>(json) ?? []).Select(TimeSpan.FromTicks).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
