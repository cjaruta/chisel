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
        ["clear-day"] = new(0x1A283A, 0x151E2B),
        ["clear-night"] = new(0x141C30, 0x0E1422),
        ["partly-cloudy-day"] = new(0x202833, 0x192029),
        ["partly-cloudy-night"] = new(0x181E2E, 0x11161F),
        ["cloudy-day"] = new(0x22262A, 0x1B1E22),
        ["cloudy-night"] = new(0x1D2024, 0x14161A),
        ["rain-day"] = new(0x1E2530, 0x161B23),
        ["rain-night"] = new(0x181E27, 0x11151B),
        ["snow-day"] = new(0x23282F, 0x1C2128),
        ["snow-night"] = new(0x1E242B, 0x161A20),
        ["storm-day"] = new(0x1B1F26, 0x13161B),
        ["storm-night"] = new(0x15181D, 0x0E1013),
        ["fog-day"] = new(0x25282D, 0x1E2125),
        ["fog-night"] = new(0x202327, 0x17191C),
    };
}
