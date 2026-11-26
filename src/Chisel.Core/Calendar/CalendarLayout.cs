namespace Chisel.Core.Calendar;

// How much the calendar shows. Compact: the month name over the dates. Standard: adds the year, the weekday names and the
// previous/next arrows. Full: adds the Today button and, when they are on, week numbers.
public enum CalendarSize { Compact, Standard, Full }

// The calendar's layout for the room it has: what it shows (see CalendarSize) and the sizes of its parts, all from the widget's
// actual size, so text and cells grow and shrink with it like the clock's dial. The thresholds live here and nowhere else.
// A widget too short for readable dates is laid out at MinHeight instead (the card scrolls the rest), like the calculator.
public sealed record CalendarLayout(CalendarSize Size, double Pad, double HeaderHeight, double WeekdayHeight, double WeekNumberWidth,
    double CellWidth, double CellHeight, double TitleFont, double DayFont, double SmallFont, double ButtonSize, double MinHeight)
{
    public const double StandardMinWidth = 220;
    public const double StandardMinHeight = 200;
    public const double FullMinWidth = 300;
    public const double FullMinHeight = 270;

    // The shortest a row of dates may get before the widget scrolls instead.
    public const double MinCellHeight = 16;

    public bool ShowWeekdayNames => Size >= CalendarSize.Standard;
    public bool ShowNavigation => Size >= CalendarSize.Standard;
    public bool ShowYear => Size >= CalendarSize.Standard;
    public bool ShowTodayButton => Size == CalendarSize.Full;
    public bool ShowWeekNumbers => WeekNumberWidth > 0;

    public static CalendarSize SizeFor(double width, double height) =>
        width >= FullMinWidth && height >= FullMinHeight ? CalendarSize.Full
        : width >= StandardMinWidth && height >= StandardMinHeight ? CalendarSize.Standard
        : CalendarSize.Compact;

    public static CalendarLayout For(double width, double height, bool weekNumbers)
    {
        var layout = Compute(width, height, weekNumbers);
        for (var i = 0; i < 8 && layout.MinHeight > height + 0.01; i++)
        {
            height = layout.MinHeight;
            layout = Compute(width, height, weekNumbers);
        }
        return layout;
    }

    private static CalendarLayout Compute(double width, double height, bool weekNumbers)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        var size = SizeFor(width, height);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.035), 4, 16);
        var innerWidth = width - 2 * pad;
        var innerHeight = height - 2 * pad;

        // The text scales with a rough cell size first; the header and weekday rows are sized from it, the cells get the rest.
        var rough = Math.Min(innerWidth / MonthGrid.Columns, innerHeight / (MonthGrid.Rows + 2));
        var title = Math.Clamp(Math.Round(rough * 0.45), 11, 26);
        var small = Math.Clamp(Math.Round(rough * 0.3), 8, 15);
        var button = size == CalendarSize.Compact ? 0 : Math.Clamp(Math.Round(title * 1.7), 22, 40);
        var header = Math.Max(Math.Round(title * 1.6), button);
        var weekday = size == CalendarSize.Compact ? 0 : Math.Round(small * 1.8);
        var weekColumn = weekNumbers && size == CalendarSize.Full ? Math.Round(Math.Max(small * 2.2, innerWidth * 0.08)) : 0;

        var cellWidth = (innerWidth - weekColumn) / MonthGrid.Columns;
        var cellHeight = Math.Max(1, (innerHeight - header - weekday) / MonthGrid.Rows);
        var day = Math.Clamp(Math.Round(Math.Min(cellWidth, cellHeight) * 0.42), 10, 26);
        day = Math.Min(day, Math.Max(1, cellHeight - 2));
        var minHeight = 2 * pad + header + weekday + MonthGrid.Rows * MinCellHeight;
        return new CalendarLayout(size, pad, header, weekday, weekColumn, cellWidth, cellHeight, title, day, small, button, minHeight);
    }
}
