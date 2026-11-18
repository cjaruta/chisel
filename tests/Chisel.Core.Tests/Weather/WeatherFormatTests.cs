using Chisel.Core.Weather;
using Xunit;

namespace Chisel.Core.Tests.Weather;

public class WeatherFormatTests
{
    [Theory]
    [InlineData(169.9, WeatherSizeTier.Small)]
    [InlineData(170, WeatherSizeTier.Medium)]
    [InlineData(279.9, WeatherSizeTier.Medium)]
    [InlineData(280, WeatherSizeTier.Large)]
    public void SizeTier_ChangesAtTheNamedHeights(double height, WeatherSizeTier expected) =>
        Assert.Equal(expected, WeatherLayout.SizeTier(300, height));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(47, 0)]
    [InlineData(48, 1)]
    [InlineData(250, 5)]
    [InlineData(384, 8)]
    [InlineData(2000, 8)]
    public void HourlyCount_FitsAboutFortyEightPixelsEach_CappedAtEight(double width, int expected) =>
        Assert.Equal(expected, WeatherLayout.HourlyCount(width));

    [Theory]
    [InlineData(21.4, "21°")]
    [InlineData(21.5, "22°")]
    [InlineData(0, "0°")]
    [InlineData(-0.4, "0°")]
    [InlineData(-3.5, "-4°")]
    [InlineData(-12, "-12°")]
    public void Temperature_IsRoundedCelsiusWithADegreeSign(double value, string expected) =>
        Assert.Equal(expected, WeatherLayout.FormatTemperature(value));
}
