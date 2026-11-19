using System;
using System.IO;
using Chisel.Core.Pictures;
using SkiaSharp;

namespace Chisel.App.Services;

// Makes the display-size copy of a still picture. A picture that is fully opaque (a photo) is encoded as JPEG, which is
// far smaller than PNG for photos; one with see-through parts stays PNG so the transparency survives.
public sealed class AvaloniaImageResizer : IImageResizer
{
    private const int JpegQuality = 85;

    public byte[]? Resize(byte[] data, int width, int height)
    {
        try
        {
            using var source = SKBitmap.Decode(data);
            if (source is null || width <= 0 || height <= 0)
            {
                return null;
            }
            using var scaled = source.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (scaled is null)
            {
                return null;
            }
            using var image = SKImage.FromBitmap(scaled);
            using var encoded = image.Encode(HasTransparency(scaled) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, JpegQuality);
            return encoded?.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    // Whether any pixel is not fully opaque.
    private static bool HasTransparency(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
        {
            return false;
        }
        var pixels = bitmap.Pixels;
        foreach (var pixel in pixels)
        {
            if (pixel.Alpha < 255)
            {
                return true;
            }
        }
        return false;
    }
}
