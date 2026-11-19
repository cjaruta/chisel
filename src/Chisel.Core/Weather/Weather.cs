using Chisel.Core.Settings;

namespace Chisel.Core.Weather;

public enum WeatherCondition { Clear, PartlyCloudy, Cloudy, Rain, Snow, Storm, Fog }

public sealed record HourlyWeather(TimeOnly Time, WeatherCondition Condition, double TemperatureC);

// Temperatures are in Celsius.
public sealed record WeatherSnapshot(
    double TemperatureC, double FeelsLikeC, WeatherCondition Condition, double HighC, double LowC,
    IReadOnlyList<HourlyWeather> Hourly);

// Thrown by a service when no location is set up yet.
public sealed class WeatherLocationMissingException : Exception;

public interface IWeatherService
{
    Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

public enum WeatherSizeTier { Small, Medium, Large }

public static class WeatherLayout
{
    public const string Credit = "Weather data by Open-Meteo";
    public const double MediumMinHeight = 170;
    public const double LargeMinHeight = 280;
    public const double HourlyEntryWidth = 48;
    public const int MaxHourlyEntries = 8;

    public static WeatherSizeTier SizeTier(double contentWidth, double contentHeight) =>
        contentHeight >= LargeMinHeight ? WeatherSizeTier.Large
        : contentHeight >= MediumMinHeight ? WeatherSizeTier.Medium
        : WeatherSizeTier.Small;

    public static int HourlyCount(double contentWidth) =>
        Math.Clamp((int)(contentWidth / HourlyEntryWidth), 0, MaxHourlyEntries);

    public static int CelsiusToFahrenheit(double celsius) =>
        (int)Math.Round(celsius * 9 / 5 + 32, MidpointRounding.AwayFromZero);

    public static string FormatTemperature(double celsius, TemperatureUnit unit)
    {
        var rounded = unit == TemperatureUnit.Fahrenheit
            ? CelsiusToFahrenheit(celsius)
            : (int)Math.Round(celsius, MidpointRounding.AwayFromZero);
        return $"{rounded}°";
    }
}
