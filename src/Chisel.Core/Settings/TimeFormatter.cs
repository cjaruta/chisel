using System.Globalization;

namespace Chisel.Core.Settings;

public enum ClockFormat
{
    TwentyFourHour = 0,
    TwelveHour = 1
}

// The one place times are turned into display text. Times are stored and compared
// in 24-hour form; only the text changes. AM/PM is fixed text, not the system culture.
public static class TimeFormatter
{
    public static string Format(TimeOnly time, ClockFormat format, bool withSeconds = false)
    {
        if (format == ClockFormat.TwentyFourHour)
        {
            return time.ToString(withSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
        }

        var hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        var suffix = time.Hour < 12 ? "AM" : "PM";
        return withSeconds
            ? $"{hour}:{time.Minute:00}:{time.Second:00} {suffix}"
            : $"{hour}:{time.Minute:00} {suffix}";
    }

    public static string FormatRange(TimeOnly start, TimeOnly end, ClockFormat format) =>
        $"{Format(start, format)}-{Format(end, format)}";
}
