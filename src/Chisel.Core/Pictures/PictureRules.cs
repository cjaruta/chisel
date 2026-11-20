using System.Globalization;
using System.Text.RegularExpressions;

namespace Chisel.Core.Pictures;

// Which picture comes next, and what counts as a click on the frame.
public static class Slideshow
{
    // A press that moves further than this many pixels is a drag, not a click.
    public const double ClickThreshold = 4;

    public static int Next(int index, int count) => count <= 0 ? 0 : (index + 1) % count;

    public static int Previous(int index, int count) => count <= 0 ? 0 : (index - 1 + count) % count;

    // The order pictures are shown in: list order, or a shuffle that is the same for the same seed.
    public static int[] PlayOrder(int count, SlideshowOrder order, int seed)
    {
        var items = Enumerable.Range(0, Math.Max(0, count)).ToArray();
        if (order == SlideshowOrder.InOrder)
        {
            return items;
        }
        // A small generator of our own, so a seed means the same order on every machine and runtime.
        var state = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
        uint NextValue()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        _ = NextValue();
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = (int)(NextValue() % (uint)(i + 1));
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }

    // +1 for the right half of the frame, -1 for the left.
    public static int StepForClick(double x, double width) => x >= width / 2 ? 1 : -1;

    public static bool IsClick(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy) <= ClickThreshold;
}

public enum IntervalUnit { Seconds, Minutes, Hours }

// How long a cycling frame shows each picture.
public static class SlideshowInterval
{
    public const int Minimum = 5;
    public const int Maximum = 24 * 60 * 60;

    public static IReadOnlyList<int> Presets { get; } = new[] { 10, 30, 60, 300, 600, 900, 1800, 3600 };

    public static int Clamp(double seconds) => (int)Math.Clamp(Math.Round(seconds), Minimum, Maximum);

    public static int FromCustom(double value, IntervalUnit unit) =>
        Clamp(value * unit switch { IntervalUnit.Minutes => 60, IntervalUnit.Hours => 3600, _ => 1 });

    private static readonly Regex Pattern = new(@"^\s*(\d+(?:\.\d+)?)\s*([a-z]*)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "30s", "5 min", "1h", "1.5 min" or a bare number (seconds); null when it is not an interval.
    public static int? TryParse(string? text)
    {
        if (text is null || Pattern.Match(text) is not { Success: true } match
            || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            return null;
        }
        var unit = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "" or "s" or "sec" or "secs" or "second" or "seconds" => IntervalUnit.Seconds,
            "m" or "min" or "mins" or "minute" or "minutes" => IntervalUnit.Minutes,
            "h" or "hr" or "hrs" or "hour" or "hours" => IntervalUnit.Hours,
            _ => (IntervalUnit?)null,
        };
        return unit is { } u ? FromCustom(number, u) : null;
    }

    public static string Format(int seconds) =>
        seconds % 3600 == 0 ? $"{seconds / 3600} h" : seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds} s";
}

// What to store for a picture that arrives.
public sealed record StorageDecision(bool Resize, int Width, int Height);

public static class PictureStoragePolicy
{
    // With "Keep original files" on, or for an animated GIF, the bytes are stored as they came. With it off, a still
    // picture longer than DisplaySide on its long side is stored as a smaller re-encoded copy.
    public static StorageDecision Decide(bool keepOriginals, int width, int height, bool isAnimatedGif)
    {
        var longSide = Math.Max(width, height);
        if (keepOriginals || isAnimatedGif || longSide <= ImageLimits.DisplaySide)
        {
            return new StorageDecision(false, width, height);
        }
        var scale = (double)ImageLimits.DisplaySide / longSide;
        return new StorageDecision(true, Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}

public enum MultiDropMode { OneSlideshow, OneFramePerPicture }

// How many frames a drop of several pictures makes.
public sealed record DropPlan(int Frames, int PicturesUsed, string? Notice);

public static class MultiDropPolicy
{
    public const int MaxFramesPerDrop = 8;

    public static DropPlan Plan(MultiDropMode mode, int count)
    {
        if (count <= 0)
        {
            return new DropPlan(0, 0, null);
        }
        if (mode == MultiDropMode.OneSlideshow)
        {
            return new DropPlan(1, count, null);
        }
        return count <= MaxFramesPerDrop
            ? new DropPlan(count, count, null)
            : new DropPlan(MaxFramesPerDrop, MaxFramesPerDrop, $"Only the first {MaxFramesPerDrop} pictures were added.");
    }
}

public static class PictureUrls
{
    private static readonly string[] DataPrefixes =
        ["data:image/png", "data:image/jpeg", "data:image/jpg", "data:image/gif", "data:image/webp"];

    // http, https, and data:image (not SVG) only.
    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        var text = url.Trim();
        if (DataPrefixes.Any(p => text.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    private static readonly Regex ImgSrc = new(
        @"<img\b[^>]*?\bsrc\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The first <img src> in a piece of HTML (what a browser puts on the drag data for a dragged picture).
    public static string? FromHtml(string? html)
    {
        if (string.IsNullOrEmpty(html) || ImgSrc.Match(html) is not { Success: true } match)
        {
            return null;
        }
        var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        value = value.Replace("&amp;", "&", StringComparison.Ordinal).Trim();
        return value.Length == 0 ? null : value;
    }
}

public static class AssetSweep
{
    // The assets no frame uses: the ones to delete, after Done.
    public static IReadOnlyList<Guid> Unused(IEnumerable<Guid> allAssetIds, IReadOnlySet<Guid> used) =>
        allAssetIds.Where(id => !used.Contains(id)).ToList();
}

public static class PictureAssets
{
    // The same bytes are stored once: the asset already stored with this hash, or the new one.
    public static PictureAsset Resolve(IEnumerable<PictureAsset> existing, PictureAsset candidate) =>
        existing.FirstOrDefault(a => a.Hash == candidate.Hash) ?? candidate;
}

public readonly record struct SourceRect(double X, double Y, double Width, double Height);

// Which part of a picture Fill mode shows. The focal point is where in the room the picture can pan: 0 is the
// left (or top) edge, 1 the right (bottom) edge, 0.5 the middle. Zoom 1 just covers the view.
public static class CropMath
{
    public static double Scale(double imageWidth, double imageHeight, double viewWidth, double viewHeight, double zoom) =>
        Math.Max(viewWidth / imageWidth, viewHeight / imageHeight) * Math.Max(1, zoom);

    public static SourceRect SourceRect(double imageWidth, double imageHeight, double viewWidth, double viewHeight,
        double focalX, double focalY, double zoom)
    {
        var scale = Scale(imageWidth, imageHeight, viewWidth, viewHeight, zoom);
        var width = Math.Min(imageWidth, viewWidth / scale);
        var height = Math.Min(imageHeight, viewHeight / scale);
        return new SourceRect(
            Math.Clamp(focalX, 0, 1) * (imageWidth - width), Math.Clamp(focalY, 0, 1) * (imageHeight - height), width, height);
    }

    // The focal point after dragging the picture by (dragX, dragY) view pixels: the picture follows the pointer.
    public static (double X, double Y) Pan(double imageWidth, double imageHeight, double viewWidth, double viewHeight,
        double focalX, double focalY, double zoom, double dragX, double dragY)
    {
        var scale = Scale(imageWidth, imageHeight, viewWidth, viewHeight, zoom);
        var rect = SourceRect(imageWidth, imageHeight, viewWidth, viewHeight, focalX, focalY, zoom);
        var roomX = imageWidth - rect.Width;
        var roomY = imageHeight - rect.Height;
        return (
            roomX <= 1e-9 ? focalX : Math.Clamp((rect.X - dragX / scale) / roomX, 0, 1),
            roomY <= 1e-9 ? focalY : Math.Clamp((rect.Y - dragY / scale) / roomY, 0, 1));
    }
}
