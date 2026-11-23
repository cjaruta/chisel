using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// Short windows: the schedule scrolls inside its card, list widgets scroll inside theirs,
// and a column whose cards' minimum heights don't fit scrolls instead of cutting cards off.
public class LayoutScrollTests
{
    private static WidgetFrame Frame(Window window, string id) =>
        window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == id);

    [AvaloniaFact]
    public void ScheduleGrid_SitsInAScrollViewerThatFitsInsideTheWindow()
    {
        var window = UiSnapshot.ShowMainWindow(1000, 500);

        var grid = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "ScheduleGrid");
        var scroller = grid.FindAncestorOfType<ScrollViewer>()!;
        var bottom = scroller.TranslatePoint(new Point(0, scroller.Bounds.Height), window)!.Value.Y;

        Assert.True(bottom <= window.ClientSize.Height, $"schedule scroller ends at {bottom}, past {window.ClientSize.Height}");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("tasks")]
    public void ListWidget_ScrollsInsideItsCard(string id)
    {
        var window = UiSnapshot.ShowMainWindow(1000, 650);

        var frame = Frame(window, id);
        // Tasks keeps its add box pinned and scrolls its list inside the view.
        var scroller = frame.View.GetVisualDescendants().OfType<ScrollViewer>().First();

        Assert.Same(frame, scroller.FindAncestorOfType<WidgetFrame>());
        Assert.True(scroller.Bounds.Height <= frame.Bounds.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void TooShortForTheMinimumHeights_TheRightColumnScrollsAndCardsKeepTheirMinimums()
    {
        var window = UiSnapshot.ShowMainWindow(1000, 420);

        var column = Frame(window, "scripture").FindAncestorOfType<ScrollViewer>()!;
        Assert.Contains("layoutColumnScroller", column.Classes);
        Assert.True(column.Extent.Height > column.Viewport.Height);
        foreach (var id in new[] { "tasks", "notes", "scripture" })
        {
            Assert.True(Frame(window, id).Bounds.Height >= Frame(window, id).MinHeight - 0.5, id);
        }

        var bottom = column.TranslatePoint(new Point(0, column.Bounds.Height), window)!.Value.Y;
        Assert.True(bottom <= window.ClientSize.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void TallEnough_NoColumnScrolls()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);

        var columns = window.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.Classes.Contains("layoutColumnScroller")).ToList();

        Assert.Equal(2, columns.Count);
        Assert.All(columns, c => Assert.True(c.Extent.Height <= c.Viewport.Height + 0.5));
        window.Close();
    }
}
