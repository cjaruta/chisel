using Chisel.Core.Calendar;

namespace Chisel.Core.Tests.Calendar;

public class CalendarLayoutTests
{
    [Theory]
    [InlineData(180, 160, CalendarSize.Compact)]
    [InlineData(CalendarLayout.StandardMinWidth - 1, 400, CalendarSize.Compact)]
    [InlineData(400, CalendarLayout.StandardMinHeight - 1, CalendarSize.Compact)]
    [InlineData(CalendarLayout.StandardMinWidth, CalendarLayout.StandardMinHeight, CalendarSize.Standard)]
    [InlineData(CalendarLayout.FullMinWidth - 1, 600, CalendarSize.Standard)]
    [InlineData(CalendarLayout.FullMinWidth, CalendarLayout.FullMinHeight, CalendarSize.Full)]
    public void SizeLevel_FollowsTheNamedThresholds(double width, double height, CalendarSize expected)
    {
        Assert.Equal(expected, CalendarLayout.SizeFor(width, height));
    }

    [Fact]
    public void Compact_ShowsOnlyTheMonthAndTheDates()
    {
        var layout = CalendarLayout.For(180, 160, weekNumbers: true);

        Assert.False(layout.ShowWeekdayNames);
        Assert.False(layout.ShowNavigation);
        Assert.False(layout.ShowTodayButton);
        Assert.False(layout.ShowYear);
        Assert.False(layout.ShowWeekNumbers);
    }

    [Fact]
    public void Standard_AddsWeekdayNamesAndArrows_Full_AddsTodayAndWeekNumbers()
    {
        var standard = CalendarLayout.For(260, 230, weekNumbers: true);
        Assert.True(standard.ShowWeekdayNames && standard.ShowNavigation && standard.ShowYear);
        Assert.False(standard.ShowTodayButton || standard.ShowWeekNumbers);

        var full = CalendarLayout.For(420, 360, weekNumbers: true);
        Assert.True(full.ShowTodayButton && full.ShowWeekNumbers);
        Assert.False(CalendarLayout.For(420, 360, weekNumbers: false).ShowWeekNumbers);
    }

    [Fact]
    public void FontsAndCells_GrowWithTheWidget_AndCellsFitIt()
    {
        var small = CalendarLayout.For(320, 280, weekNumbers: false);
        var large = CalendarLayout.For(640, 560, weekNumbers: false);

        Assert.True(large.DayFont > small.DayFont);
        Assert.True(large.TitleFont > small.TitleFont);
        Assert.True(large.CellHeight > small.CellHeight);
        foreach (var (layout, width, height) in new[] { (small, 320.0, 280.0), (large, 640.0, 560.0) })
        {
            Assert.True(2 * layout.Pad + MonthGrid.Columns * layout.CellWidth <= width + 0.01);
            Assert.True(2 * layout.Pad + layout.HeaderHeight + layout.WeekdayHeight + MonthGrid.Rows * layout.CellHeight <= height + 0.01);
            Assert.True(layout.DayFont < layout.CellHeight);
        }
    }

    [Fact]
    public void ATooShortWidget_IsLaidOutAtItsMinimum_SoTheDatesStayReadable()
    {
        var layout = CalendarLayout.For(400, 60, weekNumbers: false);

        Assert.Equal(CalendarSize.Compact, layout.Size);
        Assert.True(layout.CellHeight >= CalendarLayout.MinCellHeight - 0.01);
        Assert.True(layout.MinHeight > 60);
        Assert.True(CalendarLayout.For(400, layout.MinHeight, weekNumbers: false).MinHeight <= layout.MinHeight + 0.01);
    }

    [Fact]
    public void WeekNumbers_TakeAColumnOfTheWidth()
    {
        var with = CalendarLayout.For(420, 360, weekNumbers: true);
        var without = CalendarLayout.For(420, 360, weekNumbers: false);

        Assert.True(with.CellWidth < without.CellWidth);
        Assert.True(with.WeekNumberWidth > 0);
        Assert.Equal(0, without.WeekNumberWidth);
    }
}
