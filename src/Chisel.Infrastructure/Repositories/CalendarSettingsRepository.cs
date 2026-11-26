using System.Globalization;
using Chisel.Core.Calendar;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class CalendarSettingsRepository : ICalendarSettingsRepository
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ChiselDbContext _context;

    public CalendarSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    // A first day other than Sunday or Monday, or a date that does not parse, is dropped.
    public CalendarSettings Get()
    {
        if (_context.CalendarSettings.FirstOrDefault() is not { } e)
        {
            return CalendarSettings.Default;
        }
        DayOfWeek? first = e.FirstDay is (int)DayOfWeek.Sunday or (int)DayOfWeek.Monday ? (DayOfWeek)e.FirstDay : null;
        var marked = e.MarkedDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => DateOnly.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateOnly?)null)
            .OfType<DateOnly>()
            .Distinct()
            .Order()
            .ToList();
        return new CalendarSettings(first, e.ShowWeekNumbers, marked);
    }

    public void Save(CalendarSettings settings)
    {
        _context.CalendarSettings.RemoveRange(_context.CalendarSettings);
        _context.CalendarSettings.Add(new CalendarSettingsEntity
        {
            FirstDay = settings.FirstDay is { } first ? (int)first : null,
            ShowWeekNumbers = settings.ShowWeekNumbers,
            MarkedDays = string.Join(',', settings.MarkedDays.Distinct().Order().Select(d => d.ToString(DateFormat, CultureInfo.InvariantCulture))),
        });
        _context.SaveChanges();
    }
}
