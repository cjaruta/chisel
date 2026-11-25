using Chisel.Core.Focus;

namespace Chisel.Core.Tests.Focus;

public class FocusLayoutTests
{
    [Theory]
    [InlineData(180, 40, FocusDetail.Minimal, false)]
    [InlineData(100, 100, FocusDetail.Minimal, true)]
    [InlineData(160, 160, FocusDetail.Medium, true)]
    [InlineData(320, 400, FocusDetail.Full, true)]
    public void TheLevelOfDetail_FollowsTheSize(double width, double height, FocusDetail detail, bool ring)
    {
        var layout = FocusLayout.For(width, height, FocusFace.Ring, longTime: false, dots: 4);

        Assert.Equal((detail, ring), (layout.Detail, layout.ShowRing));
        Assert.Equal(detail >= FocusDetail.Medium, layout.ShowLabel && layout.ShowDots);
        Assert.Equal(detail == FocusDetail.Full, layout.ShowControls);
    }

    [Fact]
    public void TheDigitalFace_NeverDrawsTheRing_AndUsesTheRoomForBiggerDigits()
    {
        var ring = FocusLayout.For(320, 400, FocusFace.Ring, false, 4);
        var digital = FocusLayout.For(320, 400, FocusFace.Digital, false, 4);

        Assert.False(digital.ShowRing);
        Assert.True(digital.TimeFont > ring.TimeFont);
    }

    [Fact]
    public void EverythingScalesWithTheSize()
    {
        var small = FocusLayout.For(200, 240, FocusFace.Ring, false, 4);
        var large = FocusLayout.For(500, 600, FocusFace.Ring, false, 4);

        Assert.True(large.Diameter > small.Diameter);
        Assert.True(large.RingThickness > small.RingThickness);
        Assert.True(large.TimeFont > small.TimeFont);
        Assert.True(large.LabelFont >= small.LabelFont);
        Assert.True(large.DotSize >= small.DotSize);
    }

    [Fact]
    public void NothingIsEverClipped()
    {
        foreach (var face in new[] { FocusFace.Ring, FocusFace.Digital })
        foreach (var longTime in new[] { false, true })
        foreach (var dots in new[] { 1, 4, 12 })
        for (var w = 20; w <= 900; w += 23)
        for (var h = 20; h <= 900; h += 29)
        {
            var l = FocusLayout.For(w, h, face, longTime, dots);
            var at = $"{face} {w}x{h} long={longTime} dots={dots}";
            var innerW = w - 2 * l.Pad;
            var innerH = h - 2 * l.Pad;

            // The dial and the rows under it fit the height; the dial fits the width.
            Assert.True(l.Diameter <= innerW + 0.01, at);
            Assert.True(l.Diameter + l.BelowHeight <= innerH + 0.01, at);
            // Inside the dial: the time, the label above it and the dots under it, across and down.
            var room = l.TextRoom;
            Assert.True(l.TimeFont * (longTime ? FocusLayout.LongTimeEms : FocusLayout.ShortTimeEms) <= room + 0.01, at);
            var stack = l.TimeFont * FocusLayout.LineHeight + (l.ShowLabel ? l.LabelFont * FocusLayout.LineHeight : 0) + (l.ShowDots ? l.DotSize * 2 : 0);
            Assert.True(stack <= (l.ShowRing ? room : l.Diameter) + 0.01, at);
            if (l.ShowDots)
            {
                Assert.True(dots * l.DotSize + (dots - 1) * l.DotGap <= room + 0.01, at);
            }
        }
    }
}
