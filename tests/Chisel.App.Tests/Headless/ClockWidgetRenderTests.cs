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

namespace Chisel.App.Tests.Headless;

// Renders the clock widget at the sizes it is used at (a half-screen window's right column, the full width, a small window; one, two
// and three slots) to artifacts/ui/clock-size-*.png, and checks that nothing inside a slot is clipped or left without room.
public class ClockWidgetRenderTests(ITestOutputHelper output) : IDisposable
{
    private readonly bool _ = SetDuration();

    private static bool SetDuration()
    {
        SlideHost.SlideDuration = TimeSpan.Zero;
        return true;
    }

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static readonly ClockSlotsConfig Two = ClockSlotsConfig.Default;

    private static readonly ClockSlotsConfig One = new([ClockSlotConfig.Pinned(ClockTool.Clock)]);

    private static readonly ClockSlotsConfig Three = new([
        ClockSlotConfig.Pinned(ClockTool.Clock),
        ClockSlotConfig.Scrolling(ClockTool.Timer, ClockTool.Stopwatch),
        ClockSlotConfig.Pinned(ClockTool.Alarm)]);

    private static (MainWindow Window, MainViewModel Main, WidgetFrame Frame) Show(double width, double height, int column, ClockSlotsConfig slots)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        var main = (MainViewModel)window.DataContext!;
        main.ClockTools.SetSlots(slots);
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, column, layout.Columns[column].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock);
        return (window, main, frame);
    }

    private static void Save(WidgetFrame frame, string name)
    {
        var size = new PixelSize((int)frame.Bounds.Width, (int)frame.Bounds.Height);
        var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(frame);
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, "clock-size-" + name + ".png"));
    }

    // Everything visible in a slot must lie inside it (a list that scrolls on purpose is left out), and a slot must be big enough to use.
    private static void AssertNothingClipped(WidgetFrame frame)
    {
        foreach (var slot in frame.GetVisualDescendants().OfType<ClockSlotView>())
        {
            Assert.True(slot.Bounds.Width >= 100 && slot.Bounds.Height >= 100,
                $"a slot of {slot.Bounds.Width:0}x{slot.Bounds.Height:0} is too small to use");
            foreach (var child in slot.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0))
            {
                if (child.GetVisualAncestors().TakeWhile(a => !ReferenceEquals(a, slot)).OfType<ScrollViewer>().Any())
                {
                    continue;
                }
                var origin = child.TranslatePoint(new Point(0, 0), slot)!.Value;
                Assert.True(origin.X >= -1 && origin.Y >= -1 && origin.X + child.Bounds.Width <= slot.Bounds.Width + 1 && origin.Y + child.Bounds.Height <= slot.Bounds.Height + 1,
                    $"{child.GetType().Name} '{child.Name}' at {origin.X:0},{origin.Y:0} size {child.Bounds.Width:0}x{child.Bounds.Height:0} is clipped by its {slot.Bounds.Width:0}x{slot.Bounds.Height:0} slot");
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("half-right", 960, 760, 1, 2)] // a half-screen window, the clock in its right column
    [InlineData("full", 1920, 900, 0, 2)] // the full width of a wide window
    [InlineData("small", 640, 600, 1, 1)] // a small window: room for one slot, so it takes every tool
    public void TheDefaultSlots_FitAndAreSizedFromTheirRoom(string name, double width, double height, int column, int slots)
    {
        var (_, main, frame) = Show(width, height, column, Two);
        output.WriteLine($"{name}: widget {frame.Bounds.Width:0}x{frame.Bounds.Height:0}, slots {main.ClockSlots.Slots.Count}");

        Assert.Equal(slots, main.ClockSlots.Slots.Count);
        var views = frame.GetVisualDescendants().OfType<ClockSlotView>().ToList();
        if (slots == 2)
        {
            Assert.True(views[0].Bounds.Width <= views[0].Bounds.Height + 1, "the clock slot takes a square");
        }
        AssertNothingClipped(frame);
        Save(frame, name + "-timer");

        main.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch);
        Dispatcher.UIThread.RunJobs();
        AssertNothingClipped(frame);
        Save(frame, name + "-stopwatch");

        main.ClockSlots.Slots[^1].Select(ClockTool.Alarm);
        Dispatcher.UIThread.RunJobs();
        AssertNothingClipped(frame);
        Save(frame, name + "-alarm");
    }

    [AvaloniaFact]
    public void OneSlot_IsTheClockAloneUsingTheWholeWidget()
    {
        var (_, main, frame) = Show(1200, 760, 0, One);
        Assert.Single(main.ClockSlots.Slots);
        AssertNothingClipped(frame);
        Save(frame, "one-slot");
    }

    [AvaloniaTheory]
    [InlineData("three-slots", 1920, 900, 0, 3)] // room for all three
    [InlineData("three-slots-small", 960, 760, 1, 2)] // room for two: the last absorbs the third's tools
    public void ThreeSlots_ShareTheRoomOrAbsorbTheOnesThatDoNotFit(string name, double width, double height, int column, int shown)
    {
        var (_, main, frame) = Show(width, height, column, Three);
        output.WriteLine($"{name}: widget {frame.Bounds.Width:0}x{frame.Bounds.Height:0}, slots {main.ClockSlots.Slots.Count}");

        Assert.Equal(shown, main.ClockSlots.Slots.Count);
        if (shown < 3)
        {
            Assert.Contains(ClockTool.Alarm, main.ClockSlots.Slots[^1].Tools); // every tool stays reachable
        }
        AssertNothingClipped(frame);
        Save(frame, name);
    }

    [AvaloniaFact]
    public void ARingingAlarm_FitsItsSlotToo()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(UiSnapshot.SampleNow);
        var window = UiSnapshot.ShowMainWindow(960, 760, clock: clock);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 1, layout.Columns[1].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs(); // let the widget lay out, so a slot is on screen to ring in
        main.Alarms.NewTime = "09:35";
        main.Alarms.NewLabel = "Call back";
        main.Alarms.AddCommand.Execute(null);
        clock.Advance(TimeSpan.FromMinutes(6));
        main.Tick();
        Dispatcher.UIThread.RunJobs();

        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock);
        AssertNothingClipped(frame);
        Assert.NotNull(main.Alarms.Ringing);
        Assert.Null(main.Alerts.Current);
        Assert.Equal(ClockTool.Alarm, main.ClockSlots.Slots[^1].Current);
        Save(frame, "half-right-ringing");
    }
}
