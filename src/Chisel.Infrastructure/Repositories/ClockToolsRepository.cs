using System.Text.Json;
using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class ClockToolsRepository : IClockToolsRepository
{
    private sealed record SlotDto(bool Scrolling, int Tool, int Set);

    private readonly ChiselDbContext _context;

    public ClockToolsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public ClockToolsState Get()
    {
        if (_context.ClockTools.FirstOrDefault() is not { } e)
        {
            return new ClockToolsState(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle);
        }
        return new ClockToolsState(
            Slots(e.SlotsJson),
            new TimerState(TimeSpan.FromTicks(e.TimerDurationTicks), TimeSpan.FromTicks(e.TimerBankedTicks), e.TimerStartedAt),
            new StopwatchState(TimeSpan.FromTicks(e.StopwatchBankedTicks), e.StopwatchStartedAt, Laps(e.StopwatchLapsJson)));
    }

    public void Save(ClockToolsState state)
    {
        _context.ClockTools.RemoveRange(_context.ClockTools);
        _context.ClockTools.Add(new ClockToolsEntity
        {
            SlotsJson = JsonSerializer.Serialize(state.Slots.Normalized().Slots.Select(s => new SlotDto(s.IsScrolling, (int)s.Tool, (int)s.Set))),
            TimerDurationTicks = state.Timer.Duration.Ticks, TimerBankedTicks = state.Timer.Banked.Ticks, TimerStartedAt = state.Timer.StartedAt,
            StopwatchBankedTicks = state.Stopwatch.Banked.Ticks, StopwatchStartedAt = state.Stopwatch.StartedAt,
            StopwatchLapsJson = JsonSerializer.Serialize(state.Stopwatch.Laps.Select(l => l.Ticks)),
        });
        _context.SaveChanges();
    }

    private static ClockSlotsConfig Slots(string json)
    {
        try
        {
            var dtos = JsonSerializer.Deserialize<List<SlotDto>>(json);
            return dtos is { Count: > 0 }
                ? new ClockSlotsConfig(dtos.Select(d => new ClockSlotConfig(d.Scrolling, (ClockTool)d.Tool, (ClockToolSet)d.Set)).ToList()).Normalized()
                : ClockSlotsConfig.Default;
        }
        catch (JsonException)
        {
            return ClockSlotsConfig.Default;
        }
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
