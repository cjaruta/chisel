using System.Buffers.Binary;

namespace Chisel.Core.Pictures;

public static class ImageFormats
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Gif = "image/gif";
    public const string WebP = "image/webp";

    public static bool IsKnownMime(string mime) => mime is Png or Jpeg or Gif or WebP;

    public static string ExtensionOf(string mime) => mime switch { Png => "png", Jpeg => "jpg", Gif => "gif", WebP => "webp", _ => "bin" };
}

// What the first bytes of a file say it is. Animated: a GIF with more than one frame, or a WebP marked animated.
public sealed record ImageInfo(string MimeType, int Width, int Height, bool IsAnimated);

public static class ImageLimits
{
    public const long MaxDownloadBytes = 30L * 1024 * 1024;
    public const long MaxPixels = 100L * 1000 * 1000;
    public const int MaxSide = 12000;
    // The long side of a display-size copy.
    public const int DisplaySide = 2048;

    // Null when the size is fine, otherwise a short message.
    public static string? Check(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return "The picture has no size.";
        }
        if (width > MaxSide || height > MaxSide)
        {
            return $"The picture is larger than {MaxSide} pixels on a side.";
        }
        return (long)width * height > MaxPixels ? "The picture has more than 100 megapixels." : null;
    }
}

// Reads the type and size of PNG, JPEG, GIF and WebP files from their first bytes, and nothing else: the file's name
// and any claimed type are never trusted. SVG and every other format give null.
public static class ImageSniffer
{
    public static ImageInfo? Sniff(ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.Length >= 24 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                return Valid(ImageFormats.Png, (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..]), (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..]), false);
            }
            if (data.Length >= 10 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' && (data[4] == '7' || data[4] == '9') && data[5] == 'a')
            {
                return Valid(ImageFormats.Gif, BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]), GifFrames(data) > 1);
            }
            if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
            {
                return Jpeg(data);
            }
            if (data.Length >= 30 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            {
                return WebP(data);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // A file cut short in the middle of its header is not a picture.
        }
        return null;
    }

    private static ImageInfo? Valid(string mime, int width, int height, bool animated) =>
        width > 0 && height > 0 ? new ImageInfo(mime, width, height, animated) : null;

    private static ImageInfo? Jpeg(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }
            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                return Valid(ImageFormats.Jpeg, BinaryPrimitives.ReadUInt16BigEndian(data[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(i + 5)..]), false);
            }
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }
            i += 2 + BinaryPrimitives.ReadUInt16BigEndian(data[(i + 2)..]);
        }
        return null;
    }

    private static ImageInfo? WebP(ReadOnlySpan<byte> data)
    {
        var kind = data[12..16];
        if (kind.SequenceEqual("VP8X"u8))
        {
            var animated = (data[20] & 0x02) != 0;
            return Valid(ImageFormats.WebP, 1 + (data[24] | data[25] << 8 | data[26] << 16), 1 + (data[27] | data[28] << 8 | data[29] << 16), animated);
        }
        if (kind.SequenceEqual("VP8L"u8) && data[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
            return Valid(ImageFormats.WebP, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1, false);
        }
        if (kind.SequenceEqual("VP8 "u8) && data[23] == 0x9D && data[24] == 0x01 && data[25] == 0x2A)
        {
            return Valid(ImageFormats.WebP, BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF, false);
        }
        return null;
    }

    // How many images a GIF holds (read as far as the data goes, stopping at 2: that is all "animated" needs).
    private static int GifFrames(ReadOnlySpan<byte> data)
    {
        var packed = data[10];
        var i = 13 + ((packed & 0x80) != 0 ? 3 * (1 << ((packed & 7) + 1)) : 0);
        var frames = 0;
        while (i < data.Length && frames < 2)
        {
            switch (data[i])
            {
                case 0x21:
                    i += 2;
                    i = SkipSubBlocks(data, i);
                    break;
                case 0x2C:
                    frames++;
                    var local = data[i + 9];
                    i += 10 + ((local & 0x80) != 0 ? 3 * (1 << ((local & 7) + 1)) : 0) + 1;
                    i = SkipSubBlocks(data, i);
                    break;
                default:
                    return frames;
            }
        }
        return frames;
    }

    private static int SkipSubBlocks(ReadOnlySpan<byte> data, int i)
    {
        while (i < data.Length && data[i] != 0)
        {
            i += data[i] + 1;
        }
        return i + 1;
    }
}
