using System.Text.Json;
using Chisel.Core.Weather;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class WeatherRepository : IWeatherStore
{
    private readonly ChiselDbContext _context;

    public WeatherRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public WeatherLocation? GetLocation() =>
        _context.WeatherLocations.FirstOrDefault() is { } e ? new WeatherLocation(e.Name, e.Latitude, e.Longitude) : null;

    public void SaveLocation(WeatherLocation? location)
    {
        _context.WeatherLocations.RemoveRange(_context.WeatherLocations);
        _context.WeatherCaches.RemoveRange(_context.WeatherCaches);
        if (location is not null)
        {
            _context.WeatherLocations.Add(new WeatherLocationEntity
            {
                Name = location.Name, Latitude = location.Latitude, Longitude = location.Longitude,
            });
        }
        _context.SaveChanges();
    }

    public CachedWeather? GetCache()
    {
        if (_context.WeatherCaches.FirstOrDefault() is not { } e)
        {
            return null;
        }
        try
        {
            var snapshot = JsonSerializer.Deserialize<WeatherSnapshot>(e.SnapshotJson);
            return snapshot is null ? null : new CachedWeather(snapshot, e.FetchedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void SaveCache(WeatherSnapshot snapshot, DateTimeOffset fetchedAt)
    {
        _context.WeatherCaches.RemoveRange(_context.WeatherCaches);
        _context.WeatherCaches.Add(new WeatherCacheEntity
        {
            FetchedAt = fetchedAt, SnapshotJson = JsonSerializer.Serialize(snapshot),
        });
        _context.SaveChanges();
    }
}
