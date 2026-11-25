namespace Chisel.Infrastructure.Data;

// The one saved set of calendar options (Id is always 1). FirstDay is the DayOfWeek as an int, or null to follow the system.
// MarkedDays is the marked dates as yyyy-MM-dd, comma separated.
public class CalendarSettingsEntity
{
    public int Id { get; set; } = 1;
    public int? FirstDay { get; set; }
    public bool ShowWeekNumbers { get; set; }
    public string MarkedDays { get; set; } = "";
}
