using System;
using System.Linq;
using System.Text.Json;
using Chisel.Core.Weather;
using Xunit;

namespace Chisel.Core.Tests.Weather;

public class WeatherBackdropTests
{
    private const string Forecast = """
    {
      "current": { "time": "2000-01-01T09:15", "temperature_2m": 20.4, "apparent_temperature": 18.2, "weather_code": 2, "is_day": 0 },
      "hourly": { "time": ["2000-01-01T09:00"], "temperature_2m": [20], "weather_code": [2] },
      "daily": { "temperature_2m_max": [24], "temperature_2m_min": [12] }
    }
    """;

    // The widget's text colors: primary (App.axaml CurrentBlockOutlineColor) and the weather widget's own secondary grey.
    private static readonly uint[] TextColors = [0xF2F4F8, WeatherBackdrop.SecondaryText];

    private static double Luminance(uint rgb)
    {
        double Channel(int shift)
        {
            var c = ((rgb >> shift) & 255) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(16) + 0.7152 * Channel(8) + 0.0722 * Channel(0);
    }

    private static double Contrast(uint a, uint b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Theory]
    [InlineData(WeatherCondition.Clear, "clear")]
    [InlineData(WeatherCondition.PartlyCloudy, "partly-cloudy")]
    [InlineData(WeatherCondition.Cloudy, "cloudy")]
    [InlineData(WeatherCondition.Rain, "rain")]
    [InlineData(WeatherCondition.Snow, "snow")]
    [InlineData(WeatherCondition.Storm, "storm")]
    [InlineData(WeatherCondition.Fog, "fog")]
    public void Key_IsTheConditionByDayOrNight(WeatherCondition condition, string name)
    {
        Assert.Equal(name + "-day", WeatherBackdrop.Key(condition, true));
        Assert.Equal(name + "-night", WeatherBackdrop.Key(condition, false));
    }

    [Fact]
    public void EveryKeyHasAGradient_AndNightIsDarkerThanDay()
    {
        Assert.Equal(14, WeatherBackdrop.Gradients.Count);
        foreach (var condition in Enum.GetValues<WeatherCondition>())
        {
            var day = WeatherBackdrop.Gradients[WeatherBackdrop.Key(condition, true)];
            var night = WeatherBackdrop.Gradients[WeatherBackdrop.Key(condition, false)];
            Assert.True(Luminance(night.Top) < Luminance(day.Top));
            Assert.True(Luminance(night.Bottom) < Luminance(day.Bottom));
        }
    }

    // Day must look different from night by color (hue and saturation), not only by brightness.
    [Fact]
    public void DayTopStop_IsClearlyDifferentInColorFromNight()
    {
        foreach (var condition in Enum.GetValues<WeatherCondition>())
        {
            var day = WeatherBackdrop.Gradients[WeatherBackdrop.Key(condition, true)].Top;
            var night = WeatherBackdrop.Gradients[WeatherBackdrop.Key(condition, false)].Top;
            var distance = Math.Sqrt(new[] { 16, 8, 0 }.Sum(shift => Math.Pow(((day >> shift) & 255) - ((night >> shift) & 255), 2)));
            Assert.True(distance >= 7, $"{condition}: {distance:0.0}");
        }
    }

    [Fact]
    public void TextKeepsAtLeast4Point5ToOneContrast_OnEveryGradientStop()
    {
        foreach (var (key, gradient) in WeatherBackdrop.Gradients)
        {
            foreach (var stop in new[] { gradient.Top, gradient.Bottom })
            {
                foreach (var text in TextColors)
                {
                    Assert.True(Contrast(stop, text) >= 4.5, $"{key} {stop:X6} vs {text:X6}: {Contrast(stop, text):0.00}");
                }
            }
        }
    }

    // Particles at their maximum opacity (stacked twice, as when two overlap) must not break text contrast.
    [Fact]
    public void TextKeepsContrast_WhenMotionParticlesAreAtMaximumOpacity()
    {
        uint Over(uint particle, double alpha, uint background)
        {
            uint Mix(int shift) => (uint)Math.Round((((particle >> shift) & 255) * alpha) + (((background >> shift) & 255) * (1 - alpha)));
            return (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
        }

        foreach (var (key, gradient) in WeatherBackdrop.Gradients)
        {
            var style = WeatherMotion.Style(key);
            foreach (var stop in new[] { gradient.Top, gradient.Bottom })
            {
                var composite = Over(style.Color, style.MaxOpacity, Over(style.Color, style.MaxOpacity, stop));
                foreach (var text in TextColors)
                {
                    Assert.True(Contrast(composite, text) >= 4.5, $"{key} {stop:X6}: {Contrast(composite, text):0.00}");
                }
            }
        }
    }

    [Fact]
    public void Forecast_ReadsIsDay_AndAnOldSnapshotWithoutItReadsAsDay()
    {
        Assert.False(WeatherForecastParser.ParseForecast(Forecast).IsDay);
        Assert.True(WeatherForecastParser.ParseForecast(Forecast.Replace(", \"is_day\": 0", "")).IsDay);

        const string oldCache = """{"TemperatureC":20,"FeelsLikeC":18,"Condition":2,"HighC":24,"LowC":12,"Hourly":[]}""";
        Assert.True(JsonSerializer.Deserialize<WeatherSnapshot>(oldCache)!.IsDay);
    }
}
