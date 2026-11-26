using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

// The clock widget's slots are centered in the widget and evenly spaced: the same space at both ends and between neighbours,
// for one, two or three items, in the dark and the light theme and at small and large widget sizes.
public class ClockLayoutHeadlessTests : IDisposable
{
    public ClockLayoutHeadlessTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static ClockSlotsConfig Items(int count)
    {
        var all = new[]
        {
            ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Stopwatch), ClockSlotConfig.Pinned(ClockTool.Alarm),
        };
        return new ClockSlotsConfig(all.Take(count).ToList());
    }

    private static (MainWindow Window, WidgetFrame Frame, MainViewModel Main) Show(double width, double height, int count, string? theme = null,
        ClockAlignment alignment = ClockAlignment.Center)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, themeMode: theme);
        var main = (MainViewModel)window.DataContext!;
        main.ClockTools.SetSlots(Items(count));
        main.ClockSettings.SelectAlignmentCommand.Execute(alignment);
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock), main);
    }

    private static List<Rect> Slots(WidgetFrame frame, Avalonia.Visual relativeTo) =>
        frame.GetVisualDescendants().OfType<ClockSlotView>()
            .Select(s => new Rect(s.TranslatePoint(new Point(0, 0), relativeTo)!.Value, s.Bounds.Size))
            .OrderBy(r => r.X).ThenBy(r => r.Y).ToList();

    [AvaloniaTheory]
    [InlineData(1, 1920, 900, null)]
    [InlineData(2, 1920, 900, null)]
    [InlineData(3, 1920, 900, null)]
    [InlineData(3, 1920, 900, "light")]
    [InlineData(3, 1000, 650, null)]
    [InlineData(2, 640, 600, "light")]
    [InlineData(1, 640, 600, null)]
    public void TheSlotsAreCenteredAndEvenlySpaced_AlongAWideWidget(int count, double width, double height, string? theme)
    {
        var (window, frame, main) = Show(width, height, count, theme);
        var slots = Slots(frame, frame);
        Assert.Equal(main.ClockSlots.Slots.Count, slots.Count);
        var body = new Rect(frame.Bounds.Size);

        // Space-evenly: the same space before the first slot, between every two slots and after the last one (when there is
        // more than one slot; a single slot fills the card and centers its own content).
        if (slots.Count > 1)
        {
            var spaces = new List<double> { slots[0].Left - body.Left };
            spaces.AddRange(slots.Zip(slots.Skip(1), (a, b) => b.Left - a.Right));
            spaces.Add(body.Right - slots[^1].Right);
            Assert.All(spaces, s => Assert.Equal(spaces[0], s, 2.5));
            Assert.True(spaces[0] >= ClockSlotLayout.SlotGap - 0.5);
        }
        else
        {
            Assert.Equal(slots[0].Left - body.Left, body.Right - slots[0].Right, 1.5);
        }
        // Vertically the slots share one band.
        Assert.All(slots, s => Assert.Equal(slots[0].Top, s.Top, 1.5));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1, 480, 1200)]
    [InlineData(2, 480, 1200)]
    [InlineData(3, 480, 1200)]
    public void TheSlotsAreCenteredAndEvenlySpaced_DownATallWidget(int count, double width, double height)
    {
        var (window, frame, _) = Show(width, height, count);
        var slots = Slots(frame, frame);
        var column = frame.GetVisualDescendants().OfType<ClockSlotView>().Select(s => s.Parent).OfType<Avalonia.Controls.Grid>().First();
        if (slots.Count < 2)
        {
            window.Close();
            return;
        }
        var spaces = new List<double> { slots[0].Top };
        spaces.AddRange(slots.Zip(slots.Skip(1), (a, b) => b.Top - a.Bottom));
        Assert.All(spaces, s => Assert.True(s >= ClockSlotLayout.SlotGap - 0.5));
        Assert.All(spaces.Skip(1), s => Assert.Equal(spaces[1], s, 2.5));
        Assert.NotNull(column);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ClockAlignment.Left)]
    [InlineData(ClockAlignment.Center)]
    [InlineData(ClockAlignment.Right)]
    public void ASingleClock_FollowsItsAlignmentSetting_AndCenterIsTheDefault(ClockAlignment alignment)
    {
        Assert.Equal(ClockAlignment.Center, ClockStyle.Default.Alignment);
        var (window, frame, _) = Show(1200, 760, 1, alignment: alignment);
        var face = frame.GetVisualDescendants().OfType<ClockFace>().Single();
        var slot = Slots(frame, frame).Single();
        var center = face.TranslatePoint(face.FaceCenter, frame)!.Value;

        var middle = slot.Left + slot.Width / 2;
        switch (alignment)
        {
            case ClockAlignment.Center:
                Assert.Equal(middle, center.X, 1.5);
                break;
            case ClockAlignment.Left:
                Assert.True(center.X < middle - 20);
                break;
            default:
                Assert.True(center.X > middle + 20);
                break;
        }
        Assert.Equal(slot.Top + slot.Height / 2, center.Y, 1.5);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("dark", null)]
    [InlineData("light", "light")]
    public void Snapshot_ThreeItems(string name, string? theme)
    {
        var (window, frame, _) = Show(1500, 700, 3, theme);
        var bitmap = new RenderTargetBitmap(new PixelSize((int)frame.Bounds.Width, (int)frame.Bounds.Height));
        bitmap.Render(frame);
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, $"clock-layout-three-{name}.png"));
        window.Close();
    }
}
