using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// Removing a widget in edit mode must leave no empty box behind: the card leaves the tree and the
// neighbours reflow in the same pass, with no gap overlay and no delayed cleanup.
public class LayoutRemovalGhostTests
{
    private static LayoutHost Host(Window w) => w.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window w, string id) =>
        Host(w).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static MainWindow ShowEditing()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        ((MainViewModel)window.DataContext!).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseMove(p);
        w.MouseDown(p, Avalonia.Input.MouseButton.Left);
        w.MouseUp(p, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle(Window w)
    {
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
    }

    private static string Describe(Window w) => string.Join("\n", Host(w).GetVisualDescendants().OfType<WidgetFrame>()
        .Select(f => $"{f.WidgetId} parent={f.Parent?.GetType().Name} bounds={f.Bounds} cls={string.Join(' ', f.Classes)}"));

    private static void AssertClean(Window w, string removed, string columnIds)
    {
        Settle(w);
        var all = Host(w).GetVisualDescendants().OfType<WidgetFrame>().ToList();
        var ghost = all.FirstOrDefault(f => f.WidgetId == removed);
        Assert.True(ghost is null || !ghost.IsEffectivelyVisible, "removed frame still in tree:\n" + Describe(w));
        foreach (var column in Host(w).Frames.Where(c => c.Any()))
        {
            var frames = column.ToList();
            var first = frames.First().TranslatePoint(new Point(0, 0), Host(w))!.Value.Y;
            var last = frames.Last();
            var bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), Host(w))!.Value.Y;
            var scroller = frames[0].GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(Math.Abs((bottom - first) - scroller.Bounds.Height) < 1.5,
                $"column {string.Join(",", frames.Select(f => f.WidgetId))} leaves a gap: {bottom - first} vs {scroller.Bounds.Height}\n" + Describe(w));
        }
    }

    [AvaloniaFact]
    public void Removing_LeavesNoGhostOrGap()
    {
        var window = ShowEditing();
        Click(window, Frame(window, "notes").RemoveButton);
        AssertClean(window, "notes", "");
        AssertClean(window, "notes", "");
        window.Close();
    }

    [AvaloniaFact]
    public void Removing_TheLastWidgetInAColumn_CollapsesIt()
    {
        var window = ShowEditing();
        Click(window, Frame(window, "schedule").RemoveButton);
        AssertClean(window, "schedule", "");
        Assert.Single(Host(window).Frames, c => c.Any());
        window.Close();
    }

    [AvaloniaFact]
    public void AddingBackFromTheLibrary_LeavesNoGhost()
    {
        var window = ShowEditing();
        Click(window, Frame(window, "notes").RemoveButton);
        Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LibraryButton"));
        var add = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("libraryAdd") && (string?)b.CommandParameter == "notes");
        add.BringIntoView(); // the library list is taller than the window
        Dispatcher.UIThread.RunJobs();
        Click(window, add);
        AssertClean(window, "none", "");
        Assert.Equal(4, Host(window).GetVisualDescendants().OfType<WidgetFrame>().Count());
        window.Close();
    }
}
