using Chisel.Core.Habits;

namespace Chisel.Core.Tests.Habits;

public class HabitLayoutTests
{
    [Theory]
    [InlineData(150, 100, HabitDetail.Summary)]
    [InlineData(400, 110, HabitDetail.Summary)] // too short for a list
    [InlineData(260, 200, HabitDetail.List)]
    [InlineData(360, 260, HabitDetail.Dots)]
    [InlineData(480, 400, HabitDetail.Grid)]
    public void TheSize_PicksTheDetail(double width, double height, HabitDetail detail)
    {
        Assert.Equal(detail, HabitLayout.For(width, height).Detail);
    }

    [Fact]
    public void TheThresholds_ApplyToTheRoomInsideThePadding()
    {
        var layout = HabitLayout.For(HabitLayout.DotsMinWidth + 20, HabitLayout.DotsMinHeight + 20);
        var inner = (W: HabitLayout.DotsMinWidth + 20 - 2 * layout.Pad, H: HabitLayout.DotsMinHeight + 20 - 2 * layout.Pad);
        Assert.Equal(inner.W >= HabitLayout.DotsMinWidth && inner.H >= HabitLayout.DotsMinHeight ? HabitDetail.Dots : HabitDetail.List, layout.Detail);
    }

    [Fact]
    public void Dots_ShowAWeek_AndTheGridFiveWeeks()
    {
        Assert.Equal(0, HabitLayout.For(260, 200).HistoryDays);
        Assert.Equal(7, HabitLayout.For(360, 260).HistoryDays);
        Assert.Equal(35, HabitLayout.For(480, 400).HistoryDays);
        Assert.True(HabitLayout.For(360, 260).ShowStreaks);
        Assert.False(HabitLayout.For(260, 200).ShowStreaks);
    }

    [Fact]
    public void TextScales_WithTheWidth_WithinLimits()
    {
        var small = HabitLayout.For(260, 400);
        var large = HabitLayout.For(520, 400);
        var huge = HabitLayout.For(4000, 3000);
        Assert.True(large.NameFont > small.NameFont);
        Assert.Equal(HabitLayout.BaseNameFont * HabitLayout.MaxScale, huge.NameFont, 3);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(60, 40)]
    [InlineData(150, 100)]
    [InlineData(170, 300)]
    [InlineData(330, 220)]
    [InlineData(380, 330)]
    [InlineData(900, 700)]
    public void NothingIsClipped(double width, double height)
    {
        var l = HabitLayout.For(width, height);
        var innerW = Math.Max(0, width - 2 * l.Pad);
        var innerH = Math.Max(0, height - 2 * l.Pad);
        Assert.True(l.RingSize >= 0 && l.RingSize <= Math.Min(innerW, innerH) + 0.001);
        if (l.Detail == HabitDetail.Summary)
        {
            // "30/30" is about 2.8 ems wide and must sit inside the ring.
            Assert.True(l.CountFont * 2.8 <= l.RingSize * 0.75 + 0.001);
        }
        else
        {
            Assert.True(l.SummaryHeight + l.RowHeight <= innerH);
            Assert.True(l.CheckSize + l.HistoryWidth + 3 * l.Gap + HabitLayout.MinNameWidth <= innerW + 0.001);
            Assert.True(l.HistoryHeight <= l.RowHeight);
        }
    }
}
