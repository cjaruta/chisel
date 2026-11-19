using Chisel.Core.Settings;
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
        Assert.Equal(expected, WeatherLayout.FormatTemperature(value, TemperatureUnit.Celsius));

    [Theory]
    [InlineData(0, 32)]
    [InlineData(-40, -40)]
    [InlineData(100, 212)]
    [InlineData(0.25, 32)]   // 32.45 rounds down
    [InlineData(0.3, 33)]    // 32.54 rounds up
    [InlineData(-0.3, 31)]   // 31.46 rounds down
    [InlineData(-0.4, 31)]   // 31.28 rounds down
    [InlineData(-17.5, 1)]   // exactly 0.5 rounds away from zero
    public void CelsiusToFahrenheit_IsRounded(double celsius, int expected) =>
        Assert.Equal(expected, WeatherLayout.CelsiusToFahrenheit(celsius));

    [Theory]
    [InlineData(21.4, "71°")]
    [InlineData(0, "32°")]
    [InlineData(-40, "-40°")]
    public void Temperature_InFahrenheit_IsConvertedAndRounded(double celsius, string expected) =>
        Assert.Equal(expected, WeatherLayout.FormatTemperature(celsius, TemperatureUnit.Fahrenheit));
}
