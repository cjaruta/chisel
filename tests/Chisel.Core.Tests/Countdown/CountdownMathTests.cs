using Chisel.Core.Countdown;

namespace Chisel.Core.Tests.Countdown;

public class CountdownMathTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // A zone with daylight saving like US Eastern: clocks go forward at 02:00 on the second Sunday of March and back at
    // 02:00 on the first Sunday of November. Built here so the tests do not depend on the machine's zone database.
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone("Test Eastern", TimeSpan.FromHours(-5), "Test Eastern", "Test Eastern",
        "Test Eastern Daylight", new[]
        {
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
        });

    private static CountdownEntry Event(DateOnly date, TimeOnly? time = null, CountdownRepeat repeat = CountdownRepeat.None, string name = "Trip") =>
        new(Guid.NewGuid(), name, date, time, repeat);

    // A local wall-clock time in the zone.
    private static DateTimeOffset At(TimeZoneInfo zone, int y, int m, int d, int h = 12, int min = 0)
    {
        var local = new DateTime(y, m, d, h, min, 0);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static string Text(CountdownEntry e, DateTimeOffset now, TimeZoneInfo? zone = null) => CountdownMath.Status(e, now, zone ?? Utc).Text;

    [Theory]
    [InlineData(2000, 1, 31, 2000, 2, 1, 1)]     // across a month end
    [InlineData(2000, 2, 28, 2000, 3, 1, 2)]     // leap year: Feb 29 exists
    [InlineData(2001, 2, 28, 2001, 3, 1, 1)]     // not a leap year
    [InlineData(2000, 12, 31, 2001, 1, 1, 1)]    // across a year end
    [InlineData(2000, 3, 1, 2000, 2, 28, -2)]
    public void DaysBetween_CountsCalendarDays(int y1, int m1, int d1, int y2, int m2, int d2, int expected) =>
        Assert.Equal(expected, CountdownMath.DaysBetween(new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2)));

    [Fact]
    public void Text_ForWholeDays_TodayAndTomorrow()
    {
        var now = At(Utc, 2000, 1, 31, 23, 59);
        Assert.Equal("12 days", Text(Event(new DateOnly(2000, 2, 12)), now));
        Assert.Equal("Tomorrow", Text(Event(new DateOnly(2000, 2, 1)), now));
        Assert.Equal("Today!", Text(Event(new DateOnly(2000, 1, 31)), now));
        Assert.Equal("1 day ago", Text(Event(new DateOnly(2000, 1, 30)), now));
        Assert.Equal("3 days ago", Text(Event(new DateOnly(2000, 1, 28)), now));
    }

    [Fact]
    public void Text_WithATime_ShowsDaysAndHours_ThenHoursAndMinutes_ThenToday()
    {
        var e = Event(new DateOnly(2000, 1, 10), new TimeOnly(16, 30));
        Assert.Equal("12 days", Text(Event(new DateOnly(2000, 1, 19), new TimeOnly(16, 30)), At(Utc, 2000, 1, 7, 12)));
        Assert.Equal("3 days 4 hours", Text(e, At(Utc, 2000, 1, 7, 12, 30)));
        Assert.Equal("1 day", Text(e, At(Utc, 2000, 1, 9, 16, 30)));
        Assert.Equal("4 hours 30 minutes", Text(e, At(Utc, 2000, 1, 10, 12)));
        Assert.Equal("1 hour", Text(e, At(Utc, 2000, 1, 10, 15, 30)));
        Assert.Equal("5 minutes", Text(e, At(Utc, 2000, 1, 10, 16, 25)));
        Assert.Equal("Today!", Text(e, At(Utc, 2000, 1, 10, 17)));
    }

    [Fact]
    public void Units_SayWhenTheTextChangesByTheMinute()
    {
        var e = Event(new DateOnly(2000, 1, 10), new TimeOnly(16, 30));
        Assert.Equal(CountdownUnit.Days, CountdownMath.Status(Event(new DateOnly(2000, 1, 20)), At(Utc, 2000, 1, 10), Utc).Unit);
        Assert.Equal(CountdownUnit.Hours, CountdownMath.Status(e, At(Utc, 2000, 1, 7), Utc).Unit);
        Assert.Equal(CountdownUnit.Minutes, CountdownMath.Status(e, At(Utc, 2000, 1, 10, 12), Utc).Unit);
    }

    // On the day clocks go forward a calendar day has 23 hours; day counts must not slip.
    [Fact]
    public void DaylightSaving_DayCountsStayCalendarDays()
    {
        // Saturday 2000-03-11 23:30 to Monday 2000-03-13 (clocks go forward on Sunday 2000-03-12).
        var spring = At(Eastern, 2000, 3, 11, 23, 30);
        Assert.Equal("2 days", Text(Event(new DateOnly(2000, 3, 13)), spring, Eastern));
        Assert.Equal("Tomorrow", Text(Event(new DateOnly(2000, 3, 12)), spring, Eastern));

        // Just after midnight on the 25-hour day clocks go back (Sunday 2000-11-05).
        var fall = At(Eastern, 2000, 11, 5, 0, 30);
        Assert.Equal("Today!", Text(Event(new DateOnly(2000, 11, 5)), fall, Eastern));
        Assert.Equal("Tomorrow", Text(Event(new DateOnly(2000, 11, 6)), fall, Eastern));
    }

    [Fact]
    public void DaylightSaving_HoursAreRealHours()
    {
        // From 00:30 to 05:30 on the spring-forward day is four real hours (02:00 to 03:00 does not exist).
        var e = Event(new DateOnly(2000, 3, 12), new TimeOnly(5, 30));
        Assert.Equal("4 hours", Text(e, At(Eastern, 2000, 3, 12, 0, 30), Eastern));
    }

    [Fact]
    public void Midnight_IsTheNextLocalMidnight_AcrossDaylightSaving()
    {
        var spring = At(Eastern, 2000, 3, 12, 0, 30);
        var next = CountdownMath.NextUpdate([Event(new DateOnly(2000, 5, 1))], spring, Eastern)!.Value;
        Assert.Equal(TimeSpan.FromHours(22.5), next - spring);
    }

    [Fact]
    public void NextUpdate_IsTheNextMinute_OnlyWhenHoursOrMinutesAreShown()
    {
        var now = At(Utc, 2000, 1, 10, 12, 0).AddSeconds(20);
        Assert.Equal(At(Utc, 2000, 1, 11, 0, 0), CountdownMath.NextUpdate([Event(new DateOnly(2000, 2, 1))], now, Utc));
        Assert.Equal(At(Utc, 2000, 1, 10, 12, 1), CountdownMath.NextUpdate([Event(new DateOnly(2000, 1, 10), new TimeOnly(18, 0))], now, Utc));
        Assert.Null(CountdownMath.NextUpdate([], now, Utc));
    }

    [Theory]
    [InlineData(2000, 6, 1, 2000, 6, 15)]   // still to come this year
    [InlineData(2000, 6, 15, 2000, 6, 15)]  // today
    [InlineData(2000, 6, 16, 2001, 6, 15)]  // past: rolls to next year
    public void Yearly_RollsToTheNextOccurrence(int ty, int tm, int td, int ey, int em, int ed)
    {
        var birthday = Event(new DateOnly(1990, 6, 15), repeat: CountdownRepeat.Yearly);
        Assert.Equal(new DateOnly(ey, em, ed), CountdownMath.NextOccurrence(birthday, new DateOnly(ty, tm, td)));
    }

    [Fact]
    public void Yearly_OnFebruary29_FallsOnFebruary28_InOtherYears()
    {
        var leapling = Event(new DateOnly(2000, 2, 29), repeat: CountdownRepeat.Yearly);
        Assert.Equal(new DateOnly(2001, 2, 28), CountdownMath.NextOccurrence(leapling, new DateOnly(2000, 3, 1)));
        Assert.Equal(new DateOnly(2004, 2, 29), CountdownMath.NextOccurrence(leapling, new DateOnly(2003, 3, 1)));
    }

    [Fact]
    public void Yearly_IsNeverPast()
    {
        var e = Event(new DateOnly(1990, 1, 1), repeat: CountdownRepeat.Yearly);
        Assert.Equal("Today!", Text(e, At(Utc, 2000, 1, 1)));
        Assert.Equal("365 days", Text(e, At(Utc, 2000, 1, 2)));
    }

    [Fact]
    public void Progress_RunsFromCreationToTheTarget()
    {
        var e = Event(new DateOnly(2000, 1, 11)) with { Created = At(Utc, 2000, 1, 1, 0) };
        Assert.Equal(0.5, CountdownMath.Progress(e, At(Utc, 2000, 1, 6, 0), Utc), 3);
        Assert.Equal(1, CountdownMath.Progress(e, At(Utc, 2000, 2, 1), Utc));
        Assert.Equal(0, CountdownMath.Progress(e, At(Utc, 1999, 12, 1), Utc));
    }

    [Fact]
    public void Progress_ForAYearlyEvent_RunsFromItsLastOccurrence()
    {
        var e = Event(new DateOnly(1990, 1, 1), repeat: CountdownRepeat.Yearly) with { Created = At(Utc, 1990, 1, 1, 0) };
        Assert.InRange(CountdownMath.Progress(e, At(Utc, 2000, 7, 2, 0), Utc), 0.49, 0.51);
    }

    [Fact]
    public void Sorted_PutsUpcomingByDate_ThenPastNewestFirst_AndLeavesOutArchived()
    {
        var now = At(Utc, 2000, 1, 10);
        var events = new[]
        {
            Event(new DateOnly(2000, 3, 1), name: "March"),
            Event(new DateOnly(2000, 1, 5), name: "Long ago"),
            Event(new DateOnly(2000, 1, 10), name: "Today"),
            Event(new DateOnly(2000, 1, 9), name: "Yesterday"),
            Event(new DateOnly(1990, 1, 12), repeat: CountdownRepeat.Yearly, name: "Birthday"),
            Event(new DateOnly(2000, 2, 1), name: "Archived") with { Archived = true },
        };

        Assert.Equal(new[] { "Today", "Birthday", "March", "Yesterday", "Long ago" },
            CountdownMath.Sorted(events, now, Utc).Select(e => e.Name));
    }

    [Fact]
    public void PastPolicy_ArchivesOrDeletes_OnlyOneOffEventsWhoseDayIsOver()
    {
        var today = new DateOnly(2000, 1, 10);
        var events = new[]
        {
            Event(new DateOnly(2000, 1, 9), name: "Past"),
            Event(new DateOnly(2000, 1, 10), name: "Today"),
            Event(new DateOnly(1990, 1, 1), repeat: CountdownRepeat.Yearly, name: "Yearly"),
        };

        var kept = CountdownMath.ApplyPastPolicy(events, PastEventPolicy.ShowAgo, today);
        Assert.Same(events, kept);

        var archived = CountdownMath.ApplyPastPolicy(events, PastEventPolicy.Archive, today);
        Assert.Equal(new[] { true, false, false }, archived.Select(e => e.Archived));

        var deleted = CountdownMath.ApplyPastPolicy(events, PastEventPolicy.Delete, today);
        Assert.Equal(new[] { "Today", "Yearly" }, deleted.Select(e => e.Name));
    }

    [Fact]
    public void Spoken_ReadsTheWholeThing()
    {
        var now = At(Utc, 2000, 1, 1);
        Assert.Equal("Trip, 12 days remaining", CountdownMath.Spoken(Event(new DateOnly(2000, 1, 13)), CountdownMath.Status(Event(new DateOnly(2000, 1, 13)), now, Utc)));
        Assert.Equal("Trip, today", CountdownMath.Spoken(Event(new DateOnly(2000, 1, 1)), CountdownMath.Status(Event(new DateOnly(2000, 1, 1)), now, Utc)));
        Assert.Equal("Trip, tomorrow", CountdownMath.Spoken(Event(new DateOnly(2000, 1, 2)), CountdownMath.Status(Event(new DateOnly(2000, 1, 2)), now, Utc)));
        Assert.Equal("Trip, 3 days ago", CountdownMath.Spoken(Event(new DateOnly(1999, 12, 29)), CountdownMath.Status(Event(new DateOnly(1999, 12, 29)), now, Utc)));
    }
}
