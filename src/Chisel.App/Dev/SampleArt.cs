using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Chisel.App.Dev;

// A small made-up cover (a blue-to-orange gradient with a circle) as PNG bytes, for capture scenes and tests.
public static class SampleArt
{
    public static byte[] Png(int size = 96)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using (var context = bitmap.CreateDrawingContext())
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#5B8DEF"), 0), new GradientStop(Color.Parse("#F5A524"), 1) },
            };
            context.DrawRectangle(gradient, null, new Rect(0, 0, size, size));
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#33FFFFFF")), null, new Point(size / 2.0, size / 2.0), size / 4.0, size / 4.0);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
