using System.Collections.Immutable;
using Chisel.Core.Habits;

namespace Chisel.Core.Tests.Habits;

public class HabitStreaksTests
{
    // 2000-01-03 is a Monday.
    private static DateOnly D(int month, int day, int year = 2000) => new(year, month, day);

    private static Habit With(HabitDays days, params DateOnly[] done) =>
        new(Guid.NewGuid(), "Read", Days: days, Done: ImmutableSortedSet.Create(done));

    private static Habit Daily(params DateOnly[] done) => With(HabitDays.Every, done);

    private static DateOnly[] Range(DateOnly from, DateOnly to)
    {
        var list = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            list.Add(d);
        }
        return list.ToArray();
    }

    [Fact]
    public void ConsecutiveDays_CountThroughToday_WhenTodayIsDone()
    {
        var habit = Daily(Range(D(1, 1), D(1, 5)));
        Assert.Equal(new HabitStreak(5, 5), HabitStreaks.For(habit, D(1, 5)));
    }

    [Fact]
    public void TodayNotYetDone_KeepsYesterdaysStreak()
    {
        var habit = Daily(Range(D(1, 1), D(1, 4)));
        Assert.Equal(new HabitStreak(4, 4), HabitStreaks.For(habit, D(1, 5)));
    }

    [Fact]
    public void AMissedDay_EndsTheCurrentStreak_AndBestKeepsTheLongest()
    {
        var habit = Daily([.. Range(D(1, 1), D(1, 4)), .. Range(D(1, 6), D(1, 7))]);
        Assert.Equal(new HabitStreak(2, 4), HabitStreaks.For(habit, D(1, 7)));
        Assert.Equal(new HabitStreak(0, 4), HabitStreaks.For(habit, D(1, 9)));
    }

    [Fact]
    public void NothingDone_IsZero()
    {
        Assert.Equal(new HabitStreak(0, 0), HabitStreaks.For(Daily(), D(1, 5)));
    }

    [Fact]
    public void ScheduledWeekdays_SkipTheDaysOff()
    {
        var mwf = HabitDays.Monday | HabitDays.Wednesday | HabitDays.Friday;
        var habit = With(mwf, D(1, 3), D(1, 5), D(1, 7), D(1, 10));
        Assert.Equal(new HabitStreak(4, 4), HabitStreaks.For(habit, D(1, 11))); // Tuesday is off: no break, no count
        Assert.Equal(new HabitStreak(4, 4), HabitStreaks.For(habit, D(1, 12))); // Wednesday, not done yet

        var missedWednesday = With(mwf, D(1, 3), D(1, 7), D(1, 10));
        Assert.Equal(new HabitStreak(2, 2), HabitStreaks.For(missedWednesday, D(1, 10)));
    }

    [Fact]
    public void Streaks_RunAcrossMonthAndYearEnds_AndLeapDays()
    {
        Assert.Equal(4, HabitStreaks.For(Daily(Range(D(12, 30, 1999), D(1, 2))), D(1, 2)).Current);
        Assert.Equal(3, HabitStreaks.For(Daily(Range(D(2, 28), D(3, 1))), D(3, 1)).Current);
        Assert.Equal(2, HabitStreaks.For(Daily(Range(D(2, 28, 2001), D(3, 1, 2001))), D(3, 1, 2001)).Current);
    }

    [Fact]
    public void ADaylightSavingDay_IsStillOneCalendarDay()
    {
        // 2000-03-26 and 2000-10-29 are the European clock changes; dates are calendar days, so nothing shifts.
        Assert.Equal(3, HabitStreaks.For(Daily(Range(D(3, 25), D(3, 27))), D(3, 27)).Current);
        Assert.Equal(3, HabitStreaks.For(Daily(Range(D(10, 28), D(10, 30))), D(10, 30)).Current);
    }

    [Fact]
    public void DatesAfterToday_AreIgnored()
    {
        var habit = Daily(D(1, 4), D(1, 5), D(1, 9));
        Assert.Equal(new HabitStreak(2, 2), HabitStreaks.For(habit, D(1, 5)));
    }

    [Fact]
    public void History_EndsWithToday_AndMarksEachDay()
    {
        var habit = With(HabitDays.Every & ~HabitDays.Sunday, D(1, 3), D(1, 5));
        var history = HabitStreaks.History(habit, D(1, 6), 5); // Sun 2 .. Thu 6

        Assert.Equal(new[] { HabitDayState.Off, HabitDayState.Done, HabitDayState.Missed, HabitDayState.Done, HabitDayState.Pending }, history);
    }

    [Fact]
    public void Days_MapToWeekdays()
    {
        Assert.True(HabitDays.Every.Includes(DayOfWeek.Sunday));
        Assert.True(HabitDays.Monday.Includes(DayOfWeek.Monday));
        Assert.False(HabitDays.Monday.Includes(DayOfWeek.Tuesday));
        Assert.Equal(HabitDays.Saturday, HabitDaysExtensions.Of(DayOfWeek.Saturday));
    }
}
