using System.Globalization;
using System.Text.Json;

namespace Chisel.Core.Weather;

// A saved place. Coordinates are rounded to 2 decimals (about 1 km) before they are stored or sent.
public sealed record WeatherLocation(string Name, double Latitude, double Longitude)
{
    public static WeatherLocation Create(string name, double latitude, double longitude) =>
        new(name, Math.Round(latitude, 2), Math.Round(longitude, 2));
}

public sealed record GeocodingResult(string Name, string? Region, string? Country, double Latitude, double Longitude)
{
    public string DisplayText => string.Join(", ", new[] { Name, Region, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public WeatherLocation ToLocation() => WeatherLocation.Create(Name, Latitude, Longitude);
}

public interface IWeatherGeocoder
{
    Task<IReadOnlyList<GeocodingResult>> SearchAsync(string name, CancellationToken cancellationToken = default);
}

// The saved location and the last snapshot. Only the local database implements this.
public interface IWeatherStore
{
    WeatherLocation? GetLocation();

    // Saving or clearing (null) the location also drops the cached snapshot.
    void SaveLocation(WeatherLocation? location);

    CachedWeather? GetCache();

    void SaveCache(WeatherSnapshot snapshot, DateTimeOffset fetchedAt);
}

public sealed record CachedWeather(WeatherSnapshot Snapshot, DateTimeOffset FetchedAt);

public static class WeatherCodes
{
    public static WeatherCondition ToCondition(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 or 2 => WeatherCondition.PartlyCloudy,
        3 => WeatherCondition.Cloudy,
        45 or 48 => WeatherCondition.Fog,
        >= 51 and <= 67 or >= 80 and <= 82 => WeatherCondition.Rain,
        >= 71 and <= 77 or 85 or 86 => WeatherCondition.Snow,
        >= 95 and <= 99 => WeatherCondition.Storm,
        _ => WeatherCondition.Cloudy,
    };
}

// Reads Open-Meteo JSON. Throws if the JSON isn't the expected shape.
public static class WeatherForecastParser
{
    public static WeatherSnapshot ParseForecast(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var current = root.GetProperty("current");
        var now = ParseLocal(current.GetProperty("time").GetString()!);
        var startOfHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        var hourly = root.GetProperty("hourly");
        var times = hourly.GetProperty("time").EnumerateArray().Select(t => ParseLocal(t.GetString()!)).ToList();
        var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToList();
        var codes = hourly.GetProperty("weather_code").EnumerateArray().ToList();
        var entries = new List<HourlyWeather>();
        for (var i = 0; i < times.Count; i++)
        {
            if (times[i] >= startOfHour)
            {
                entries.Add(new HourlyWeather(TimeOnly.FromDateTime(times[i]),
                    WeatherCodes.ToCondition(codes[i].GetInt32()), temps[i].GetDouble()));
            }
        }

        var daily = root.GetProperty("daily");
        return new WeatherSnapshot(
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("apparent_temperature").GetDouble(),
            WeatherCodes.ToCondition(current.GetProperty("weather_code").GetInt32()),
            daily.GetProperty("temperature_2m_max")[0].GetDouble(),
            daily.GetProperty("temperature_2m_min")[0].GetDouble(),
            entries);
    }

    public static IReadOnlyList<GeocodingResult> ParseGeocoding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results))
        {
            return [];
        }
        return results.EnumerateArray().Select(r => new GeocodingResult(
            r.GetProperty("name").GetString()!,
            r.TryGetProperty("admin1", out var region) ? region.GetString() : null,
            r.TryGetProperty("country", out var country) ? country.GetString() : null,
            r.GetProperty("latitude").GetDouble(),
            r.GetProperty("longitude").GetDouble())).ToList();
    }

    // "2000-01-01T09:15" is local time at the place, with no offset.
    private static DateTime ParseLocal(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
