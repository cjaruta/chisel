using Chisel.Core.Settings;
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
    public void AddTemperatureUnit_LoadsExistingSettingsAsCelsius()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261119003331_AddWeather");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO UserSettings (Id, Name, WakeTime, BedTime, ClockFormat, SchedulePalette) VALUES ('6f1c2f4e-0000-0000-0000-000000000003', 'Test User', '06:30:00', '23:00:00', 1, 'minimal')");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var saved = Assert.Single(readContext.UserSettings);
        Assert.Equal(TemperatureUnit.Celsius, new Chisel.Infrastructure.Repositories.UserSettingsRepository(readContext).Get()!.TemperatureUnit);
        Assert.Equal(1, saved.ClockFormat);
    }

    [Fact]
    public void AddTheme_LoadsExistingSettingsAsDefaultModeWithNoCustomAccent()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261119061321_AddMediaScrollVolume");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO UserSettings (Id, Name, WakeTime, BedTime, ClockFormat, SchedulePalette) VALUES ('6f1c2f4e-0000-0000-0000-000000000004', 'Test User', '06:30:00', '23:00:00', 1, 'minimal')");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var loaded = new Chisel.Infrastructure.Repositories.UserSettingsRepository(readContext).Get()!;
        Assert.Equal("default", loaded.ThemeMode);
        Assert.Null(loaded.AccentColor);
        Assert.Equal(ClockFormat.TwelveHour, loaded.ClockFormat);
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
    public void ExistingTwoColumnDatabase_LoadsExactlyAsBefore()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        // A two-column layout as the app has been saving it since columns and weights were added.
        using (var context = new ChiselDbContext(options))
        {
            context.Database.Migrate();
            context.Database.ExecuteSqlRaw(
                "INSERT INTO LayoutColumns (Position, WidthWeight) VALUES (0, 1.3), (1, 1.0);" +
                "INSERT INTO WidgetPlacements (Id, WidgetId, ColumnIndex, Position, HeightWeight) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000021', 'schedule', 0, 0, 1.0), " +
                "('6f1c2f4e-0000-0000-0000-000000000022', 'tasks', 1, 0, 1.05), " +
                "('6f1c2f4e-0000-0000-0000-000000000023', 'notes', 1, 1, 1.1), " +
                "('6f1c2f4e-0000-0000-0000-000000000024', 'links', 1, 2, 0.85);");
        }

        using var readContext = new ChiselDbContext(options);
        var layout = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get();
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1, links:0.85]", layout!.Describe());
    }

    [Fact]
    public void AddHiddenWidgets_LeavesAnExistingLayoutExactlyAsItWas()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261117183845_AddLayoutColumnsAndWeights");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO LayoutColumns (Position, WidthWeight) VALUES (0, 1.3), (1, 1.0);" +
                "INSERT INTO WidgetPlacements (Id, WidgetId, ColumnIndex, Position, HeightWeight) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000031', 'schedule', 0, 0, 1.0), " +
                "('6f1c2f4e-0000-0000-0000-000000000032', 'tasks', 1, 0, 1.05), " +
                "('6f1c2f4e-0000-0000-0000-000000000033', 'notes', 1, 1, 1.1), " +
                "('6f1c2f4e-0000-0000-0000-000000000034', 'links', 1, 2, 0.85);");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var layout = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get()!;
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1, links:0.85]", layout.Describe());
        Assert.Empty(readContext.HiddenWidgets);
    }

    [Fact]
    public void AddWidgetHeaderVisibility_LoadsAnExistingLayoutWithNoHeaderChoices()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261119003331_AddWeather");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO LayoutColumns (Position, WidthWeight) VALUES (0, 1.3), (1, 1.0);" +
                "INSERT INTO WidgetPlacements (Id, WidgetId, ColumnIndex, Position, HeightWeight) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000041', 'schedule', 0, 0, 1.0), " +
                "('6f1c2f4e-0000-0000-0000-000000000042', 'tasks', 1, 0, 1.05);");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var layout = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get()!;
        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.Null(w.ShowHeader));
    }

    [Fact]
    public void AddWidgetGroups_LoadsAnExistingLayoutAsPlainCards()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261119072456_AddMediaAlbumGradient");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO LayoutColumns (Position, WidthWeight) VALUES (0, 1.3), (1, 1.0);" +
                "INSERT INTO WidgetPlacements (Id, WidgetId, ColumnIndex, Position, HeightWeight, ShowHeader) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000051', 'schedule', 0, 0, 1.0, 0), " +
                "('6f1c2f4e-0000-0000-0000-000000000052', 'tasks', 1, 0, 1.05, NULL);");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var layout = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get()!;
        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.False(w.IsGroup));
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05]", layout.Describe());
    }

    [Fact]
    public void AddGroupViewMode_LoadsAnExistingGroupAsTabs()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261119183409_AddWidgetGroups");
            context.Database.ExecuteSqlRaw(
                "INSERT INTO LayoutColumns (Position, WidthWeight) VALUES (0, 1.0);" +
                "INSERT INTO WidgetPlacements (Id, WidgetId, ColumnIndex, Position, HeightWeight, ShowHeader, GroupMembers, ActiveTab) VALUES " +
                "('6f1c2f4e-0000-0000-0000-000000000061', 'notes', 0, 0, 1.0, NULL, 'notes:n;tasks:t', 1);");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var group = new Chisel.Infrastructure.Repositories.WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets[0];
        Assert.Equal(Chisel.Core.Layout.GroupViewMode.Tabs, group.ViewMode);
        Assert.Equal(new[] { "notes", "tasks" }, group.MemberIds);
        Assert.Equal(1, group.ActiveIndex);
    }

    [Fact]
    public void Model_AfterMigrations_HasNoPendingChanges()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using var context = new ChiselDbContext(options);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void AddTaskDueDate_KeepsExistingTasksWithTheirOrderAndNoDueDate()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261121002045_AddNotepadSettings");
            context.Database.ExecuteSqlRaw("INSERT INTO Tasks (Id, Text, IsDone, Position) VALUES ('6f1c2f4e-0000-0000-0000-000000000011', 'Second', 1, 1)");
            context.Database.ExecuteSqlRaw("INSERT INTO Tasks (Id, Text, IsDone, Position) VALUES ('6f1c2f4e-0000-0000-0000-000000000010', 'First', 0, 0)");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var tasks = new Chisel.Infrastructure.Repositories.TaskRepository(readContext).GetAll();
        Assert.Equal(new[] { "First", "Second" }, tasks.Select(t => t.Text).ToArray());
        Assert.Equal(new[] { false, true }, tasks.Select(t => t.IsDone).ToArray());
        Assert.All(tasks, t => Assert.Null(t.DueDate));
    }

    [Fact]
    public void AddQuickLinkPosition_KeepsEveryExistingLinkInItsOldOrder()
    {
        var (connection, options) = CreateOptions();
        using var _ = connection;

        using (var context = new ChiselDbContext(options))
        {
            context.GetService<IMigrator>().Migrate("20261123192602_AddTaskDueDate");
            context.Database.ExecuteSqlRaw("INSERT INTO QuickLinks (Id, Label, Url) VALUES ('6f1c2f4e-0000-0000-0000-000000000022', 'Mail', 'https://mail.example.com')");
            context.Database.ExecuteSqlRaw("INSERT INTO QuickLinks (Id, Label, Url) VALUES ('6f1c2f4e-0000-0000-0000-000000000021', 'Docs', 'https://docs.example.com')");
            context.Database.ExecuteSqlRaw("INSERT INTO QuickLinks (Id, Label, Url) VALUES ('6f1c2f4e-0000-0000-0000-000000000023', 'News', 'https://news.example.com')");
            context.Database.Migrate();
        }

        using var readContext = new ChiselDbContext(options);
        var links = new Chisel.Infrastructure.Repositories.QuickLinkRepository(readContext).GetAll();
        Assert.Equal(new[] { "Mail", "Docs", "News" }, links.Select(l => l.Label).ToArray());
        Assert.Equal(new[] { "https://mail.example.com", "https://docs.example.com", "https://news.example.com" }, links.Select(l => l.Url).ToArray());
    }
}
