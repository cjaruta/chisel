using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;
using Chisel.Core.Layout;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

// The timer in a shrinking card: the time stays, the buttons that do not fit go, and nothing is cut off or piled over another part.
// And the analog clock leaves off its numerals once its dial is small.
public class ClockTimerResizeTests : IDisposable
{
    public ClockTimerResizeTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static MainWindow Show(double width, double height, ClockTool tool)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, clock: new FakeTimeProvider(UiSnapshot.SampleNow));
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 1, layout.Columns[1].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        main.ClockSlots.Slots[^1].Select(tool);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(MainWindow w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1000, 600)]
    [InlineData(1000, 520)]
    [InlineData(800, 460)]
    [InlineData(640, 420)]
    [InlineData(640, 360)]
    public void TheTimer_KeepsItsTime_AndEverythingShownIsInsideTheCard(double width, double height)
    {
        var window = Show(width, height, ClockTool.Timer);
        UiSnapshot.Render(window, $"clock-timer-{width:0}x{height:0}");
        var text = Named<TextBlock>(window, "ClockTimerText");
        var slot = text.GetVisualAncestors().OfType<ClockSlotView>().First();

        Assert.True(text.IsEffectivelyVisible, "the time is hidden");
        Assert.Equal("00:00", text.Text);
        foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c.Name is { } n && n.StartsWith("ClockTimer") && n != "ClockTimerPanel" && c.IsEffectivelyVisible))
        {
            var top = control.TranslatePoint(new Point(0, 0), slot)!.Value;
            var bottom = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), slot)!.Value;
            Assert.True(top.Y >= -1 && bottom.Y <= slot.Bounds.Height + 1, $"{control.Name} is cut off in a {slot.Bounds.Width:0}x{slot.Bounds.Height:0} slot");
            Assert.True(top.X >= -1 && bottom.X <= slot.Bounds.Width + 1, $"{control.Name} is wider than a {slot.Bounds.Width:0} px slot");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void TheTimer_GivesUpItsButtonsInOrder_AsTheCardShrinks_AndNeverTheTime()
    {
        var shown = new List<int>();
        foreach (var height in new[] { 760.0, 600, 520, 460, 420, 360 })
        {
            var window = Show(1000, height, ClockTool.Timer);
            var visible = window.GetVisualDescendants().OfType<Button>().Count(b => b.Name is { } n && n.StartsWith("ClockTimer") && b.IsEffectivelyVisible);
            Assert.True(Named<TextBlock>(window, "ClockTimerText").IsEffectivelyVisible);
            shown.Add(visible);
            window.Close();
        }

        Assert.Equal(shown.OrderByDescending(n => n), shown); // never more buttons in a smaller card
        Assert.True(shown[0] > shown[^1], "nothing was given up at all");
    }

    [AvaloniaFact]
    public void TheClockNumerals_GoWhenTheDialIsSmall_AndTheRestOfTheDialStays()
    {
        static byte[] Draw(ClockStyle style, double size)
        {
            var face = new ClockFace { Style = style, HourAngle = 305, MinuteAngle = 60, SecondAngle = 150, Hour24Angle = 152.5 };
            face.Measure(new Size(size, size));
            face.Arrange(new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(new PixelSize((int)size, (int)size));
            using (var context = bitmap.CreateDrawingContext())
            {
                face.Draw(context);
            }
            using var stream = new MemoryStream();
            bitmap.Save(stream);
            return stream.ToArray();
        }

        var numbers = ClockPresets.Apply("numbers", ClockStyle.Default);
        var none = numbers with { Numerals = NumeralStyle.None };
        Assert.NotEqual(NumeralStyle.None, numbers.Numerals);

        // A small dial is drawn the same with the numerals asked for or not; a large one is not.
        var small = ClockDialLayout.NumeralsFromDiameter - 10 + 2 * 4; // the face keeps 4 px off its edge
        Assert.True(Draw(numbers, small).SequenceEqual(Draw(none, small)), "numerals are drawn on a small dial");
        Assert.False(Draw(numbers, 300).SequenceEqual(Draw(none, 300)), "numerals are missing from a large dial");
    }
}
