using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

// Checks the visual tree, not pixels, so results don't depend on fonts or rendering.
public class MainWindowHeadlessTests
{
    private static IEnumerable<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);

    private static List<Border> GridBlocks(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("gridBlock")).ToList();

    private static string BlockTitle(Border block) =>
        block.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("blockTitle")).Text ?? "";

    // The block's slot on the day grid (its container), relative to the grid panel.
    private static Rect Slot(Border block) => ((Visual)block.GetVisualParent()!).Bounds;

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static DateTimeOffset At(int hour, int minute) => new(2000, 1, 1, hour, minute, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void MainWindow_LoadsWithItsTitle()
    {
        var window = UiSnapshot.ShowMainWindow();

        Assert.True(window.IsLoaded);
        Assert.Equal("Chisel", window.Title);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Today")]
    [InlineData("Tasks")]
    [InlineData("Notes")]
    [InlineData("Quick links")]
    public void MainWindow_ShowsCardHeading(string heading)
    {
        var window = UiSnapshot.ShowMainWindow();

        var headings = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("cardHeading"))
            .Select(t => t.Text);
        Assert.Contains(heading, headings);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_ShowsEveryBlockWithTheOvernightBlockTwice()
    {
        var window = UiSnapshot.ShowMainWindow();

        Assert.Equal(
            new[] { "Call", "Focus", "Sleep", "Sleep", "Wake up", "Workout" },
            GridBlocks(window).Select(BlockTitle).OrderBy(t => t));
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_ShowsTwentyFiveHourLabels()
    {
        var window = UiSnapshot.ShowMainWindow();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("hourLabel")).ToList();
        Assert.Equal(25, labels.Count);
        Assert.Equal("00:00", labels[0].Text);
        Assert.Equal("12:00", labels[12].Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_BlocksArePlacedAndSizedByTheirTimes()
    {
        var window = UiSnapshot.ShowMainWindow();
        var blocks = GridBlocks(window);

        var workout = Slot(blocks.Single(b => BlockTitle(b) == "Workout"));
        Assert.Equal(14 * 60, workout.Y, 1);
        Assert.Equal(60, workout.Height, 1);

        var focus = Slot(blocks.Single(b => BlockTitle(b) == "Focus"));
        Assert.Equal(8 * 60, focus.Y, 1);
        Assert.Equal(180, focus.Height, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_OverlappingBlocksSitSideBySide()
    {
        var window = UiSnapshot.ShowMainWindow();
        var blocks = GridBlocks(window);

        var focus = Slot(blocks.Single(b => BlockTitle(b) == "Focus"));
        var call = Slot(blocks.Single(b => BlockTitle(b) == "Call"));
        var workout = Slot(blocks.Single(b => BlockTitle(b) == "Workout"));

        Assert.Equal(workout.Width / 2, focus.Width, tolerance: 1);
        Assert.Equal(focus.Width, call.Width, tolerance: 1);
        Assert.Equal(focus.Right, call.X, tolerance: 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_MarkersAreLabelledLinesAtTheirTime()
    {
        var window = UiSnapshot.ShowMainWindow();

        var markers = window.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("markerRow")).ToList();
        Assert.Equal(2, markers.Count);
        var midday = markers.Single(m => m.Classes.Contains("checkpoint"));
        var slot = ((Visual)midday.GetVisualParent()!).Bounds;
        Assert.Equal(12 * 60, slot.Center.Y, 1);
        Assert.Contains("12:00  Midday", Texts(window));
        Assert.Contains("13:00  Last coffee", Texts(window));
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_HighlightsCurrentBlocksAndDimsPastOnes()
    {
        var window = UiSnapshot.ShowMainWindow(now: At(10, 30));
        var blocks = GridBlocks(window);

        Assert.Equal(new[] { "Focus" }, blocks.Where(b => b.Classes.Contains("current")).Select(BlockTitle));
        Assert.Contains(blocks, b => BlockTitle(b) == "Call" && b.Classes.Contains("past"));
        Assert.Contains(blocks, b => BlockTitle(b) == "Wake up" && b.Classes.Contains("past"));
        Assert.DoesNotContain(blocks, b => BlockTitle(b) == "Workout" && b.Classes.Contains("past"));
        Assert.Contains("Now: Focus", Texts(window));
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_NowLineSitsAtTheCurrentTime()
    {
        var window = UiSnapshot.ShowMainWindow(now: At(10, 30));

        var nowLine = Named<Control>(window, "NowLine");
        Assert.Equal(630, nowLine.Bounds.Center.Y, 1);
        Assert.Contains("10:30", Texts(window));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(10, 30)]
    [InlineData(2, 0)]
    [InlineData(22, 0)]
    public void Schedule_OpensScrolledSoTheNowLineIsVisible(int hour, int minute)
    {
        var window = UiSnapshot.ShowMainWindow(now: At(hour, minute));

        var scroller = Named<ScrollViewer>(window, "ScheduleScroller");
        var nowLine = Named<Control>(window, "NowLine");
        var y = nowLine.TranslatePoint(new Point(0, nowLine.Bounds.Height / 2), scroller)!.Value.Y;
        Assert.InRange(y, 0, scroller.Viewport.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_NowButtonScrollsBackToTheCurrentTime()
    {
        var window = UiSnapshot.ShowMainWindow(now: At(10, 30));
        var scroller = Named<ScrollViewer>(window, "ScheduleScroller");
        var opened = scroller.Offset;

        scroller.Offset = new Vector(0, 0);
        Named<Button>(window, "NowButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(opened.Y, scroller.Offset.Y, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_AtTwoAm_BothPiecesOfTheOvernightBlockAreCurrent()
    {
        var window = UiSnapshot.ShowMainWindow(now: At(2, 0));

        var sleep = GridBlocks(window).Where(b => BlockTitle(b) == "Sleep").ToList();
        Assert.Equal(2, sleep.Count);
        Assert.All(sleep, b => Assert.Contains("current", b.Classes));
        Assert.Equal(0, Slot(sleep.OrderBy(b => Slot(b).Y).First()).Y, 1);
        window.Close();
    }

    [Theory]
    [InlineData(630, 600, 1468, 430)]  // now about a third of the way down
    [InlineData(60, 600, 1468, 0)]     // near the top: no negative offset
    [InlineData(1400, 600, 1468, 868)] // near the end: stop at the bottom
    public void ScheduleScrollOffset_PutsNowAThirdDownWithinBounds(double nowY, double viewport, double extent, double expected)
    {
        Assert.Equal(expected, MainWindow.ScheduleScrollOffset(nowY, viewport, extent), 1);
    }

    [AvaloniaFact]
    public void Lists_ShowTheSeededTasksNotesAndLinks()
    {
        var window = UiSnapshot.ShowMainWindow();

        var texts = Texts(window).ToList();
        Assert.Contains("Write the weekly plan", texts);
        Assert.Contains("Groceries", texts);
        Assert.Contains("Docs", texts);
        window.Close();
    }

    [AvaloniaFact]
    public void Snapshots_GridAtMorningAndOvernight()
    {
        var morning = UiSnapshot.ShowMainWindow(now: At(10, 30));
        UiSnapshot.Render(morning, "grid-1030");
        morning.Close();

        var night = UiSnapshot.ShowMainWindow(now: At(2, 0));
        UiSnapshot.Render(night, "grid-0200");
        night.Close();
    }
}
