namespace Chisel.Core.Pictures;

// Saving the frames and cleaning up pictures nobody uses. Nothing is deleted before Done (SaveAndSweep) or the next
// start (Sweep), so Cancel in edit mode can bring everything back.
public sealed class PictureCatalog(IPictureRepository repository, IPictureFileStore files)
{
    public PictureFrameSet Load() => repository.LoadFrames();

    public PictureAsset? Get(Guid id) => repository.Get(id);

    public string PathOf(PictureAsset asset) => files.PathOf(asset.StoredName);

    // Saves the frames that are still on the dashboard (the frame ids), and nothing else changes.
    public PictureFrameSet Save(PictureFrameSet set, IEnumerable<string> dashboardIds)
    {
        var pruned = set.PrunedTo(dashboardIds);
        repository.SaveFrames(pruned);
        return pruned;
    }

    // Save, then delete every picture (row and file) that no saved frame uses.
    public PictureFrameSet SaveAndSweep(PictureFrameSet set, IEnumerable<string> dashboardIds)
    {
        var pruned = Save(set, dashboardIds);
        Sweep();
        return pruned;
    }

    // Deletes the pictures no saved frame uses. knownFiles: the file names in the pictures folder, when the caller
    // can list them; a file with no row is deleted too.
    public void Sweep(IEnumerable<string>? knownFiles = null)
    {
        var assets = repository.Assets();
        var used = repository.LoadFrames().UsedAssetIds;
        var unused = AssetSweep.Unused(assets.Select(a => a.Id), used).ToHashSet();
        foreach (var asset in assets.Where(a => unused.Contains(a.Id)))
        {
            files.Delete(asset.StoredName);
        }
        if (unused.Count > 0)
        {
            repository.DeleteAssets(unused);
        }

        var keep = assets.Where(a => !unused.Contains(a.Id)).Select(a => a.StoredName).ToHashSet();
        foreach (var name in (knownFiles ?? []).Where(n => !keep.Contains(n) && !n.EndsWith(".tmp", StringComparison.Ordinal)))
        {
            files.Delete(name);
        }
    }
}
