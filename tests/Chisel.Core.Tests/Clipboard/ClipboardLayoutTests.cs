using Chisel.Core.Clipboard;

namespace Chisel.Core.Tests.Clipboard;

public class ClipboardLayoutTests
{
    [Theory]
    [InlineData(160, 60, ClipboardDetail.Latest)]
    [InlineData(400, 80, ClipboardDetail.Latest)]
    [InlineData(ClipboardLayout.ListMinWidth + 20, ClipboardLayout.ListMinHeight + 20, ClipboardDetail.List)]
    [InlineData(ClipboardLayout.FullMinWidth + 20, ClipboardLayout.ListMinHeight + 20, ClipboardDetail.List)]
    [InlineData(ClipboardLayout.FullMinWidth + 20, ClipboardLayout.FullMinHeight + 20, ClipboardDetail.Full)]
    public void PicksDetail_FromNamedThresholds(double width, double height, ClipboardDetail expected) =>
        Assert.Equal(expected, ClipboardLayout.For(width, height).Detail);

    [Fact]
    public void OnlyTheLargestSize_ShowsSearchAndTimes()
    {
        var list = ClipboardLayout.For(ClipboardLayout.ListMinWidth + 20, ClipboardLayout.ListMinHeight + 20);
        var full = ClipboardLayout.For(500, 500);
        Assert.False(list.ShowSearch || list.ShowTimes);
        Assert.True(list.ShowList);
        Assert.True(full.ShowSearch && full.ShowTimes && full.ShowList);
        Assert.False(ClipboardLayout.For(160, 60).ShowList);
    }

    [Fact]
    public void Sizes_ScaleWithTheWidth_WithinLimits()
    {
        var small = ClipboardLayout.For(320, 500);
        var big = ClipboardLayout.For(640, 500);
        Assert.True(big.Scale > small.Scale);
        Assert.Equal(big.PreviewFont / small.PreviewFont, big.RowHeight / small.RowHeight, 6);
        Assert.Equal(ClipboardLayout.MaxScale, ClipboardLayout.For(4000, 4000).Scale);
    }

    [Theory]
    [InlineData(120, 40)]
    [InlineData(150, 70)]
    [InlineData(250, 140)]
    [InlineData(300, 230)]
    [InlineData(800, 900)]
    public void Content_NeverOverflowsTheRoom(double width, double height)
    {
        var layout = ClipboardLayout.For(width, height);
        Assert.True(layout.MinContentHeight <= height - 2 * layout.Pad + 1e-6);
        Assert.True(layout.ButtonHeight + layout.IntroLines * layout.IntroLineHeight <= height - 2 * layout.Pad + 1e-6);
    }
}
