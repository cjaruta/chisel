using Chisel.Core.Countdown;

namespace Chisel.Core.Tests.Countdown;

public class CountdownLayoutTests
{
    [Theory]
    [InlineData(180, 300, CountdownSize.Single)]
    [InlineData(CountdownLayout.ListMinWidth - 1, 600, CountdownSize.Single)]
    [InlineData(600, CountdownLayout.ListMinHeight - 1, CountdownSize.Single)]
    [InlineData(CountdownLayout.ListMinWidth, CountdownLayout.ListMinHeight, CountdownSize.List)]
    [InlineData(CountdownLayout.DetailedMinWidth - 1, 600, CountdownSize.List)]
    [InlineData(600, CountdownLayout.DetailedMinHeight - 1, CountdownSize.List)]
    [InlineData(CountdownLayout.DetailedMinWidth, CountdownLayout.DetailedMinHeight, CountdownSize.Detailed)]
    public void SizeFor_UsesTheNamedThresholds(double width, double height, CountdownSize expected) =>
        Assert.Equal(expected, CountdownLayout.SizeFor(width, height));

    [Fact]
    public void Single_ShowsOnlyTheNearestEvent()
    {
        var layout = CountdownLayout.For(180, 140, otherEvents: 5);
        Assert.Equal(0, layout.VisibleRows);
        Assert.False(layout.ShowMore);
        Assert.Equal(0, layout.ProgressHeight);
    }

    [Fact]
    public void List_ShowsRowsWithoutProgress_Detailed_AddsProgress()
    {
        var list = CountdownLayout.For(230, 220, otherEvents: 2);
        Assert.Equal(CountdownSize.List, list.Size);
        Assert.Equal(2, list.VisibleRows);
        Assert.Equal(0, list.ProgressHeight);

        var detailed = CountdownLayout.For(400, 400, otherEvents: 2);
        Assert.True(detailed.ProgressHeight > 0);
    }

    [Fact]
    public void RowsThatDoNotFit_AreCountedInAMoreLine()
    {
        var layout = CountdownLayout.For(300, 260, otherEvents: 40);
        Assert.True(layout.VisibleRows < 40);
        Assert.True(layout.ShowMore);
    }

    [Theory]
    [InlineData(120, 90, 3)]
    [InlineData(180, 140, 3)]
    [InlineData(200, 170, 10)]
    [InlineData(259, 600, 10)]
    [InlineData(260, 250, 10)]
    [InlineData(400, 400, 1)]
    [InlineData(600, 900, 50)]
    [InlineData(1100, 300, 50)]
    public void Everything_FitsInsideTheWidget(double width, double height, int others)
    {
        var layout = CountdownLayout.For(width, height, others);
        var used = 2 * layout.Pad + layout.HeroHeight + (layout.VisibleRows > 0 || layout.ShowMore ? layout.Gap : 0)
            + layout.VisibleRows * layout.RowHeight + (layout.ShowMore ? layout.MoreHeight : 0);
        Assert.True(used <= height + 0.01, $"{used} > {height}");
        Assert.True(layout.HeroNumberFont * 0.6 * 4 <= width, "four digits fit across");
    }

    [Fact]
    public void Fonts_GrowWithTheWidget()
    {
        var small = CountdownLayout.For(150, 120, 0);
        var large = CountdownLayout.For(190, 400, 0);
        Assert.True(large.HeroNumberFont > small.HeroNumberFont);
        Assert.True(large.HeroLabelFont >= small.HeroLabelFont);

        var listSmall = CountdownLayout.For(220, 200, 3);
        var listLarge = CountdownLayout.For(600, 700, 3);
        Assert.True(listLarge.RowNameFont > listSmall.RowNameFont);
    }
}
