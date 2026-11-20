namespace Chisel.Core.Clock;

[Flags]
public enum AlarmDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Every = Weekdays | Weekend,
}

// An alarm: a time of day on chosen weekdays, or just once (no days). LastFiredOn stops it ringing twice on the same day.
public sealed record Alarm(Guid Id, TimeOnly Time, string Label, bool Enabled, AlarmDays Days, DateOnly? LastFiredOn)
{
    public static Alarm Create(TimeOnly time, string label, AlarmDays days) =>
        new(Guid.NewGuid(), time, label.Trim(), true, days, null);

    public bool Repeats => Days != AlarmDays.None;

    // A one-time alarm counts on any date; a repeating one only on its days.
    public bool OccursOn(DateOnly date) => !Repeats || Days.HasFlag(Flag(date.DayOfWeek));

    public static AlarmDays Flag(DayOfWeek day) => day == DayOfWeek.Sunday ? AlarmDays.Sunday : (AlarmDays)(1 << ((int)day - 1));
}

public interface IAlarmRepository
{
    IReadOnlyList<Alarm> GetAll();
    // Adds the alarm, or replaces the one with the same id.
    void Save(Alarm alarm);
    void Delete(Guid id);
}

public static class AlarmFormat
{
    private static readonly (AlarmDays Day, string Short)[] Names =
    [
        (AlarmDays.Monday, "Mon"), (AlarmDays.Tuesday, "Tue"), (AlarmDays.Wednesday, "Wed"), (AlarmDays.Thursday, "Thu"),
        (AlarmDays.Friday, "Fri"), (AlarmDays.Saturday, "Sat"), (AlarmDays.Sunday, "Sun"),
    ];

    public static string Days(AlarmDays days) => days switch
    {
        AlarmDays.None => "Once",
        AlarmDays.Every => "Every day",
        AlarmDays.Weekdays => "Weekdays",
        AlarmDays.Weekend => "Weekends",
        _ => string.Join(", ", Names.Where(n => days.HasFlag(n.Day)).Select(n => n.Short)),
    };
}
