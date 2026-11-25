using System.Globalization;

namespace Chisel.Core.Countdown;

// Checks what the user typed into the countdown's add/edit form. Error is the message to show, or null when it is fine; Time is
// the parsed time (null when left empty).
public static class CountdownValidation
{
    public const int MaxEvents = 50;
    public const int MaxNameLength = 60;
    public static readonly DateOnly MinDate = new(1900, 1, 1);
    public static readonly DateOnly MaxDate = new(2199, 12, 31);

    public sealed record Result(string? Error, TimeOnly? Time);

    public static Result Validate(string? name, DateOnly? date, string? timeText, string? emoji, int eventCount, bool adding, CultureInfo culture)
    {
        if (adding && eventCount >= MaxEvents)
        {
            return new($"You can track up to {MaxEvents} events.", null);
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            return new("Give the event a name.", null);
        }
        if (name.Trim().Length > MaxNameLength)
        {
            return new($"Keep the name under {MaxNameLength} characters.", null);
        }
        if (date is not { } d)
        {
            return new("Pick a date.", null);
        }
        if (d < MinDate || d > MaxDate)
        {
            return new($"Pick a date between {MinDate.Year} and {MaxDate.Year}.", null);
        }
        TimeOnly? time = null;
        if (!string.IsNullOrWhiteSpace(timeText))
        {
            var text = timeText.Trim();
            if (!TimeOnly.TryParse(text, culture, DateTimeStyles.None, out var parsed)
                && !TimeOnly.TryParseExact(text, ["H:mm", "HH:mm", "H.mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return new("Enter the time like 14:30, or leave it empty.", null);
            }
            time = new TimeOnly(parsed.Hour, parsed.Minute);
        }
        if (!string.IsNullOrWhiteSpace(emoji) && new StringInfo(emoji.Trim()).LengthInTextElements > 1)
        {
            return new("Use a single emoji, or leave it empty.", null);
        }
        return new(null, time);
    }
}
