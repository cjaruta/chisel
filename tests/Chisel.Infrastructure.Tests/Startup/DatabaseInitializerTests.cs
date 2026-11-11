using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Startup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Startup;

public class DatabaseInitializerTests
{
    private static (SqliteConnection Connection, DbContextOptions<ChiselDbContext> Options) CreateOptions()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite(connection)
            .Options;

        return (connection, options);
    }

    [Fact]
    public void Initialize_OnEmptyDatabase_MigratesAndSeedsDefaults()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;
        using var context = new ChiselDbContext(options);

        DatabaseInitializer.Initialize(context);

        var settings = new UserSettingsRepository(context).Get();
        Assert.NotNull(settings);
        Assert.Equal(new TimeOnly(6, 30), settings!.WakeTime);

        var schedule = new ScheduleRepository(context).Get();
        Assert.Equal(5, schedule.Blocks.Count);

        var markers = new MarkerRepository(context).GetAll();
        Assert.Equal(3, markers.Count);
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotReseed()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;
        using var context = new ChiselDbContext(options);

        DatabaseInitializer.Initialize(context);
        new UserSettingsRepository(context).Save(new Core.Settings.UserSettings("Changed", new TimeOnly(5, 0), new TimeOnly(21, 0)));

        DatabaseInitializer.Initialize(context);

        var settings = new UserSettingsRepository(context).Get();
        Assert.Equal("Changed", settings!.Name);
    }
}
