namespace Chisel.Core.Weather;

// Muted two-stop vertical gradient, as 0xRRGGBB.
public readonly record struct BackdropGradient(uint Top, uint Bottom);

// Weather backdrop data and logic, all in this file. Colors are kept muted so the widget's text stays readable
// (WeatherBackdropTests checks 4.5:1 contrast on every stop, with the motion particles on top).
public static class WeatherBackdrop
{
    // The weather widget's secondary text (hour labels, condition, footer): light grey, so backdrops can be brighter than
    // the app's dim grey would allow.
    public const uint SecondaryText = 0xC4C9D2;

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
        ["clear-day"] = new(0x1C4472, 0x183960),
        ["clear-night"] = new(0x141C30, 0x0E1422),
        ["partly-cloudy-day"] = new(0x2F4152, 0x283645),
        ["partly-cloudy-night"] = new(0x181E2E, 0x11161F),
        ["cloudy-day"] = new(0x393F49, 0x30353D),
        ["cloudy-night"] = new(0x1D2024, 0x14161A),
        ["rain-day"] = new(0x2F3D51, 0x283344),
        ["rain-night"] = new(0x181E27, 0x11151B),
        ["snow-day"] = new(0x38434C, 0x303941),
        ["snow-night"] = new(0x1E242B, 0x161A20),
        ["storm-day"] = new(0x2F3546, 0x282C3B),
        ["storm-night"] = new(0x15181D, 0x0E1013),
        ["fog-day"] = new(0x3B4042, 0x323639),
        ["fog-night"] = new(0x202327, 0x17191C),
    };
}
