using System;
using Chisel.Core.Weather;

namespace Chisel.App.Services;

// Keeps the weather location and cache in memory only; used where no database is wired in.
public sealed class MemoryWeatherStore : IWeatherStore
{
    private WeatherLocation? _location;
    private CachedWeather? _cache;

    public WeatherLocation? GetLocation() => _location;

    public void SaveLocation(WeatherLocation? location)
    {
        _location = location;
        _cache = null;
    }

    public CachedWeather? GetCache() => _cache;

    public void SaveCache(WeatherSnapshot snapshot, DateTimeOffset fetchedAt) => _cache = new CachedWeather(snapshot, fetchedAt);
}

// Finds nothing; the default when no real geocoder is wired in.
public sealed class NoGeocoder : IWeatherGeocoder
{
    public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<GeocodingResult>> SearchAsync(
        string name, System.Threading.CancellationToken cancellationToken = default) =>
        System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<GeocodingResult>>([]);
}
