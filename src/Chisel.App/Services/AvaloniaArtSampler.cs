using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Chisel.App.Services;

// Decodes a cover straight to about 32 pixels wide (the decoder scales while it reads) and copies the pixels out.
// The bitmap is disposed before returning. Any failure just means no palette from this cover.
public sealed class AvaloniaArtSampler : IArtSampler
{
    private const int Width = 32;

    public ArtPixels? Sample(byte[] art, CancellationToken cancel)
    {
        try
        {
            using var stream = new MemoryStream(art);
            using var bitmap = Bitmap.DecodeToWidth(stream, Width, BitmapInterpolationMode.MediumQuality);
            cancel.ThrowIfCancellationRequested();
            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            if (width < 1 || height < 1)
            {
                return null;
            }
            var stride = width * 4;
            var data = new byte[stride * height];
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), data.Length, stride);
            }
            finally
            {
                handle.Free();
            }
            var bgra = bitmap.Format == PixelFormats.Bgra8888;
            for (var i = 0; i < data.Length; i += 4)
            {
                var a = data[i + 3];
                if (bgra)
                {
                    (data[i], data[i + 2]) = (data[i + 2], data[i]);
                }
                if (a is > 0 and < 255)
                {
                    // Decoders hand back premultiplied color; undo it so transparent edges do not darken the palette.
                    data[i] = (byte)Math.Min(255, data[i] * 255 / a);
                    data[i + 1] = (byte)Math.Min(255, data[i + 1] * 255 / a);
                    data[i + 2] = (byte)Math.Min(255, data[i + 2] * 255 / a);
                }
            }
            return new ArtPixels(data, width, height);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
