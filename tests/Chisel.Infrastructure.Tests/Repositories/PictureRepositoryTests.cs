using Microsoft.EntityFrameworkCore.Infrastructure;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Pictures;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class PictureRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private static PictureAsset Asset(char hashChar, string name = "a.png") =>
        PictureAsset.Create(new string(hashChar, 64), name, ImageFormats.Png, 40, 30, 1234, "https://example.com/" + name,
            new DateTimeOffset(2000, 1, 1, 9, 30, 0, TimeSpan.Zero));

    [Fact]
    public void AnAsset_RoundTrips_AndIsFoundByHashAndId()
    {
        var asset = Asset('a');
        using (var context = _fixture.CreateContext())
        {
            new PictureRepository(context).AddAsset(asset);
        }

        using var read = _fixture.CreateContext();
        var repository = new PictureRepository(read);
        Assert.Equal(asset, repository.Find(asset.Hash));
        Assert.Equal(asset, repository.Get(asset.Id));
        Assert.Equal(new[] { asset }, repository.Assets());
        Assert.Null(repository.Find(new string('f', 64)));
    }

    [Fact]
    public void TheSameBytesCannotBeStoredTwice()
    {
        using var context = _fixture.CreateContext();
        var repository = new PictureRepository(context);
        repository.AddAsset(Asset('a', "one.png"));

        Assert.ThrowsAny<DbUpdateException>(() => repository.AddAsset(Asset('a', "two.png")));
    }

    [Fact]
    public void Frames_RoundTrip_WithOptionsOrderAndCrops()
    {
        var one = Asset('a');
        var two = Asset('b', "b.jpg");
        var options = new FrameOptions(FitMode.Fit, true, SlideshowMode.Cycle, 600, SlideshowOrder.Shuffle, SlideshowTransition.None, 1);
        var set = PictureFrameSet.Empty
            .AddPictures("pictureframe:k1", new[] { one.Id, two.Id })
            .SetCrop("pictureframe:k1", 1, 0.25, 0.75, 2.5)
            .SetOptions("pictureframe:k1", options with { CurrentIndex = 1 })
            .AddPictures("pictureframe:k2", new[] { two.Id });
        using (var context = _fixture.CreateContext())
        {
            var repository = new PictureRepository(context);
            repository.AddAsset(one);
            repository.AddAsset(two);
            repository.SaveFrames(set);
        }

        using var read = _fixture.CreateContext();
        var loaded = new PictureRepository(read).LoadFrames();

        var frame = loaded.Get("pictureframe:k1");
        Assert.Equal(options, frame.Options);
        Assert.Equal(new[] { one.Id, two.Id }, frame.Pictures.Select(p => p.AssetId));
        Assert.Equal((0.25, 0.75, 2.5), (frame.Pictures[1].FocalX, frame.Pictures[1].FocalY, frame.Pictures[1].Zoom));
        Assert.Equal(new[] { two.Id }, loaded.Get("pictureframe:k2").Pictures.Select(p => p.AssetId));
    }

    [Fact]
    public void SavingFrames_ReplacesWhatWasThere()
    {
        var asset = Asset('a');
        using var context = _fixture.CreateContext();
        var repository = new PictureRepository(context);
        repository.AddAsset(asset);
        repository.SaveFrames(PictureFrameSet.Empty.AddPictures("f1", new[] { asset.Id }).AddPictures("f2", new[] { asset.Id }));

        repository.SaveFrames(PictureFrameSet.Empty.AddPictures("f2", new[] { asset.Id }));

        var loaded = repository.LoadFrames();
        Assert.Equal(new[] { "f2" }, loaded.FrameIds);
    }

    [Fact]
    public void DeletingAssets_RemovesTheirRowsOnly()
    {
        using var context = _fixture.CreateContext();
        var repository = new PictureRepository(context);
        var keep = Asset('a');
        var drop = Asset('b');
        repository.AddAsset(keep);
        repository.AddAsset(drop);

        repository.DeleteAssets(new[] { drop.Id });

        Assert.Equal(new[] { keep }, repository.Assets());
    }

    [Fact]
    public void ThePictureSettings_RoundTrip_AndOldSettingsRowsGetTheDefaults()
    {
        using (var context = _fixture.CreateContext())
        {
            new UserSettingsRepository(context).Save(new UserSettings("Test User", new TimeOnly(7, 0), new TimeOnly(23, 0))
                .WithPictureKeepOriginals(false).WithPictureMultiDrop(MultiDropMode.OneFramePerPicture));
        }

        using var read = _fixture.CreateContext();
        var settings = new UserSettingsRepository(read).Get()!;
        Assert.False(settings.PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneFramePerPicture, settings.PictureMultiDrop);
    }

    public void Dispose() => _fixture.Dispose();
}

public class PictureFileStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-pictures-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Fact]
    public void Write_CreatesThePicturesFolder_AndTheFileIsReadBackFromItsPath()
    {
        var store = new PictureFileStore(Path.Combine(_folder, "pictures"));
        var name = new string('a', 64) + ".png";

        Assert.False(store.Exists(name));
        store.Write(name, new byte[] { 1, 2, 3 });

        Assert.True(store.Exists(name));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(store.PathOf(name)));
        Assert.StartsWith(_folder, store.PathOf(name));
    }

    [Fact]
    public void Delete_RemovesTheFile_AndIsQuietWhenItIsAlreadyGone()
    {
        var store = new PictureFileStore(Path.Combine(_folder, "pictures"));
        var name = new string('b', 64) + ".gif";
        store.Write(name, new byte[] { 9 });

        store.Delete(name);
        store.Delete(name);

        Assert.False(store.Exists(name));
    }

    [Theory]
    [InlineData("..\\evil.png")]
    [InlineData("../evil.png")]
    [InlineData("a/b.png")]
    [InlineData("")]
    public void NamesThatLeaveTheFolder_AreRefused(string name)
    {
        var store = new PictureFileStore(Path.Combine(_folder, "pictures"));

        Assert.Throws<ArgumentException>(() => store.Write(name, new byte[] { 1 }));
        Assert.Throws<ArgumentException>(() => store.PathOf(name));
    }

    [Fact]
    public void OldDatabases_GainThePictureTablesAndSettings_OnMigrate()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ChiselDbContext>().UseSqlite(connection).Options;
        using var context = new ChiselDbContext(options);
        var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();

        // Everything before the picture frame migration, with a settings row saved then.
        var before = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>().Migrations.Keys
            .OrderBy(k => k).Last(k => !k.EndsWith("AddPictureFrames"));
        migrator.Migrate(before);
        context.Database.ExecuteSqlRaw(
            "INSERT INTO UserSettings (Id, Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, MediaAlbumGradient, MediaAnimateGradient, ThemeMode) " +
            "VALUES ('11111111-1111-1111-1111-111111111111', 'Test User', '07:00:00', '23:00:00', 0, 'minimal', 0, 1, 1, 0, 1, 1, 'default')");

        context.Database.Migrate();

        var settings = new UserSettingsRepository(context).Get()!;
        Assert.True(settings.PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneSlideshow, settings.PictureMultiDrop);
        Assert.Empty(new PictureRepository(context).Assets());
    }
}
