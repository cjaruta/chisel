using Chisel.Core.Weather;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WeatherRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private static readonly WeatherSnapshot Snapshot = new(20.4, 18.2, WeatherCondition.Rain, 24, 12,
        [new HourlyWeather(new TimeOnly(9, 0), WeatherCondition.Clear, 20), new HourlyWeather(new TimeOnly(10, 0), WeatherCondition.Storm, 21.5)]);

    private static readonly DateTimeOffset FetchedAt = new(2000, 1, 1, 9, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Empty_HasNoLocationAndNoCache()
    {
        using var context = _fixture.CreateContext();
        var repository = new WeatherRepository(context);

        Assert.Null(repository.GetLocation());
        Assert.Null(repository.GetCache());
    }

    [Fact]
    public void Location_AndCache_RoundTrip()
    {
        using (var context = _fixture.CreateContext())
        {
            var repository = new WeatherRepository(context);
            repository.SaveLocation(new WeatherLocation("Testville", 12.35, -45.68));
            repository.SaveCache(Snapshot, FetchedAt);
        }

        using var readContext = _fixture.CreateContext();
        var read = new WeatherRepository(readContext);
        Assert.Equal(new WeatherLocation("Testville", 12.35, -45.68), read.GetLocation());
        var cache = read.GetCache()!;
        Assert.Equal(FetchedAt, cache.FetchedAt);
        Assert.Equal(Snapshot.TemperatureC, cache.Snapshot.TemperatureC);
        Assert.Equal(Snapshot.Condition, cache.Snapshot.Condition);
        Assert.Equal(Snapshot.Hourly, cache.Snapshot.Hourly);
    }

    [Fact]
    public void SavingAgain_ReplacesTheCache_AndChangingOrClearingTheLocationDropsIt()
    {
        using var context = _fixture.CreateContext();
        var repository = new WeatherRepository(context);
        repository.SaveLocation(new WeatherLocation("Testville", 1, 2));
        repository.SaveCache(Snapshot, FetchedAt);
        repository.SaveCache(Snapshot with { TemperatureC = 5 }, FetchedAt.AddMinutes(20));
        Assert.Equal(5, repository.GetCache()!.Snapshot.TemperatureC);

        repository.SaveLocation(new WeatherLocation("Sampleton", 3, 4));
        Assert.Null(repository.GetCache());
        Assert.Equal("Sampleton", repository.GetLocation()!.Name);

        repository.SaveCache(Snapshot, FetchedAt);
        repository.SaveLocation(null);
        Assert.Null(repository.GetLocation());
        Assert.Null(repository.GetCache());
    }

    [Fact]
    public void ExistingDatabase_LoadsUnchanged_AfterTheWeatherMigration()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ChiselDbContext>().UseSqlite(connection).Options;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261117200732_AddHiddenWidgets");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO UserSettings (Id, Name, WakeTime, BedTime, ClockFormat, SchedulePalette) VALUES ('6f1c2f4e-0000-0000-0000-000000000003', 'Test User', '06:30:00', '23:00:00', 1, 'minimal')");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        Assert.Equal("Test User", Assert.Single(readContext.UserSettings).Name);
        var repository = new WeatherRepository(readContext);
        Assert.Null(repository.GetLocation());
        Assert.Null(repository.GetCache());
    }

    public void Dispose() => _fixture.Dispose();
}
