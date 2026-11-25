namespace Chisel.Core.Calendar;

// The calendar's options, saved as one record (replace-all). FirstDay: Sunday or Monday, or null to follow the system culture.
// ShowWeekNumbers: a column of ISO week numbers when the widget is large enough. MarkedDays: dates the user put a dot on.
public sealed record CalendarSettings(DayOfWeek? FirstDay = null, bool ShowWeekNumbers = false, IReadOnlyList<DateOnly>? MarkedDays = null)
{
    public static CalendarSettings Default { get; } = new();

    public IReadOnlyList<DateOnly> MarkedDays { get; init; } = MarkedDays ?? [];
}

public interface ICalendarSettingsRepository
{
    CalendarSettings Get();
    void Save(CalendarSettings settings);
}
