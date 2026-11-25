using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Startup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

// Saved widget data that can't be read is copied to damaged\<name>-<hash>.bad next to the database the first time it is
// read, so the widget can start empty without the next save silently throwing the only copy away.
public class DamagedDataTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-damaged-" + Guid.NewGuid().ToString("N"));
    private readonly ChiselDbContext _context;

    public DamagedDataTests()
    {
        Directory.CreateDirectory(_folder);
        _context = new ChiselDbContext(new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "chisel.db")}").Options);
        DatabaseInitializer.Initialize(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private string[] Kept(string prefix) =>
        Directory.Exists(Path.Combine(_folder, "damaged")) ? Directory.GetFiles(Path.Combine(_folder, "damaged"), prefix + "-*.bad") : [];

    [Fact]
    public void UnreadableHabits_AreKeptOnce_AndSurviveTheNextSave()
    {
        _context.Habits.Add(new HabitsEntity { Version = 1, Json = "{\"habits\":[{\"name\":\"Rea" });
        _context.SaveChanges();
        var repository = new HabitRepository(_context);

        Assert.Empty(repository.Get().Habits);
        repository.Get();
        repository.Save(HabitData.Empty);

        var kept = Assert.Single(Kept("habits"));
        Assert.Equal("{\"habits\":[{\"name\":\"Rea", File.ReadAllText(kept));
    }

    [Fact]
    public void AnUnreadableCountdown_IsKept()
    {
        _context.CountdownSettings.Add(new CountdownSettingsEntity { Version = 1, Json = "[1,2" });
        _context.SaveChanges();

        Assert.Equal(CountdownSettings.Default, new CountdownRepository(_context).Get());

        Assert.Equal("[1,2", File.ReadAllText(Assert.Single(Kept("countdown"))));
    }

    [Fact]
    public void AnUnreadableLayoutProfile_IsKept()
    {
        _context.LayoutProfiles.Add(new LayoutProfileEntity { Slot = 1, Name = "Work", LayoutJson = "{\"columns\":" });
        _context.SaveChanges();

        Assert.Empty(new LayoutProfileRepository(_context).GetAll());

        Assert.Single(Kept("layout-profile-1"));
    }

    [Fact]
    public void ReadableData_KeepsNothing()
    {
        var habits = new HabitRepository(_context);
        habits.Save(HabitData.Empty);
        habits.Get();
        new CountdownRepository(_context).Get();

        Assert.False(Directory.Exists(Path.Combine(_folder, "damaged")));
    }
}
