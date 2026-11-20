namespace Chisel.Core.Clock;

// The modes of the clock widget's panel.
public enum ClockMode { Clock, Alarm, Timer, Stopwatch }

// Where the mode strip sits in the panel.
public enum StripPlacement { Top, Bottom }

// Which tools are switched on (set in edit mode) and where the mode strip goes. The Clock mode is always there.
public sealed record ClockToolsConfig(bool AlarmEnabled, bool TimerEnabled, bool StopwatchEnabled, StripPlacement Strip)
{
    public static ClockToolsConfig Default { get; } = new(true, true, true, StripPlacement.Top);

    public bool IsEnabled(ClockMode mode) => mode switch
    {
        ClockMode.Alarm => AlarmEnabled,
        ClockMode.Timer => TimerEnabled,
        ClockMode.Stopwatch => StopwatchEnabled,
        _ => true,
    };

    public IReadOnlyList<ClockMode> EnabledModes => Enum.GetValues<ClockMode>().Where(IsEnabled).ToList();
}

// Everything saved about the tools: the config, the timer and stopwatch (as absolute timestamps), and the mode last shown.
public sealed record ClockToolsState(ClockToolsConfig Config, TimerState Timer, StopwatchState Stopwatch, ClockMode Mode);

// The one saved tools state (replace-all). Get returns the defaults until one is saved.
public interface IClockToolsRepository
{
    ClockToolsState Get();
    void Save(ClockToolsState state);
}
