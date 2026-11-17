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
    public void AddSchedulePalette_GivesExistingSettingsTheMinimalPalette()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261117175519_AddBlockColor");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO UserSettings (Id, Name, WakeTime, BedTime, ClockFormat) VALUES ('6f1c2f4e-0000-0000-0000-000000000002', 'Test User', '06:30:00', '23:00:00', 1)");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var saved = Assert.Single(readContext.UserSettings);
        Assert.Equal(Chisel.Core.Settings.UserSettings.DefaultSchedulePalette, saved.SchedulePalette);
        Assert.Equal(1, saved.ClockFormat);
    }

    [Fact]
    public void AddLayoutColumnsAndWeights_MovesOldSideAndMainRowsIntoColumns()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261117181523_AddSchedulePalette");
            // Old model: Column 0 was Side (the right-hand list), 1 was Main.
            context.Database.ExecuteSqlRaw(
                "INSERT INTO WidgetPlacements (Id, WidgetId, Column, Position) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000011', 'notes', 0, 1), " +
                "('6f1c2f4e-0000-0000-0000-000000000012', 'tasks', 0, 0), " +
                "('6f1c2f4e-0000-0000-0000-000000000013', 'schedule', 1, 0)");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var layout = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get();
        Assert.Equal("1.3[schedule:1] | 1[tasks:1, notes:1]", layout!.Describe());
    }

    [Fact]
    public void AddLayoutColumnsAndWeights_OnAnEmptyTable_LeavesNoLayout()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using var context = new ChiselDbContext(options);
        context.Database.Migrate();

        Assert.Null(new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(context).Get());
        Assert.Empty(context.LayoutColumns);
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