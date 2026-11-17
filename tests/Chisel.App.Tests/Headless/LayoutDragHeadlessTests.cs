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

// In layout edit mode, dragging a widget's title bar moves it: a ghost follows the pointer, an accent line
// shows the landing gap, and dropping moves the widget in the working copy.
public class LayoutDragHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static string Order(Window window) =>
        string.Join(" | ", Host(window).Frames.Select(c => string.Join(",", c.Select(f => f.WidgetId))));

    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static MainWindow ShowEditing(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        var customize = Named<Button>(window, "CustomizeLayoutButton");
        var center = InWindow(customize, window).Center;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(ViewModel(window).Layout.IsEditing);
        return window;
    }

    private static Point Bar(Window window, string id) => InWindow(Frame(window, id).EditBar, window).Center;

    // A point a given fraction of the way down a widget's card.
    private static Point Within(Window window, string id, double fraction)
    {
        var bounds = InWindow(Frame(window, id), window);
        return new Point(bounds.Center.X, bounds.Top + bounds.Height * fraction);
    }

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

    private static void Drag(Window window, string id, Point to)
    {
        var from = Bar(window, id);
        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        MoveTo(window, to);
        Release(window, to);
    }

    private static void AssertNoDragVisuals(Window window)
    {
        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        Assert.False(Named<Border>(window, "DragGhost").IsVisible);
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.DoesNotContain("dragSource", f.Classes));
        Assert.False(ViewModel(window).Layout.IsDraggingWidget);
    }

    [AvaloniaFact]
    public void TitleBar_ShowsAHandCursor()
    {
        var window = ShowEditing();

        Assert.NotNull(Frame(window, "tasks").EditBar.Cursor);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingWithinAColumn_ReordersTheWidgets()
    {
        var window = ShowEditing();

        Drag(window, "links", Within(window, "tasks", 0.2));

        Assert.Equal("schedule | links,tasks,notes", Order(window));
        Assert.True(ViewModel(window).Layout.IsEditing);
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingDownWithinAColumn_LandsBelowTheTarget()
    {
        var window = ShowEditing();

        Drag(window, "tasks", Within(window, "notes", 0.8));

        Assert.Equal("schedule | notes,tasks,links", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingToAnotherColumn_MovesTheWidgetThere()
    {
        var window = ShowEditing();

        Drag(window, "tasks", Within(window, "schedule", 0.9));

        Assert.Equal("schedule,tasks | notes,links", Order(window));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(2)]
    public void AClickWithoutRealMovement_ChangesNothing(double nudge)
    {
        var window = ShowEditing();
        var bar = Bar(window, "notes");

        Press(window, bar);
        MoveTo(window, new Point(bar.X + nudge, bar.Y + nudge));
        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        Release(window, new Point(bar.X + nudge, bar.Y + nudge));

        Assert.Equal("schedule | tasks,notes,links", Order(window));
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingOutsideTheColumns_ChangesNothing()
    {
        var window = ShowEditing();

        Drag(window, "notes", new Point(100, 30)); // over the clock

        Assert.Equal("schedule | tasks,notes,links", Order(window));
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void MovingAColumnsOnlyWidgetAway_CollapsesThatColumnOnDrop()
    {
        var window = ShowEditing();

        Drag(window, "schedule", Within(window, "links", 0.9));

        Assert.Equal("tasks,notes,links,schedule", Order(window));
        Assert.True(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void DuringADrag_TheIndicatorAndGhostShowAndNothingElseMoves()
    {
        var window = ShowEditing();
        Rect[] Positions() => Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();
        var before = Positions();

        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X + 20, from.Y - 20));
        MoveTo(window, Within(window, "tasks", 0.8));

        var indicator = Named<Border>(window, "DropIndicator");
        Assert.True(indicator.IsVisible);
        Assert.True(Named<Border>(window, "DragGhost").IsVisible);
        Assert.Contains("dragSource", Frame(window, "links").Classes);
        // The line sits in the gap between tasks and notes.
        var line = InWindow(indicator, window);
        Assert.InRange(line.Center.Y, InWindow(Frame(window, "tasks"), window).Bottom, InWindow(Frame(window, "notes"), window).Top);
        Assert.Equal(before, Positions());

        MoveTo(window, Within(window, "schedule", 0.3));
        Assert.Equal(before, Positions());
        Assert.Equal("schedule | tasks,notes,links", Order(window));

        Release(window, Within(window, "schedule", 0.3));
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void OverItsOwnPlace_NoIndicatorShows()
    {
        var window = ShowEditing();
        var from = Bar(window, "notes");
        Press(window, from);
        MoveTo(window, new Point(from.X + 20, from.Y + 20));

        MoveTo(window, Within(window, "notes", 0.5));

        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        Release(window, Within(window, "notes", 0.5));
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeDuringADrag_CancelsOnlyTheDrag()
    {
        var window = ShowEditing();
        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X, from.Y - 30));
        MoveTo(window, Within(window, "tasks", 0.2));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        AssertNoDragVisuals(window);
        Release(window, Within(window, "tasks", 0.2));

        Assert.Equal("schedule | tasks,notes,links", Order(window));
        Assert.True(ViewModel(window).Layout.IsEditing);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void DoneKeepsTheNewOrder_CancelRestoresIt()
    {
        var window = ShowEditing();
        Drag(window, "links", Within(window, "tasks", 0.2));
        ViewModel(window).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("schedule | links,tasks,notes", Order(window));
        AssertNoDragVisuals(window);
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.Equal(1, f.Opacity));

        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Drag(window, "schedule", Within(window, "notes", 0.2));
        ViewModel(window).Layout.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("schedule | links,tasks,notes", Order(window));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1904, 984)]
    public void Snapshot_MidDrag(int width, int height)
    {
        var window = ShowEditing(width, height);
        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X - 20, from.Y - 40));
        MoveTo(window, Within(window, "tasks", 0.7));

        UiSnapshot.Render(window, $"layout-drag-{width}x{height}");
        Release(window, Within(window, "tasks", 0.7));
        window.Close();
    }
}
