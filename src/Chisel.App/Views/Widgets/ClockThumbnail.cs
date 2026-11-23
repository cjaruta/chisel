using Avalonia;
using Avalonia.Media.Imaging;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// A small picture of a clock look (shown at ten past ten, the way watches are), for the preset gallery. It is drawn by the same
// ClockFace as the widget, so a thumbnail can never disagree with what the preset really looks like.
public static class ClockThumbnail
{
    public static RenderTargetBitmap Render(ClockStyle style, int size = 88)
    {
        var face = new ClockFace { Style = style, HourAngle = 305, MinuteAngle = 60, SecondAngle = 180, Hour24Angle = 152.5 };
        face.Measure(new Size(size, size));
        face.Arrange(new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using (var context = bitmap.CreateDrawingContext())
        {
            face.Draw(context);
        }
        return bitmap;
    }
}
