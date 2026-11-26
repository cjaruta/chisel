namespace Chisel.Core.Focus;

public enum FocusPhase { Focus, ShortBreak, LongBreak }

// The face the widget draws: the ring that drains as time runs out, or the digits alone.
public enum FocusFace { Ring, Digital }

// The focus timer's options. Durations are whole minutes; ChimeVolume is 0 (muted) to 100. TaskId is the task shown as the
// current focus, if any (only its id is kept, the task itself stays in the task list).
public sealed record FocusSettings(
    int FocusMinutes, int ShortBreakMinutes, int LongBreakMinutes, int SessionsBeforeLongBreak,
    bool AutoStartNext, int ChimeVolume, FocusFace Face, Guid? TaskId)
{
    public static FocusSettings Default { get; } = new(25, 5, 15, 4, AutoStartNext: false, ChimeVolume: 40, FocusFace.Ring, TaskId: null);

    public TimeSpan Duration(FocusPhase phase) => TimeSpan.FromMinutes(phase switch
    {
        FocusPhase.ShortBreak => ShortBreakMinutes,
        FocusPhase.LongBreak => LongBreakMinutes,
        _ => FocusMinutes,
    });

    // The same options with every value moved into its limits (for data read from storage).
    public FocusSettings Clamped() => this with
    {
        FocusMinutes = Math.Clamp(FocusMinutes, FocusValidation.MinMinutes, FocusValidation.MaxFocusMinutes),
        ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, FocusValidation.MinMinutes, FocusValidation.MaxShortBreakMinutes),
        LongBreakMinutes = Math.Clamp(LongBreakMinutes, FocusValidation.MinMinutes, FocusValidation.MaxLongBreakMinutes),
        SessionsBeforeLongBreak = Math.Clamp(SessionsBeforeLongBreak, 1, FocusValidation.MaxSessionsBeforeLongBreak),
        ChimeVolume = Math.Clamp(ChimeVolume, 0, 100),
    };
}

public static class FocusValidation
{
    public const int MinMinutes = 1;
    public const int MaxFocusMinutes = 180;
    public const int MaxShortBreakMinutes = 60;
    public const int MaxLongBreakMinutes = 120;
    public const int MaxSessionsBeforeLongBreak = 12;

    // What is wrong with the options, in words for the user, or null when they are fine.
    public static string? Error(FocusSettings s) =>
        s.FocusMinutes is < MinMinutes or > MaxFocusMinutes ? $"Focus must be {MinMinutes} to {MaxFocusMinutes} minutes."
        : s.ShortBreakMinutes is < MinMinutes or > MaxShortBreakMinutes ? $"A short break must be {MinMinutes} to {MaxShortBreakMinutes} minutes."
        : s.LongBreakMinutes is < MinMinutes or > MaxLongBreakMinutes ? $"A long break must be {MinMinutes} to {MaxLongBreakMinutes} minutes."
        : s.SessionsBeforeLongBreak is < 1 or > MaxSessionsBeforeLongBreak ? $"Sessions before a long break must be 1 to {MaxSessionsBeforeLongBreak}."
        : s.ChimeVolume is < 0 or > 100 ? "The chime volume must be 0 to 100."
        : null;
}

// A ready-made set of durations; applying one keeps every other option.
public sealed record FocusPreset(string Name, int FocusMinutes, int ShortBreakMinutes, int LongBreakMinutes)
{
    public FocusSettings ApplyTo(FocusSettings s) =>
        s with { FocusMinutes = FocusMinutes, ShortBreakMinutes = ShortBreakMinutes, LongBreakMinutes = LongBreakMinutes };
}

public static class FocusPresets
{
    public static IReadOnlyList<FocusPreset> All { get; } = new[]
    {
        new FocusPreset("25 / 5", 25, 5, 15),
        new FocusPreset("50 / 10", 50, 10, 30),
        new FocusPreset("15 / 3", 15, 3, 10),
    };
}
