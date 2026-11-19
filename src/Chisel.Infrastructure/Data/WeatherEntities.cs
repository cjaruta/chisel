namespace Chisel.Infrastructure.Data;

// The one saved weather location (Id is always 1).
public class WeatherLocationEntity
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

// The last snapshot fetched for that location (Id is always 1), stored as JSON.
public class WeatherCacheEntity
{
    public int Id { get; set; } = 1;
    public DateTimeOffset FetchedAt { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
}
