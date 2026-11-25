namespace Chisel.Core.Countdown;

public enum CountdownPhase { Upcoming, Today, Past }

// The smallest unit the text shows, which says how often it can change: Days at midnight, Hours and Minutes every minute.
public enum CountdownUnit { Days, Hours, Minutes }

// How far off an event is. Lead is the part shown large ("12", "Today!", "Tomorrow"), Rest what follows it ("days",
// "days 4 hours ago"); Text is both. Days is the calendar-day difference (negative when past).
public sealed record CountdownStatus(CountdownPhase Phase, int Days, string Lead, string Rest, CountdownUnit Unit, DateOnly Date)
{
    public string Text => Rest.Length == 0 ? Lead : $"{Lead} {Rest}";
}

// The countdown's arithmetic, all in the user's local time. Day counts are calendar-day differences between local dates, so a
// day that daylight saving makes 23 or 25 hours long still counts as one; hours and minutes on the last day are real elapsed
// time between instants. Pure functions of (event, now, zone), so every rule is tested without a clock.
public static class CountdownMath
{
    // Under this many days an event with a time shows days and hours ("3 days 4 hours").
    public const int DaysAndHoursUnder = 7;

    public static int DaysBetween(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber;

    // The date a yearly event falls on in a year; February 29 falls on February 28 in other years.
    public static DateOnly InYear(DateOnly date, int year) =>
        new(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));

    // The date that counts from `today`: a one-off event's own date, or the next time a yearly one comes round (today included).
    public static DateOnly NextOccurrence(CountdownEntry e, DateOnly today)
    {
        if (e.Repeat != CountdownRepeat.Yearly)
        {
            return e.Date;
        }
        var date = InYear(e.Date, Math.Max(today.Year, e.Date.Year));
        return date >= today ? date : InYear(e.Date, date.Year + 1);
    }

    public static CountdownStatus Status(CountdownEntry e, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = Local(now, zone);
        var today = DateOnly.FromDateTime(local);
        var date = NextOccurrence(e, today);
        var days = DaysBetween(today, date);
        if (days < 0)
        {
            return new(CountdownPhase.Past, days, (-days).ToString(), (days == -1 ? "day" : "days") + " ago", CountdownUnit.Days, date);
        }

        var target = date.ToDateTime(e.Time ?? TimeOnly.MinValue);
        if (e.Time is not null && local < target)
        {
            var wall = target - local;
            if (wall < TimeSpan.FromDays(1))
            {
                return HoursAndMinutes(Instant(target, zone) - now, days, date);
            }
            if (days < DaysAndHoursUnder)
            {
                var rest = Plural(wall.Days, "day", false) + (wall.Hours > 0 ? " " + Plural(wall.Hours, "hour", true) : "");
                return new(CountdownPhase.Upcoming, days, wall.Days.ToString(), rest, CountdownUnit.Hours, date);
            }
        }
        return days switch
        {
            0 => new(CountdownPhase.Today, 0, "Today!", "", CountdownUnit.Days, date),
            1 when e.Time is null => new(CountdownPhase.Upcoming, 1, "Tomorrow", "", CountdownUnit.Days, date),
            _ => new(CountdownPhase.Upcoming, days, days.ToString(), days == 1 ? "day" : "days", CountdownUnit.Days, date),
        };
    }

    // "4 hours 30 minutes", "1 hour", "5 minutes": whole minutes, rounded up so the last minute shows "1 minute".
    private static CountdownStatus HoursAndMinutes(TimeSpan remaining, int days, DateOnly date)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        var (h, m) = (minutes / 60, minutes % 60);
        return h > 0
            ? new(CountdownPhase.Upcoming, days, h.ToString(), (h == 1 ? "hour" : "hours") + (m > 0 ? " " + Plural(m, "minute", true) : ""), CountdownUnit.Minutes, date)
            : new(CountdownPhase.Upcoming, days, m.ToString(), m == 1 ? "minute" : "minutes", CountdownUnit.Minutes, date);
    }

    private static string Plural(int n, string unit, bool withNumber)
    {
        var word = n == 1 ? unit : unit + "s";
        return withNumber ? $"{n} {word}" : word;
    }

    // "Trip, 12 days remaining", "Trip, today", "Trip, tomorrow", "Trip, 3 days ago".
    public static string Spoken(CountdownEntry e, CountdownStatus status) => status.Phase switch
    {
        CountdownPhase.Today => $"{e.Name}, today",
        CountdownPhase.Past => $"{e.Name}, {status.Text}",
        _ when status.Lead == "Tomorrow" => $"{e.Name}, tomorrow",
        _ => $"{e.Name}, {status.Text} remaining",
    };

    // How far through its wait an event is, 0 to 1: from when it was added (or, for a yearly event, its last occurrence if that is
    // later) to its next target instant.
    public static double Progress(CountdownEntry e, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(Local(now, zone));
        var next = NextOccurrence(e, today);
        var time = e.Time ?? TimeOnly.MinValue;
        var end = Instant(next.ToDateTime(time), zone);
        var start = e.Created;
        if (e.Repeat == CountdownRepeat.Yearly)
        {
            var last = Instant(InYear(e.Date, next.Year - 1).ToDateTime(time), zone);
            if (last > start)
            {
                start = last;
            }
        }
        if (end <= start)
        {
            return now >= end ? 1 : 0;
        }
        return Math.Clamp((now - start) / (end - start), 0, 1);
    }

    // Upcoming and today's events by when they happen, then past ones newest first. Archived events are left out.
    public static IReadOnlyList<CountdownEntry> Sorted(IEnumerable<CountdownEntry> events, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(Local(now, zone));
        return events.Where(e => !e.Archived)
            .Select(e => (Event: e, Date: NextOccurrence(e, today)))
            .OrderBy(x => x.Date < today)
            .ThenBy(x => x.Date < today ? -x.Date.DayNumber : x.Date.DayNumber)
            .ThenBy(x => x.Event.Time ?? TimeOnly.MinValue)
            .Select(x => x.Event)
            .ToList();
    }

    // When the text of any of these events can next change: the next local midnight, or the next minute while one shows hours
    // or minutes. Null when there is nothing to watch, so no timer runs.
    public static DateTimeOffset? NextUpdate(IEnumerable<CountdownEntry> events, DateTimeOffset now, TimeZoneInfo zone)
    {
        var any = false;
        var byMinute = false;
        foreach (var e in events)
        {
            if (e.Archived)
            {
                continue;
            }
            any = true;
            byMinute |= Status(e, now, zone).Unit != CountdownUnit.Days;
        }
        if (!any)
        {
            return null;
        }
        var local = Local(now, zone);
        var midnight = Instant(local.Date.AddDays(1), zone);
        if (!byMinute)
        {
            return midnight;
        }
        var minute = Instant(new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0).AddMinutes(1), zone);
        return minute < midnight ? minute : midnight;
    }

    // Applies the past-event setting: one-off events whose day is over are archived or deleted. Returns the same list when
    // nothing changes, so the caller knows whether to save.
    public static IReadOnlyList<CountdownEntry> ApplyPastPolicy(IReadOnlyList<CountdownEntry> events, PastEventPolicy policy, DateOnly today)
    {
        bool IsOver(CountdownEntry e) => e.Repeat == CountdownRepeat.None && e.Date < today && !e.Archived;
        if (policy == PastEventPolicy.ShowAgo || !events.Any(IsOver))
        {
            return events;
        }
        return policy == PastEventPolicy.Archive
            ? events.Select(e => IsOver(e) ? e with { Archived = true } : e).ToList()
            : events.Where(e => !IsOver(e)).ToList();
    }

    public static DateTime Local(DateTimeOffset now, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(now, zone).DateTime;

    public static DateOnly Today(DateTimeOffset now, TimeZoneInfo zone) => DateOnly.FromDateTime(Local(now, zone));

    // The instant of a local wall-clock time. A time skipped by a spring-forward change maps to the hour after; a repeated one
    // to its first occurrence.
    public static DateTimeOffset Instant(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
