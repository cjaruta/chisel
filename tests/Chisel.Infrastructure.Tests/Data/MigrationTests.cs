using Chisel.Core.Schedule;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Chisel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class MigrationTests
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
    public void Migrate_OnEmptyDatabase_CreatesSchemaThatAcceptsData()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.Database.Migrate();
            context.Tasks.Add(new TaskEntity { Id = Guid.NewGuid(), Text = "Test", IsDone = false });
            context.SaveChanges();
        }

        using var readContext = new ChiselDbContext(options);
        Assert.Single(readContext.Tasks);
    }

    [Fact]
    public void AddBlockColor_GivesExistingBlocksTheDefaultColor()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261111192348_AddClockFormat");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO ScheduleBlocks (Id, Title, Start, \"End\", Position) VALUES ('6f1c2f4e-0000-0000-0000-000000000001', 'Focus', '08:00:00', '11:00:00', 0)");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        Assert.Equal(BlockColor.Default, Assert.Single(readContext.ScheduleBlocks).Color);
    }

    [Fact]
    public void Model_AfterMigrations_HasNoPendingChanges()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using var context = new ChiselDbContext(options);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}