namespace Chisel.Core.Weather;

// Muted two-stop vertical gradient, as 0xRRGGBB.
public readonly record struct BackdropGradient(uint Top, uint Bottom);

// Weather backdrop data and logic, all in this file. Colors are kept dark and low saturation so the widget's
// text stays readable (WeatherBackdropTests checks 4.5:1 contrast on every stop).
public static class WeatherBackdrop
{
    public static string Key(WeatherCondition condition, bool isDay) =>
        (condition switch
        {
            WeatherCondition.Clear => "clear",
            WeatherCondition.PartlyCloudy => "partly-cloudy",
            WeatherCondition.Cloudy => "cloudy",
            WeatherCondition.Rain => "rain",
            WeatherCondition.Snow => "snow",
            WeatherCondition.Storm => "storm",
            _ => "fog",
        }) + (isDay ? "-day" : "-night");

    public static readonly IReadOnlyDictionary<string, BackdropGradient> Gradients = new Dictionary<string, BackdropGradient>
    {
        ["clear-day"] = new(0x0E2846, 0x09223F),
        ["clear-night"] = new(0x141C30, 0x0E1422),
        ["partly-cloudy-day"] = new(0x1C2837, 0x162331),
        ["partly-cloudy-night"] = new(0x181E2E, 0x11161F),
        ["cloudy-day"] = new(0x222830, 0x1C222B),
        ["cloudy-night"] = new(0x1D2024, 0x14161A),
        ["rain-day"] = new(0x1D2839, 0x172232),
        ["rain-night"] = new(0x181E27, 0x11151B),
        ["snow-day"] = new(0x1F2931, 0x19232B),
        ["snow-night"] = new(0x1E242B, 0x161A20),
        ["storm-day"] = new(0x232736, 0x1C2130),
        ["storm-night"] = new(0x15181D, 0x0E1013),
        ["fog-day"] = new(0x23282B, 0x1C2326),
        ["fog-night"] = new(0x202327, 0x17191C),
    };
}
