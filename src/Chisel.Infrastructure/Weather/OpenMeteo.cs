using System.Globalization;
using Chisel.Core.Weather;

namespace Chisel.Infrastructure.Weather;

internal static class OpenMeteoHttp
{
    // The handler is only replaced by tests.
    public static HttpClient CreateClient(HttpMessageHandler? handler) =>
        new(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(10) };
}

public sealed class OpenMeteoWeatherService : IWeatherService
{
    private readonly IWeatherStore _store;
    private readonly HttpClient _client;

    public OpenMeteoWeatherService(IWeatherStore store, HttpMessageHandler? handler = null)
    {
        _store = store;
        _client = OpenMeteoHttp.CreateClient(handler);
    }

    public async Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var location = _store.GetLocation() ?? throw new WeatherLocationMissingException();
        var latitude = location.Latitude.ToString("0.##", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("0.##", CultureInfo.InvariantCulture);
        var url = "https://api.open-meteo.com/v1/forecast"
            + $"?latitude={latitude}&longitude={longitude}"
            + "&current=temperature_2m,apparent_temperature,weather_code,is_day"
            + "&hourly=temperature_2m,weather_code"
            + "&daily=temperature_2m_max,temperature_2m_min"
            + "&timezone=auto&forecast_days=2&temperature_unit=celsius";

        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return WeatherForecastParser.ParseForecast(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}

public sealed class OpenMeteoGeocoder : IWeatherGeocoder
{
    private readonly HttpClient _client;

    public OpenMeteoGeocoder(HttpMessageHandler? handler = null)
    {
        _client = OpenMeteoHttp.CreateClient(handler);
    }

    public async Task<IReadOnlyList<GeocodingResult>> SearchAsync(string name, CancellationToken cancellationToken = default)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name.Trim()) + "&count=5";
        using var response = await _client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return WeatherForecastParser.ParseGeocoding(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
