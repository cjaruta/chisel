using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Chisel.App.Tests.Headless;

// Checks the visual tree, not pixels, so results don't depend on fonts or rendering.
public class MainWindowHeadlessTests
{
    private static IEnumerable<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);

    private static List<Border> TimelineRows(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timelineRow")).ToList();

    private static string RowTitle(Border row) =>
        row.GetVisualDescendants().OfType<TextBlock>().Last().Text ?? "";

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
    public void Schedule_ListsBlocksAndMarkersInTimeOrder()
    {
        var window = UiSnapshot.ShowMainWindow();

        Assert.Equal(
            new[] { "Wake up", "Focus", "Last coffee", "Workout", "Sleep" },
            TimelineRows(window).Select(RowTitle));
        window.Close();
    }

    [AvaloniaFact]
    public void Schedule_MarksOnlyTheCurrentBlockAsCurrent()
    {
        var window = UiSnapshot.ShowMainWindow();

        var current = Assert.Single(TimelineRows(window), r => r.Classes.Contains("current"));
        Assert.Equal("Focus", RowTitle(current));
        Assert.Contains("Now: Focus", Texts(window));
        window.Close();
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
    public void Schedule_AtTwoAm_ShowsTheOvernightBlockFirstAndCurrent()
    {
        var window = UiSnapshot.ShowMainWindow(now: new DateTimeOffset(2000, 1, 1, 2, 0, 0, TimeSpan.Zero));
        UiSnapshot.Render(window, "main-0200");

        var rows = TimelineRows(window);
        Assert.Equal("Sleep", RowTitle(rows[0]));
        Assert.Contains("current", rows[0].Classes);
        window.Close();
    }
}
