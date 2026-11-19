using Chisel.Core.Pictures;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class PictureRepository(ChiselDbContext context) : IPictureRepository
{
    public IReadOnlyList<PictureAsset> Assets() => context.PictureAssets.AsEnumerable().Select(ToDomain).OrderBy(a => a.AddedAt).ToList();

    public PictureAsset? Find(string hash) => context.PictureAssets.FirstOrDefault(a => a.Hash == hash) is { } e ? ToDomain(e) : null;

    public PictureAsset? Get(Guid id) => context.PictureAssets.Find(id) is { } e ? ToDomain(e) : null;

    public void AddAsset(PictureAsset asset)
    {
        context.PictureAssets.Add(new PictureAssetEntity
        {
            Id = asset.Id, Hash = asset.Hash, FileName = asset.FileName, MimeType = asset.MimeType, Width = asset.Width,
            Height = asset.Height, Bytes = asset.Bytes, SourceUrl = asset.SourceUrl, AddedAt = asset.AddedAt,
        });
        try
        {
            context.SaveChanges();
        }
        catch
        {
            // A failed save must not stay pending and break the next one.
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public void DeleteAssets(IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        context.PictureAssets.RemoveRange(context.PictureAssets.Where(a => list.Contains(a.Id)));
        context.SaveChanges();
    }

    // Replace-all: what is saved is exactly the set.
    public void SaveFrames(PictureFrameSet set)
    {
        context.FramePictures.RemoveRange(context.FramePictures);
        context.FrameOptions.RemoveRange(context.FrameOptions);
        foreach (var frameId in set.FrameIds)
        {
            var content = set.Get(frameId);
            var o = content.Options;
            context.FrameOptions.Add(new FrameOptionsEntity
            {
                FrameId = frameId, Fit = (int)o.Fit, BlurFill = o.BlurFill, Mode = (int)o.Mode, IntervalSeconds = o.IntervalSeconds,
                Order = (int)o.Order, Transition = (int)o.Transition, CurrentIndex = o.CurrentIndex,
            });
            for (var i = 0; i < content.Pictures.Count; i++)
            {
                var p = content.Pictures[i];
                context.FramePictures.Add(new FramePictureEntity
                {
                    Id = Guid.NewGuid(), FrameId = frameId, AssetId = p.AssetId, Position = i, FocalX = p.FocalX, FocalY = p.FocalY, Zoom = p.Zoom,
                });
            }
        }
        context.SaveChanges();
    }

    public PictureFrameSet LoadFrames()
    {
        var pictures = context.FramePictures.AsEnumerable().GroupBy(p => p.FrameId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Position).ToList());
        var frames = new Dictionary<string, FrameContent>();
        foreach (var o in context.FrameOptions.AsEnumerable())
        {
            var list = pictures.GetValueOrDefault(o.FrameId) ?? [];
            frames[o.FrameId] = new FrameContent(
                new FrameOptions((FitMode)o.Fit, o.BlurFill, (SlideshowMode)o.Mode, o.IntervalSeconds, (SlideshowOrder)o.Order, (SlideshowTransition)o.Transition, o.CurrentIndex),
                list.Select(p => new FramePicture(p.AssetId, p.FocalX, p.FocalY, p.Zoom)).ToList());
        }
        return new PictureFrameSet(frames);
    }

    private static PictureAsset ToDomain(PictureAssetEntity e) =>
        new(e.Id, e.Hash, e.FileName, e.MimeType, e.Width, e.Height, e.Bytes, e.SourceUrl, e.AddedAt);
}
