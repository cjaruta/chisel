using Chisel.Core.Notes;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Startup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.Infrastructure.Tests.Startup;

public class DatabaseRecoveryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-recovery-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 12, 30, 0, TimeSpan.Zero));
    private readonly List<ChiselDbContext> _opened = [];

    public DatabaseRecoveryTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _opened.ForEach(c => c.Dispose());
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private string DbPath => Path.Combine(_folder, "chisel.db");

    private ChiselDbContext Open()
    {
        var context = new ChiselDbContext(new DbContextOptionsBuilder<ChiselDbContext>().UseSqlite($"Data Source={DbPath}").Options);
        _opened.Add(context);
        DatabaseInitializer.Initialize(context);
        return context;
    }

    private string[] BadFiles() => Directory.GetFiles(_folder, "*.bad").Select(Path.GetFileName).ToArray()!;

    [Fact]
    public void AFileThatIsNotADatabase_IsKeptAsBad_AndAFreshOneStarts()
    {
        byte[] junk = [.. Enumerable.Range(0, 5000).Select(i => (byte)(i * 7))];
        File.WriteAllBytes(DbPath, junk);

        var result = DatabaseRecovery.Open(DbPath, Open, _time);

        Assert.Equal(["chisel.db.2000-01-01-123000.bad"], BadFiles());
        Assert.Equal(junk, File.ReadAllBytes(Path.Combine(_folder, "chisel.db.2000-01-01-123000.bad")));
        Assert.Equal("chisel.db.2000-01-01-123000.bad", result.KeptAs);
        Assert.NotNull(new UserSettingsRepository(result.Value).Get());
    }

    [Fact]
    public void ATruncatedDatabase_IsKeptAsBad_AndAFreshOneStarts()
    {
        var first = Open();
        var repository = new NoteRepository(first, _time);
        for (var i = 0; i < 200; i++)
        {
            repository.Add(new Note(_time, "Test", new string('x', 2000)));
        }
        first.Dispose();
        SqliteConnection.ClearAllPools();
        using (var stream = new FileStream(DbPath, FileMode.Open))
        {
            stream.SetLength(stream.Length / 3);
        }

        var result = DatabaseRecovery.Open(DbPath, Open, _time);

        Assert.Single(BadFiles());
        Assert.Empty(new NoteRepository(result.Value, _time).GetAll());
    }

    [Fact]
    public void AnEmptyFile_IsAFreshDatabase_NotDamage()
    {
        File.WriteAllBytes(DbPath, []);

        var result = DatabaseRecovery.Open(DbPath, Open, _time);

        Assert.Null(result.KeptAs);
        Assert.Empty(BadFiles());
    }

    // Anything other than a damaged file (locked by another program, no permission, a bug) is not fixed by starting over,
    // so the file is left alone and the error goes on to the error window.
    [Fact]
    public void OtherErrors_LeaveTheFileAlone()
    {
        File.WriteAllBytes(DbPath, []);

        Assert.Throws<InvalidOperationException>(() =>
            DatabaseRecovery.Open<ChiselDbContext>(DbPath, () => throw new InvalidOperationException("locked"), _time));

        Assert.True(File.Exists(DbPath));
        Assert.Empty(BadFiles());
    }
}
