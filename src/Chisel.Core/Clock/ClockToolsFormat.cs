namespace Chisel.Core.Clock;

public static class ClockToolsFormat
{
    // mm:ss, or h:mm:ss from one hour. A part of a second counts as a whole one, so 00:00 only shows at zero.
    public static string Countdown(TimeSpan remaining)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        return Clock(seconds);
    }

    // mm:ss.t (tenths, truncated), or h:mm:ss.t from one hour.
    public static string Stopwatch(TimeSpan elapsed)
    {
        var tenths = (long)Math.Max(0, elapsed.TotalMilliseconds) / 100;
        return $"{Clock(tenths / 10)}.{tenths % 10}";
    }

    private static string Clock(long totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }
}
