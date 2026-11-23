using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

// Every built-in face drawn by the one renderer, at ten past ten, on one sheet (artifacts/ui/clock-dials.png).
public class ClockDialsRenderTests
{
    private const int Cell = 260;

    [AvaloniaFact]
    public void Sheet_ShowsEveryFace_IncludingTheNewDials()
    {
        var presets = ClockPresets.All;
        var columns = 4;
        var rows = (presets.Count + columns - 1) / columns;
        var sheet = new RenderTargetBitmap(new PixelSize(Cell * columns, Cell * rows));
        using (var context = sheet.CreateDrawingContext())
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse("#1F222A")), new Rect(0, 0, Cell * columns, Cell * rows)); // a dark card, like the widget
            for (var i = 0; i < presets.Count; i++)
            {
                var face = new ClockFace
                {
                    Style = ClockPresets.Apply(presets[i].Id, ClockStyle.Default),
                    HourAngle = 305, MinuteAngle = 60, SecondAngle = 150, Hour24Angle = 10 * 15 + 10 * 0.25,
                };
                face.Measure(new Size(Cell, Cell));
                face.Arrange(new Rect(0, 0, Cell, Cell));
                using (context.PushTransform(Matrix.CreateTranslation(i % columns * Cell, i / columns * Cell)))
                {
                    face.Draw(context);
                }
            }
        }
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        sheet.Save(Path.Combine(directory, "clock-dials.png"));
    }
}
