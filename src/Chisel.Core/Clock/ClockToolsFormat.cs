namespace Chisel.Core.Clock;

public static class ClockToolsFormat
{
    // mm:ss, or h:mm:ss from one hour. A part of a second counts as a whole one, so 00:00 only shows at zero.
    public static string Countdown(TimeSpan remaining)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return Clock(seconds);
    }

    // The stopwatch moves in steps of this many milliseconds (0.2 s), so it only needs redrawing five times a second.
    public const int StopwatchStepMilliseconds = 200;

    // mm:ss.t, or h:mm:ss.t from one hour, where t is 0, 2, 4, 6 or 8: the time in steps of 0.2 s, rounded DOWN (never up), so the
    // display never runs ahead of real time. Every face, every lap and every split shows time this way.
    public static string Stopwatch(TimeSpan elapsed)
    {
        var steps = Math.Max(0, elapsed.Ticks) / (TimeSpan.TicksPerMillisecond * StopwatchStepMilliseconds);
        return $"{Clock(steps / 5)}.{steps % 5 * 2}";
    }

    private static string Clock(long totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }
}
