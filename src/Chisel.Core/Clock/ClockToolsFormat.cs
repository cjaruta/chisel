namespace Chisel.Core.Clock;

public static class ClockToolsFormat
{
    // mm:ss, or h:mm:ss from one hour. A part of a second counts as a whole one, so 00:00 only shows at zero.
    public static string Countdown(TimeSpan remaining)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return Clock(seconds);
    }

    // The stopwatch moves in steps of this many milliseconds (0.1 s), so it only needs redrawing ten times a second.
    public const int StopwatchStepMilliseconds = 100;

    // mm:ss.t, or h:mm:ss.t from one hour, where t is the tenth, 0 to 9: the time rounded DOWN (never up) to 0.1 s, so the
    // display never runs ahead of real time. Every face, every lap and every split shows time this way.
    public static string Stopwatch(TimeSpan elapsed)
    {
        var steps = Math.Max(0, elapsed.Ticks) / (TimeSpan.TicksPerMillisecond * StopwatchStepMilliseconds);
        return $"{Clock(steps / 10)}.{steps % 10}";
    }

    private static string Clock(long totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }
}
