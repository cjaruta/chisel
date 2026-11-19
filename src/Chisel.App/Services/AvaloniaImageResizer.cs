using System;
using System.IO;
using Avalonia.Media.Imaging;
using Chisel.Core.Pictures;

namespace Chisel.App.Services;

// Makes the display-size copy of a still picture: decoded at the smaller size and encoded as PNG.
public sealed class AvaloniaImageResizer : IImageResizer
{
    public byte[]? Resize(byte[] data, int width, int height)
    {
        try
        {
            using var source = new MemoryStream(data);
            using var bitmap = Bitmap.DecodeToWidth(source, width, BitmapInterpolationMode.HighQuality);
            using var output = new MemoryStream();
            bitmap.Save(output);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}
