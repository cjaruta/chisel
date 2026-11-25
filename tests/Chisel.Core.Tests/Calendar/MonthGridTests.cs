using Chisel.Core.Calendar;

namespace Chisel.Core.Tests.Calendar;

public class MonthGridTests
{
    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    public void EveryMonth_HasSixFullWeeks_StartingOnTheFirstDay_AndHoldsTheWholeMonth(DayOfWeek firstDay)
    {
        // 2026 and 2027 between them start a month on every weekday.
        var starts = new HashSet<DayOfWeek>();
        for (var month = new DateOnly(2026, 1, 1); month.Year < 2028; month = month.AddMonths(1))
        {
            starts.Add(month.DayOfWeek);
            var days = MonthGrid.Days(month.Year, month.Month, firstDay);

            Assert.Equal(MonthGrid.Rows * MonthGrid.Columns, days.Count);
            Assert.Equal(firstDay, days[0].DayOfWeek);
            Assert.True(days[0] <= month && month.AddDays(-7) < days[0]);
            Assert.All(days.Zip(days.Skip(1)), pair => Assert.Equal(pair.First.AddDays(1), pair.Second));
            Assert.Equal(DateTime.DaysInMonth(month.Year, month.Month), days.Count(d => d.Month == month.Month));
        }
        Assert.Equal(7, starts.Count);
    }

    [Theory]
    [InlineData(2024, 29)]
    [InlineData(2000, 29)]
    [InlineData(1900, 28)]
    [InlineData(2026, 28)]
    public void February_FollowsLeapYears(int year, int days)
    {
        Assert.Equal(days, MonthGrid.Days(year, 2, DayOfWeek.Monday).Count(d => d.Month == 2));
    }

    [Fact]
    public void AMonthStartingOnTheFirstDayOfTheWeek_BeginsOnItsFirst()
    {
        // 2026-02-01 is a Sunday; 2026-06-01 is a Monday.
        Assert.Equal(new DateOnly(2026, 2, 1), MonthGrid.Days(2026, 2, DayOfWeek.Sunday)[0]);
        Assert.Equal(new DateOnly(2026, 6, 1), MonthGrid.Days(2026, 6, DayOfWeek.Monday)[0]);
        Assert.Equal(new DateOnly(2026, 1, 26), MonthGrid.Days(2026, 2, DayOfWeek.Monday)[0]);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, 2026, 12, 27, 53)]
    [InlineData(DayOfWeek.Monday, 2026, 12, 28, 53)]
    [InlineData(DayOfWeek.Monday, 2027, 1, 4, 1)]
    [InlineData(DayOfWeek.Sunday, 2027, 1, 3, 1)]
    public void WeekNumber_IsTheIsoWeekOfTheRowsThursday(DayOfWeek firstDay, int year, int month, int day, int week)
    {
        Assert.Equal(week, MonthGrid.WeekNumber(new DateOnly(year, month, day), firstDay));
    }

    [Fact]
    public void WeekdayOrder_StartsOnTheFirstDay()
    {
        Assert.Equal(DayOfWeek.Monday, MonthGrid.WeekdayOrder(DayOfWeek.Monday)[0]);
        Assert.Equal(DayOfWeek.Sunday, MonthGrid.WeekdayOrder(DayOfWeek.Monday)[6]);
        Assert.Equal(DayOfWeek.Saturday, MonthGrid.WeekdayOrder(DayOfWeek.Sunday)[6]);
    }
}
