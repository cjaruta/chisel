namespace Chisel.Core.Clock;

// Everything saved about the tools: which slots the clock widget has and what each shows, plus the timer and stopwatch
// (as absolute timestamps, so they survive a restart).
public sealed record ClockToolsState(ClockSlotsConfig Slots, TimerState Timer, StopwatchState Stopwatch);

// The one saved tools state (replace-all). Get returns the defaults until one is saved.
public interface IClockToolsRepository
{
    ClockToolsState Get();
    void Save(ClockToolsState state);
}
