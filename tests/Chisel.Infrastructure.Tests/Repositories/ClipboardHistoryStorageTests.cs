using Chisel.Core.Clipboard;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ClipboardHistoryStorageTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-clip-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_folder, "clipboard-history.json");

    [Fact]
    public void Settings_DefaultToNotKeepingHistory()
    {
        using var context = _fixture.CreateContext();
        var settings = new ClipboardHistorySettingsRepository(context).Get();
        Assert.Equal(ClipboardHistorySettings.Default, settings);
        Assert.False(settings.KeepHistory);
    }

    [Fact]
    public void Settings_RoundTrip_AndOutOfRangeNumbersArePulledIn()
    {
        var settings = new ClipboardHistorySettings(true, 60, 2000);
        using (var context = _fixture.CreateContext())
        {
            var repository = new ClipboardHistorySettingsRepository(context);
            repository.Save(ClipboardHistorySettings.Default);
            repository.Save(settings);
        }
        using (var again = _fixture.CreateContext())
        {
            Assert.Equal(settings, new ClipboardHistorySettingsRepository(again).Get());
            Assert.Single(again.ClipboardHistorySettings);
            again.ClipboardHistorySettings.Single().MaxItems = 500;
            again.SaveChanges();
        }
        using var third = _fixture.CreateContext();
        Assert.Equal(ClipboardHistorySettings.MaxItemsLimit, new ClipboardHistorySettingsRepository(third).Get().MaxItems);
    }

    [Fact]
    public void FileStore_WithNoFile_LoadsNothing()
    {
        Assert.Empty(new ClipboardHistoryFileStore(FilePath).Load());
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void FileStore_RoundTrips_AndDeleteRemovesTheFile()
    {
        var store = new ClipboardHistoryFileStore(FilePath);
        var at = new DateTimeOffset(2000, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new ClipboardEntry(Guid.NewGuid(), "pinned words", at, true),
            new ClipboardEntry(Guid.NewGuid(), "line one\nline two", at.AddMinutes(-5), false),
        };

        store.Save(entries);
        Assert.Equal(entries, store.Load());
        Assert.Equal(FilePath, store.Location);

        store.Delete();
        Assert.False(File.Exists(FilePath));
        Assert.Empty(store.Load());
    }

    [Fact]
    public void FileStore_WithADamagedFile_LoadsNothing()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Empty(new ClipboardHistoryFileStore(FilePath).Load());
    }

    public void Dispose()
    {
        _fixture.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }
}
