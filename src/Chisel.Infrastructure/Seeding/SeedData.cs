using Chisel.Core.Schedule;
using Chisel.Core.Settings;

namespace Chisel.Infrastructure.Seeding;

public static class SeedData
{
    public static UserSettings CreateUserSettings() =>
        new("Caden", new TimeOnly(6, 30), new TimeOnly(23, 0));

    public static DaySchedule CreateSchedule() =>
        new(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Focus", new TimeOnly(12, 0), new TimeOnly(14, 0)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0)),
            new ScheduleBlock("Focus", new TimeOnly(16, 0), new TimeOnly(18, 0)),
            new ScheduleBlock("Read", new TimeOnly(22, 0), new TimeOnly(23, 0))
        });

    public static IReadOnlyList<Marker> CreateMarkers() =>
        new[]
        {
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder),
            new Marker("All work done", new TimeOnly(18, 0), MarkerKind.Checkpoint),
            new Marker("Dinner", new TimeOnly(18, 30), MarkerKind.Reminder)
        };
}
