using System.Globalization;

namespace Chisel.Core.Calendar;

// The days a month view shows: always 6 weeks of 7 days, so the grid keeps its size from month to month. The first row starts
// on the chosen first day of the week, on or before the 1st; days before and after the month fill the rest.
public static class MonthGrid
{
    public const int Rows = 6;
    public const int Columns = 7;

    public static IReadOnlyList<DateOnly> Days(int year, int month, DayOfWeek firstDay)
    {
        var first = new DateOnly(year, month, 1);
        var start = first.AddDays(-Offset(first.DayOfWeek, firstDay));
        var days = new DateOnly[Rows * Columns];
        for (var i = 0; i < days.Length; i++)
        {
            days[i] = start.AddDays(i);
        }
        return days;
    }

    // The column a weekday falls in when weeks start on firstDay.
    public static int Offset(DayOfWeek day, DayOfWeek firstDay) => ((int)day - (int)firstDay + 7) % 7;

    // The seven weekdays in column order.
    public static DayOfWeek[] WeekdayOrder(DayOfWeek firstDay) =>
        Enumerable.Range(0, Columns).Select(i => (DayOfWeek)(((int)firstDay + i) % 7)).ToArray();

    // The ISO week number of the row that starts on rowStart: the week its Thursday falls in, so it is right for either first day.
    public static int WeekNumber(DateOnly rowStart, DayOfWeek firstDay)
    {
        var thursday = rowStart.AddDays(Offset(DayOfWeek.Thursday, firstDay));
        return ISOWeek.GetWeekOfYear(thursday.ToDateTime(TimeOnly.MinValue));
    }
}
