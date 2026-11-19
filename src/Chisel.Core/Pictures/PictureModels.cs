namespace Chisel.Core.Pictures;

public enum FitMode { Fill, Fit }

public enum SlideshowMode { Manual, Cycle }

public enum SlideshowOrder { InOrder, Shuffle }

public enum SlideshowTransition { Fade, None }

// One stored picture file. The same bytes are stored once: Hash is the SHA-256 of the stored bytes (lower-case hex),
// and the file is pictures\<Hash>.<extension of FileName>.
public sealed record PictureAsset(
    Guid Id, string Hash, string FileName, string MimeType, int Width, int Height, long Bytes,
    string? SourceUrl, DateTimeOffset AddedAt)
{
    // Checks the rules every asset follows; throws ArgumentException for the first one broken.
    public static PictureAsset Create(string hash, string fileName, string mimeType, int width, int height, long bytes,
        string? sourceUrl, DateTimeOffset addedAt, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A hash is 64 hex digits.", nameof(hash));
        }
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("A picture has a size.", nameof(width));
        }
        if (bytes <= 0)
        {
            throw new ArgumentException("A picture has bytes.", nameof(bytes));
        }
        if (!ImageFormats.IsKnownMime(mimeType))
        {
            throw new ArgumentException("Only PNG, JPEG, GIF and WebP are kept.", nameof(mimeType));
        }
        return new PictureAsset(id ?? Guid.NewGuid(), hash.ToLowerInvariant(), SafeFileName(fileName, mimeType), mimeType,
            width, height, bytes, string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl, addedAt);
    }

    // The name the user will recognise, without any folder part; a fallback when there is none.
    private static string SafeFileName(string? fileName, string mimeType)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        var clean = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        return clean.Length == 0 ? "picture." + ImageFormats.ExtensionOf(mimeType) : clean;
    }

    // The file's name inside the pictures folder.
    public string StoredName => $"{Hash}.{ImageFormats.ExtensionOf(MimeType)}";

    public bool IsGif => MimeType == ImageFormats.Gif;
}

// A picture in a frame's list, with how it is cropped: the focal point (0..1 across and down the image) that stays
// in view in Fill mode, and a zoom of 1 or more.
public sealed record FramePicture(Guid AssetId, double FocalX = 0.5, double FocalY = 0.5, double Zoom = 1)
{
    public const double MaxZoom = 8;

    public FramePicture Cropped(double focalX, double focalY, double zoom) =>
        this with { FocalX = Math.Clamp(focalX, 0, 1), FocalY = Math.Clamp(focalY, 0, 1), Zoom = Math.Clamp(zoom, 1, MaxZoom) };
}

// A frame's own options.
public sealed record FrameOptions(
    FitMode Fit = FitMode.Fill, bool BlurFill = false, SlideshowMode Mode = SlideshowMode.Manual,
    int IntervalSeconds = 30, SlideshowOrder Order = SlideshowOrder.InOrder,
    SlideshowTransition Transition = SlideshowTransition.Fade, int CurrentIndex = 0);

public sealed record FrameContent(FrameOptions Options, IReadOnlyList<FramePicture> Pictures)
{
    public static FrameContent Empty { get; } = new(new FrameOptions(), []);
}

// Every picture frame's options and pictures, by frame instance id. Immutable: each change returns a new set, which
// is what lets edit mode keep a working copy and Cancel put the old one back. A frame with no entry is empty.
public sealed class PictureFrameSet
{
    public static PictureFrameSet Empty { get; } = new(new Dictionary<string, FrameContent>());

    private readonly IReadOnlyDictionary<string, FrameContent> _frames;

    public PictureFrameSet(IReadOnlyDictionary<string, FrameContent> frames) => _frames = frames;

    public IReadOnlyCollection<string> FrameIds => _frames.Keys.ToList();

    public FrameContent Get(string frameId) => _frames.TryGetValue(frameId, out var content) ? content : FrameContent.Empty;

    public bool Has(string frameId) => _frames.ContainsKey(frameId);

    // Every asset some frame uses.
    public IReadOnlySet<Guid> UsedAssetIds => _frames.Values.SelectMany(f => f.Pictures).Select(p => p.AssetId).ToHashSet();

    // Adds pictures at the end of a frame's list. A picture the frame already has is skipped (the same bytes are one asset).
    public PictureFrameSet AddPictures(string frameId, IEnumerable<Guid> assetIds)
    {
        var content = Get(frameId);
        var list = content.Pictures.ToList();
        foreach (var id in assetIds)
        {
            if (list.All(p => p.AssetId != id))
            {
                list.Add(new FramePicture(id));
            }
        }
        return With(frameId, content with { Pictures = list });
    }

    public PictureFrameSet RemovePicture(string frameId, int index)
    {
        var content = Get(frameId);
        if (index < 0 || index >= content.Pictures.Count)
        {
            return this;
        }
        var list = content.Pictures.ToList();
        list.RemoveAt(index);
        var current = Math.Clamp(content.Options.CurrentIndex, 0, Math.Max(0, list.Count - 1));
        return With(frameId, new FrameContent(content.Options with { CurrentIndex = current }, list));
    }

    // Moves a picture up (negative) or down (positive) the list by delta places, stopping at the ends.
    public PictureFrameSet Move(string frameId, int index, int delta)
    {
        var content = Get(frameId);
        var to = Math.Clamp(index + delta, 0, content.Pictures.Count - 1);
        if (index < 0 || index >= content.Pictures.Count || to == index)
        {
            return this;
        }
        var list = content.Pictures.ToList();
        var moved = list[index];
        list.RemoveAt(index);
        list.Insert(to, moved);
        return With(frameId, content with { Pictures = list });
    }

    public PictureFrameSet SetCrop(string frameId, int index, double focalX, double focalY, double zoom)
    {
        var content = Get(frameId);
        if (index < 0 || index >= content.Pictures.Count)
        {
            return this;
        }
        var list = content.Pictures.ToList();
        list[index] = list[index].Cropped(focalX, focalY, zoom);
        return With(frameId, content with { Pictures = list });
    }

    public PictureFrameSet SetOptions(string frameId, FrameOptions options) => With(frameId, Get(frameId) with { Options = options });

    // Only the frames still on the dashboard keep their pictures.
    public PictureFrameSet PrunedTo(IEnumerable<string> frameIds)
    {
        var keep = frameIds.ToHashSet();
        return _frames.Keys.All(keep.Contains) ? this : new PictureFrameSet(_frames.Where(f => keep.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value));
    }

    private PictureFrameSet With(string frameId, FrameContent content)
    {
        var frames = _frames.ToDictionary(f => f.Key, f => f.Value);
        frames[frameId] = content;
        return new PictureFrameSet(frames);
    }
}
