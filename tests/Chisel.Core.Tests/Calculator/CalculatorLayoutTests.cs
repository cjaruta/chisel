using Chisel.Core.Calculator;
using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Calculator;

public class CalculatorLayoutTests
{
    [Fact]
    public void Settings_DefaultToBasicInDegrees()
    {
        Assert.False(CalculatorSettings.Default.Advanced);
        Assert.Equal(AngleMode.Degrees, CalculatorSettings.Default.Angle);
    }

    [Theory]
    [InlineData(700, 420)]
    [InlineData(520, 420)]
    [InlineData(900, 300)]
    public void WideEnough_ShowsTheAdvancedPanel_WhenItIsOn(double width, double height)
    {
        Assert.True(CalculatorLayout.For(width, height, advanced: true).ShowAdvanced);
        Assert.False(CalculatorLayout.For(width, height, advanced: false).ShowAdvanced);
    }

    [Theory]
    [InlineData(200, 420)]
    [InlineData(260, 520)]
    [InlineData(340, 420)]
    [InlineData(470, 420)]
    public void TooNarrow_FallsBackToTheBasicKeypad_EvenWhenAdvancedIsOn(double width, double height) =>
        Assert.False(CalculatorLayout.For(width, height, advanced: true).ShowAdvanced);

    [Fact]
    public void TheAdvancedPanel_NeverSquishesTheKeys()
    {
        for (double width = 150; width <= 1000; width += 5)
        {
            var layout = CalculatorLayout.For(width, 420, advanced: true);
            Assert.True(layout.KeyWidth >= CalculatorLayout.MinKeyWidth - 0.001 || !layout.ShowAdvanced && layout.Columns == 4, $"width {width}: key {layout.KeyWidth}");
            Assert.Equal(layout.ShowAdvanced ? 9 : 4, layout.Columns);
        }
    }

    [Fact]
    public void Keys_FillTheWidgetExactly()
    {
        var layout = CalculatorLayout.For(300, 480, advanced: false);
        var used = layout.KeyWidth * layout.Columns + layout.Gap * (layout.Columns - 1) + layout.Pad * 2;
        Assert.Equal(300, used, 3);
        var usedHeight = layout.KeyHeight * 5 + layout.Gap * 5 + layout.DisplayHeight + layout.Pad * 2;
        Assert.Equal(480, usedHeight, 3);
    }

    [Fact]
    public void Text_FollowsTheSharedTypeScale()
    {
        var layout = CalculatorLayout.For(300, 480, advanced: false);
        var scale = ClockTypeScale.For(300, 480);
        Assert.Equal(scale.Pad, layout.Pad);
        Assert.Equal(scale.Gap, layout.Gap);
        Assert.Equal(scale.Caption, layout.SecondaryFont);
        Assert.Equal(scale.Display, layout.ResultFont);
    }

    [Fact]
    public void Everything_GrowsWithTheWidget()
    {
        var small = CalculatorLayout.For(220, 340, advanced: false);
        var big = CalculatorLayout.For(420, 640, advanced: false);
        Assert.True(big.KeyWidth > small.KeyWidth);
        Assert.True(big.KeyHeight > small.KeyHeight);
        Assert.True(big.KeyFont > small.KeyFont);
        Assert.True(big.ResultFont > small.ResultFont);
    }

    [Theory]
    [InlineData(200, 300)]
    [InlineData(240, 420)]
    [InlineData(300, 600)]
    [InlineData(600, 380)]
    [InlineData(900, 700)]
    public void TextAlwaysFitsItsKey(double width, double height)
    {
        foreach (var advanced in new[] { false, true })
        {
            var layout = CalculatorLayout.For(width, height, advanced);
            Assert.True(layout.KeyFont <= layout.KeyHeight * 0.5, $"{width}x{height}");
            Assert.True(layout.KeyFont <= layout.KeyWidth * 0.5, $"{width}x{height}");
            Assert.True(layout.SmallKeyFont <= layout.KeyHeight * 0.5, $"{width}x{height}");
            Assert.True(layout.SmallKeyFont * 3 <= layout.KeyWidth, $"{width}x{height}");
            Assert.True(layout.ResultFont <= layout.DisplayHeight, $"{width}x{height}");
        }
    }
}
