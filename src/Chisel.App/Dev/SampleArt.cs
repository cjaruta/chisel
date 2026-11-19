using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Chisel.App.Dev;

// A small made-up cover (a blue-to-orange gradient with a circle) as PNG bytes, for capture scenes and tests.
public static class SampleArt
{
    // Made-up covers for the gradient captures: "warm", "cool", "contrast" (black, white and one red block), "mono" (greys).
    public static readonly string[] CoverKinds = ["warm", "cool", "contrast", "mono"];

    public static byte[] Cover(string kind, int size = 160)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using (var context = bitmap.CreateDrawingContext())
        {
            var area = new Rect(0, 0, size, size);
            IBrush Linear(string from, string to) => new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse(from), 0), new GradientStop(Color.Parse(to), 1) },
            };
            switch (kind)
            {
                case "warm":
                    context.DrawRectangle(Linear("#F7B733", "#C2185B"), null, area);
                    context.DrawEllipse(new SolidColorBrush(Color.Parse("#66FF8A3D")), null, new Point(size * 0.35, size * 0.6), size * 0.3, size * 0.3);
                    break;
                case "cool":
                    context.DrawRectangle(Linear("#1FA2A6", "#2B3A8C"), null, area);
                    context.DrawEllipse(new SolidColorBrush(Color.Parse("#555BC0EB")), null, new Point(size * 0.65, size * 0.4), size * 0.28, size * 0.28);
                    break;
                case "contrast":
                    context.DrawRectangle(Brushes.White, null, area);
                    context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, size, size * 0.18));
                    context.DrawRectangle(Brushes.Black, null, new Rect(0, size * 0.82, size, size * 0.18));
                    context.DrawRectangle(new SolidColorBrush(Color.Parse("#E01E37")), null, new Rect(size * 0.3, size * 0.3, size * 0.4, size * 0.4));
                    break;
                default:
                    context.DrawRectangle(Linear("#2A2A2A", "#D8D8D8"), null, area);
                    context.DrawEllipse(new SolidColorBrush(Color.Parse("#55808080")), null, new Point(size / 2.0, size / 2.0), size * 0.3, size * 0.3);
                    break;
            }
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

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
