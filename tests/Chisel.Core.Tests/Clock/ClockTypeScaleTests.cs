using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockTypeScaleTests
{
    [Fact]
    public void AtTheReferenceSize_ItIsTheBaseScale_MatchingTheRestOfTheApp()
    {
        var s = ClockTypeScale.For(260, 260);
        Assert.Equal(1.0, s.Factor);
        Assert.Equal(40, s.Display);
        Assert.Equal(16, s.Title);
        Assert.Equal(14, s.Body);
        Assert.Equal(12, s.Caption);
        Assert.False(s.Compact);
    }

    [Fact]
    public void Sizes_GrowAndShrinkTogether_KeepingTheirRelativeSizes()
    {
        var small = ClockTypeScale.For(200, 200);
        var large = ClockTypeScale.For(400, 400);
        Assert.True(small.Display < large.Display);
        Assert.True(small.Body < large.Body);
        Assert.True(small.Pad < large.Pad);
        foreach (var s in new[] { small, large })
        {
            Assert.True(s.Display > s.Title && s.Title > s.Body && s.Body > s.Caption - 0.001);
        }
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(5000, 5000)]
    public void TheFactor_IsKeptWithinLimits_SoTextStaysReadable(double width, double height)
    {
        var s = ClockTypeScale.For(width, height);
        Assert.InRange(s.Factor, ClockTypeScale.MinFactor, ClockTypeScale.MaxFactor);
        Assert.True(s.Body >= 12);
        Assert.True(s.Caption >= 10);
    }

    [Fact]
    public void TheScaleFollowsTheSmallerSide_ANarrowTallSlotIsNotBlownUp()
    {
        var narrow = ClockTypeScale.For(160, 600);
        var square = ClockTypeScale.For(160, 160);
        Assert.True(narrow.Display <= square.Display * 1.3);
        Assert.True(ClockTypeScale.For(600, 160).Display <= ClockTypeScale.For(160, 160).Display * 1.3);
    }

    [Theory]
    [InlineData(300, 140, true)]
    [InlineData(160, 300, true)]
    [InlineData(300, 300, false)]
    [InlineData(170, 150, false)]
    public void ASmallSlotIsCompact(double width, double height, bool compact) => Assert.Equal(compact, ClockTypeScale.For(width, height).Compact);

    [Fact]
    public void ButtonsAreSizedFromTheBodyText_NotFixed()
    {
        var small = ClockTypeScale.For(170, 200);
        var large = ClockTypeScale.For(400, 400);
        Assert.True(small.ButtonMinWidth < large.ButtonMinWidth);
        Assert.Equal(small.Body * 5, small.ButtonMinWidth);
        Assert.True(large.ButtonPadX > small.ButtonPadX - 0.001);
    }
}
