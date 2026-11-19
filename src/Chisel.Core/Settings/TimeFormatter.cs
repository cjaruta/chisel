using System.Globalization;
using System.Text.RegularExpressions;

namespace Chisel.Core.Settings;

public enum ClockFormat
{
    TwentyFourHour = 0,
    TwelveHour = 1
}

// The one place times are turned into display text and read back from it. Times are stored
// and compared in 24-hour form; only the text changes. AM/PM is fixed text, not the system culture.
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

    // Compact label for a whole hour: "15:00" in 24-hour, "3 PM" in 12-hour.
    public static string FormatHour(TimeOnly time, ClockFormat format) =>
        format == ClockFormat.TwentyFourHour
            ? Format(time, format)
            : $"{(time.Hour % 12 == 0 ? 12 : time.Hour % 12)} {(time.Hour < 12 ? "AM" : "PM")}";

    public static string FormatRange(TimeOnly start, TimeOnly end, ClockFormat format) =>
        $"{Format(start, format)}-{Format(end, format)}";

    private static readonly Regex TimePattern = new(
        @"^(?<hour>\d{1,2})(:(?<minute>\d{2}))?\s*(?<suffix>AM|PM)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Reads a time typed in either format: "14:30", "2:30 PM", "2:30pm", "9 AM".
    // A bare hour needs AM/PM, so "9" alone is rejected.
    public static bool TryParse(string? text, out TimeOnly time)
    {
        time = default;
        var match = TimePattern.Match(text?.Trim() ?? "");
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
        var suffix = match.Groups["suffix"];
        if (minute > 59)
        {
            return false;
        }

        if (suffix.Success)
        {
            if (hour is < 1 or > 12)
            {
                return false;
            }
            var isPm = suffix.Value.Equals("PM", StringComparison.OrdinalIgnoreCase);
            hour = hour % 12 + (isPm ? 12 : 0);
        }
        else if (!match.Groups["minute"].Success || hour > 23)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }
}
