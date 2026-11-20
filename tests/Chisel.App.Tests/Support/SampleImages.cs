using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Chisel.App.Tests.Support;

// Pictures made on the spot for tests and contact sheets (no files in the repo, nothing personal): a diagonal gradient with
// a ring and a marker in one corner, so a crop, a fit and a fade can be told apart by eye.
public static class SampleImages
{
    public static byte[] Png(int width, int height, Color from, Color to)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = bitmap.CreateDrawingContext())
        {
            var area = new Rect(0, 0, width, height);
            context.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) },
            }, null, area);
            var size = Math.Min(width, height);
            context.DrawEllipse(null, new Pen(Brushes.White, Math.Max(1, size / 24.0)), area.Center, size * 0.32, size * 0.32);
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, size * 0.18, size * 0.18));
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    public static byte[] Landscape(int width = 480, int height = 270) => Png(width, height, Color.Parse("#2E5E8C"), Color.Parse("#E0A458"));

    public static byte[] Portrait(int width = 270, int height = 480) => Png(width, height, Color.Parse("#7A3E6B"), Color.Parse("#4FB0A5"));

    public static byte[] Tiny() => Png(8, 8, Color.Parse("#3A8F4B"), Color.Parse("#D9D26A"));

    // Larger than the 2048 pixel display size, and than what a frame ever decodes.
    public static byte[] Huge() => Png(4096, 3072, Color.Parse("#B03A2E"), Color.Parse("#F4D03F"));

    // A small animated GIF of up to four solid frames, built by hand: every pixel is a clear code followed by its color, so
    // the LZW stream is trivially valid.
    public static byte[] Gif(int size = 24, params Color[] frames)
    {
        if (frames.Length == 0)
        {
            frames = [Color.Parse("#C0392B"), Color.Parse("#2980B9")];
        }
        var bytes = new List<byte>();
        bytes.AddRange("GIF89a"u8.ToArray());
        bytes.AddRange([(byte)size, 0, (byte)size, 0, 0x81, 0, 0]);
        for (var i = 0; i < 4; i++)
        {
            var color = i < frames.Length ? frames[i] : Colors.Black;
            bytes.AddRange([color.R, color.G, color.B]);
        }
        bytes.AddRange([0x21, 0xFF, 0x0B, .. "NETSCAPE2.0"u8.ToArray(), 0x03, 0x01, 0x00, 0x00, 0x00]);
        for (var f = 0; f < frames.Length; f++)
        {
            bytes.AddRange([0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00]);
            bytes.AddRange([0x2C, 0, 0, 0, 0, (byte)size, 0, (byte)size, 0, 0x00, 0x02]);

            var bits = new List<bool>();
            void Code(int value)
            {
                for (var b = 0; b < 3; b++)
                {
                    bits.Add((value >> b & 1) == 1);
                }
            }
            for (var p = 0; p < size * size; p++)
            {
                Code(4);
                Code(f);
            }
            Code(5);
            var data = new List<byte>();
            for (var i = 0; i < bits.Count; i += 8)
            {
                byte b = 0;
                for (var k = 0; k < 8 && i + k < bits.Count; k++)
                {
                    if (bits[i + k])
                    {
                        b |= (byte)(1 << k);
                    }
                }
                data.Add(b);
            }
            for (var i = 0; i < data.Count; i += 255)
            {
                var count = Math.Min(255, data.Count - i);
                bytes.Add((byte)count);
                bytes.AddRange(data.GetRange(i, count));
            }
            bytes.Add(0);
        }
        bytes.Add(0x3B);
        return bytes.ToArray();
    }
}
