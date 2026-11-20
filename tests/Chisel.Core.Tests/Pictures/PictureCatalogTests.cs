using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

public class PictureCatalogTests
{
    private readonly FakeRepository _repository = new();
    private readonly FakeStore _store = new();

    private PictureAsset Add(char c)
    {
        var asset = PictureAsset.Create(new string(c, 64), $"{c}.png", ImageFormats.Png, 4, 4, 10, null, DateTimeOffset.UnixEpoch);
        _repository.AddAsset(asset);
        _store.Write(asset.StoredName, new byte[] { 1 });
        return asset;
    }

    [Fact]
    public void Save_KeepsOnlyFramesStillOnTheDashboard_AndDoesNotDeleteAnything()
    {
        var a = Add('a');
        var b = Add('b');
        var set = PictureFrameSet.Empty.AddPictures("f1", new[] { a.Id }).AddPictures("f2", new[] { b.Id });

        new PictureCatalog(_repository, _store).Save(set, new[] { "f1", "notes" });

        Assert.Equal(new[] { "f1" }, _repository.Frames.FrameIds);
        Assert.Equal(2, _repository.Stored.Count);
        Assert.Equal(2, _store.Files.Count);
    }

    [Fact]
    public void SaveAndSweep_DeletesPicturesNoFrameUses_RowAndFile()
    {
        var a = Add('a');
        var b = Add('b');
        var set = PictureFrameSet.Empty.AddPictures("f1", new[] { a.Id }).AddPictures("f2", new[] { b.Id });

        // f2 was deleted from the dashboard in this edit.
        new PictureCatalog(_repository, _store).SaveAndSweep(set, new[] { "f1" });

        Assert.Equal(new[] { a.Id }, _repository.Stored.Select(x => x.Id));
        Assert.True(_store.Exists(a.StoredName));
        Assert.False(_store.Exists(b.StoredName));
    }

    [Fact]
    public void Sweep_AtStartup_RemovesPicturesFromAnInterruptedEdit_AndKeepsTheSavedOnes()
    {
        var kept = Add('a');
        var orphan = Add('b');
        _repository.SaveFrames(PictureFrameSet.Empty.AddPictures("f1", new[] { kept.Id }));

        new PictureCatalog(_repository, _store).Sweep();

        Assert.Equal(new[] { kept.Id }, _repository.Stored.Select(x => x.Id));
        Assert.False(_store.Exists(orphan.StoredName));
    }

    [Fact]
    public void Sweep_AlsoRemovesFilesThatHaveNoRow()
    {
        var kept = Add('a');
        _store.Write(new string('c', 64) + ".png", new byte[] { 1 });
        _repository.SaveFrames(PictureFrameSet.Empty.AddPictures("f1", new[] { kept.Id }));

        new PictureCatalog(_repository, _store).Sweep(new[] { new string('c', 64) + ".png", kept.StoredName });

        Assert.True(_store.Exists(kept.StoredName));
        Assert.False(_store.Exists(new string('c', 64) + ".png"));
    }
}
