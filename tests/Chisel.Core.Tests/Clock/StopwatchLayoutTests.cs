using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

// The analog stopwatch's level of detail follows the diameter of its face, and every size follows it proportionally.
public class StopwatchLayoutTests
{
    private static readonly double[] Sizes = Enumerable.Range(16, 400).Select(d => (double)d).ToArray();

    [Theory]
    [InlineData(20, StopwatchDetail.Minimal, StopwatchNumbers.None, false)]
    [InlineData(95, StopwatchDetail.Minimal, StopwatchNumbers.None, false)]
    [InlineData(96, StopwatchDetail.Medium, StopwatchNumbers.Main, false)]
    [InlineData(139, StopwatchDetail.Medium, StopwatchNumbers.Main, false)]
    [InlineData(140, StopwatchDetail.Medium, StopwatchNumbers.All, false)]
    [InlineData(179, StopwatchDetail.Medium, StopwatchNumbers.All, false)]
    [InlineData(180, StopwatchDetail.Large, StopwatchNumbers.All, true)]
    [InlineData(400, StopwatchDetail.Large, StopwatchNumbers.All, true)]
    public void EachSize_ChoosesItsLevelOfDetail(double diameter, StopwatchDetail detail, StopwatchNumbers numbers, bool subDial)
    {
        var m = StopwatchLayout.For(diameter);
        Assert.Equal((detail, numbers, subDial), (m.Detail, m.Numbers, m.SubDial));
        Assert.Equal(detail >= StopwatchDetail.Medium, m.MinorTicks);
    }

    [Fact]
    public void NumbersStayUntilTheDialIsTooSmallForThem_ThinningFromTwelveToFourToNone()
    {
        Assert.True(StopwatchLayout.NumbersFrom <= 100);
        Assert.All(Sizes.Where(d => d >= StopwatchLayout.NumbersFrom), d => Assert.NotEqual(StopwatchNumbers.None, StopwatchLayout.For(d).Numbers));
        Assert.All(Sizes.Where(d => d < StopwatchLayout.NumbersFrom), d => Assert.Equal(StopwatchNumbers.None, StopwatchLayout.For(d).Numbers));
        Assert.All(Sizes.Where(d => d >= StopwatchLayout.AllNumbersFrom), d => Assert.Equal(StopwatchNumbers.All, StopwatchLayout.For(d).Numbers));
        Assert.All(Sizes.Where(d => d >= StopwatchLayout.NumbersFrom && d < StopwatchLayout.AllNumbersFrom), d => Assert.Equal(StopwatchNumbers.Main, StopwatchLayout.For(d).Numbers));
    }

    [Fact]
    public void TheThresholds_AreOrdered_AndEverythingOnlyEverGrowsWithTheDiameter()
    {
        Assert.True(StopwatchLayout.MinDiameter < StopwatchLayout.NumbersFrom);
        Assert.True(StopwatchLayout.NumbersFrom < StopwatchLayout.AllNumbersFrom);
        Assert.True(StopwatchLayout.AllNumbersFrom < StopwatchLayout.LargeFrom);

        var before = StopwatchLayout.For(Sizes[0]);
        foreach (var d in Sizes.Skip(1))
        {
            var m = StopwatchLayout.For(d);
            Assert.True(m.Detail >= before.Detail && m.Numbers >= before.Numbers, $"detail drops going up to {d}");
            Assert.True(m.MinorTicks || !before.MinorTicks);
            Assert.True(m.SubDial || !before.SubDial);
            Assert.True(m.NumberFont >= before.NumberFont && m.MajorTickWidth >= before.MajorTickWidth && m.MinorTickWidth >= before.MinorTickWidth, $"a size shrinks going up to {d}");
            Assert.True(m.MajorTickLength >= before.MajorTickLength && m.SecondLength >= before.SecondLength && m.SubRadius >= before.SubRadius);
            Assert.True(m.Stroke(3) >= before.Stroke(3) && m.Stroke(1) >= before.Stroke(1));
            before = m;
        }
    }

    [Fact]
    public void NumeralsAreProportionalToTheDiameter_WithinALegibleMinimumAndAMaximum()
    {
        foreach (var d in Sizes.Where(d => d >= StopwatchLayout.NumbersFrom))
        {
            var font = StopwatchLayout.For(d).NumberFont;
            Assert.InRange(font, StopwatchLayout.MinNumberFont, StopwatchLayout.MaxNumberFont);
            if (font > StopwatchLayout.MinNumberFont && font < StopwatchLayout.MaxNumberFont)
            {
                Assert.Equal(d * StopwatchLayout.NumberFontFraction, font, 6);
            }
        }
    }

    [Fact]
    public void ThePartsStayInsideTheDial_NumbersNeverReachTheTicks_OrTheSubDialTheNumbers()
    {
        foreach (var d in Sizes.Where(d => d >= StopwatchLayout.NumbersFrom))
        {
            var m = StopwatchLayout.For(d);
            var r = d / 2;
            Assert.True(m.NumberOuterEdge < m.TickInnerEdge, $"numbers touch the ticks at {d}");
            Assert.True(m.TickInnerEdge < r);
            Assert.True(m.NumberRadius - m.NumberFont * 0.75 > m.MajorTickWidth + m.SecondTail, $"numbers reach the hand pivot at {d}");
            Assert.True(m.SecondLength < r && m.SecondTail < m.NumberRadius);
            if (m.SubDial)
            {
                Assert.True(m.SubOffset + m.SubRadius < m.NumberRadius - m.NumberFont, $"the sub-dial reaches the numbers at {d}");
                Assert.True(m.SubOffset - m.SubRadius > 0);
            }
        }
    }

    [Fact]
    public void Strokes_NeverGoBelowOnePixel_AndAStyleWithNoneStaysWithoutOne()
    {
        foreach (var d in Sizes)
        {
            var m = StopwatchLayout.For(d);
            Assert.True(m.MinorTickWidth >= 1 && m.MajorTickWidth >= 1);
            Assert.True(m.Stroke(0.2) >= 1);
            Assert.Equal(0, m.Stroke(0));
        }
    }

    [Fact]
    public void TheDiameter_IsTheRoomLessTheEdgeGapOnBothSides()
    {
        Assert.Equal(88, StopwatchLayout.DiameterFor(100));
        Assert.Equal(0, StopwatchLayout.DiameterFor(5));
    }
}
