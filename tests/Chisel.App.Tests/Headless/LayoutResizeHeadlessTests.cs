using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// In layout edit mode, the handle between two stacked widgets resizes them: the one above grows as the one
// below shrinks, the column keeps its height, widths never change, and neither goes under the minimum.
public class LayoutResizeHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static List<Control> Handles(Window window) =>
        window.GetVisualDescendants().OfType<Control>().Where(c => c.Classes.Contains("resizeHandle")).ToList();

    private static Control Handle(Window window, string aboveId) => Named<Control>(window, $"ResizeHandle_{aboveId}");

    private static Control Grip(Control handle) =>
        handle.GetVisualDescendants().OfType<Control>().Single(c => c.Classes.Contains("resizeGrip"));

    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static void EnterEditMode(Window window)
    {
        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static MainWindow ShowEditing(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        EnterEditMode(window);
        return window;
    }

    private static Point HandleCenter(Window window, string aboveId) => InWindow(Handle(window, aboveId), window).Center;

    private static void Press(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void MoveTo(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void DragHandle(Window window, string aboveId, double dy)
    {
        var start = HandleCenter(window, aboveId);
        Press(window, start);
        MoveTo(window, new Point(start.X, start.Y + dy / 2));
        MoveTo(window, new Point(start.X, start.Y + dy));
        Release(window, new Point(start.X, start.Y + dy));
    }

    private static double Height(Window window, string id) => InWindow(Frame(window, id), window).Height;

    private static double ColumnHeight(Window window) =>
        InWindow(Frame(window, "links"), window).Bottom - InWindow(Frame(window, "tasks"), window).Top;

    [AvaloniaFact]
    public void Handles_SitOnlyBetweenStackedWidgets()
    {
        var window = ShowEditing();

        Assert.Equal(new[] { "ResizeHandle_tasks", "ResizeHandle_notes" }, Handles(window).Select(h => h.Name));

        var handle = InWindow(Handle(window, "tasks"), window);
        Assert.True(handle.Top >= InWindow(Frame(window, "tasks"), window).Bottom - 0.5);
        Assert.True(handle.Bottom <= InWindow(Frame(window, "notes"), window).Top + 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Handles_AreUsableOnlyInEditMode()
    {
        var window = UiSnapshot.ShowMainWindow();
        Assert.All(Handles(window), h =>
        {
            Assert.False(h.IsHitTestVisible);
            Assert.False(Grip(h).IsEffectivelyVisible);
        });

        EnterEditMode(window);
        Assert.All(Handles(window), h =>
        {
            Assert.True(h.IsHitTestVisible);
            Assert.True(Grip(h).IsEffectivelyVisible);
        });

        ViewModel(window).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.All(Handles(window), h => Assert.False(h.IsHitTestVisible));
        window.Close();
    }

    [AvaloniaFact]
    public void Handle_ShowsAVerticalResizeCursor()
    {
        var window = ShowEditing();

        var hit = Handle(window, "tasks").GetVisualDescendants().OfType<Control>().Single(c => c.Classes.Contains("resizeHit"));

        Assert.NotNull(hit.Cursor);
        Assert.Equal(8, hit.Bounds.Height, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingAHandle_ResizesTheTwoNeighboursOppositeWays()
    {
        var window = ShowEditing();
        var tasks = Height(window, "tasks");
        var notes = Height(window, "notes");
        var links = Height(window, "links");
        var column = ColumnHeight(window);
        var widths = Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window).Width).ToArray();

        DragHandle(window, "tasks", 40);

        Assert.Equal(tasks + 40, Height(window, "tasks"), 0);
        Assert.Equal(notes - 40, Height(window, "notes"), 0);
        Assert.Equal(links, Height(window, "links"), 0);
        Assert.Equal(column, ColumnHeight(window), 0);
        Assert.Equal(widths, Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window).Width).ToArray());
        Assert.False(ViewModel(window).Layout.IsResizing);
        Assert.True(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingUp_GrowsTheWidgetBelow()
    {
        var window = ShowEditing();
        var notes = Height(window, "notes");
        var links = Height(window, "links");

        DragHandle(window, "notes", -30);

        Assert.Equal(notes - 30, Height(window, "notes"), 0);
        Assert.Equal(links + 30, Height(window, "links"), 0);
        window.Close();
    }

    [AvaloniaFact]
    public void TheNeighboursResizeLiveWhileDragging()
    {
        var window = ShowEditing();
        var tasks = Height(window, "tasks");
        var start = HandleCenter(window, "tasks");

        Press(window, start);
        MoveTo(window, new Point(start.X, start.Y + 25));

        Assert.True(ViewModel(window).Layout.IsResizing);
        Assert.Equal(tasks + 25, Height(window, "tasks"), 0);
        Release(window, new Point(start.X, start.Y + 25));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1000)]
    [InlineData(-1000)]
    public void TheDragStopsAtTheMinimumAndPushesNothingElse(double dy)
    {
        var window = ShowEditing(1904, 984);
        var links = Height(window, "links");
        var schedule = InWindow(Frame(window, "schedule"), window);

        DragHandle(window, "tasks", dy);

        var shrunk = dy > 0 ? "notes" : "tasks";
        Assert.Equal(WidgetRegistry.MinimumHeight, Height(window, shrunk), 0);
        Assert.Equal(links, Height(window, "links"), 0);
        Assert.Equal(schedule, InWindow(Frame(window, "schedule"), window));
        window.Close();
    }

    [AvaloniaFact]
    public void ResizedProportions_SurviveAWindowResize()
    {
        var window = ShowEditing(1200, 760);
        DragHandle(window, "tasks", 50);
        ViewModel(window).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var before = Height(window, "tasks") / Height(window, "notes");

        window.Width = 1904;
        window.Height = 984;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, Height(window, "tasks") / Height(window, "notes"), 2);
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeDuringAResize_PutsTheHeightsBackAndStaysInEditMode()
    {
        var window = ShowEditing();
        var tasks = Height(window, "tasks");
        var start = HandleCenter(window, "tasks");
        Press(window, start);
        MoveTo(window, new Point(start.X, start.Y + 40));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        MoveTo(window, new Point(start.X, start.Y + 80));
        Release(window, new Point(start.X, start.Y + 80));

        Assert.Equal(tasks, Height(window, "tasks"), 0);
        Assert.True(ViewModel(window).Layout.IsEditing);
        Assert.False(ViewModel(window).Layout.IsResizing);
        window.Close();
    }

    [AvaloniaFact]
    public void EnteringAndLeavingEditMode_MovesNothing()
    {
        var window = UiSnapshot.ShowMainWindow();
        Rect[] Positions() => Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();
        var before = Positions();

        EnterEditMode(window);
        var editing = Positions();
        ViewModel(window).Layout.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, editing);
        Assert.Equal(before, Positions());
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1904, 984)]
    [InlineData(1000, 650)]
    public void Snapshot_MidResize(int width, int height)
    {
        var window = ShowEditing(width, height);
        var start = HandleCenter(window, "tasks");
        Press(window, start);
        MoveTo(window, new Point(start.X, start.Y + 30));

        UiSnapshot.Render(window, $"layout-resize-{width}x{height}");
        Release(window, new Point(start.X, start.Y + 30));
        window.Close();
    }
}
