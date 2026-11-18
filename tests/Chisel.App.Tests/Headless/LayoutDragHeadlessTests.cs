using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// In layout edit mode, dragging a widget moves it: a floating snapshot follows the pointer, an amber line marks
// the slot in the hovered column (the board itself stays put), and dropping moves the widget in the working copy.
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
        // Measured from the center so a widget's own scale (the home slot's inset) doesn't count as movement.
        new(visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value
            - new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), visual.Bounds.Size);

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
        Assert.False(Named<Border>(window, "DragFloat").IsVisible);
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.DoesNotContain("dragSource", f.Classes));
        Assert.False(ViewModel(window).Layout.IsDraggingWidget);
    }

    [AvaloniaFact]
    public void Widget_ShowsAMoveCursor()
    {
        var window = ShowEditing();

        Assert.NotNull(Frame(window, "tasks").EditBar.Cursor);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingFromTheBodyOfAWidget_MovesIt()
    {
        var window = ShowEditing();
        var body = Within(window, "links", 0.8);
        var to = Within(window, "tasks", 0.2);

        Press(window, body);
        MoveTo(window, new Point(body.X + 10, body.Y + 10));
        MoveTo(window, to);
        Release(window, to);

        Assert.Equal("schedule | links,tasks,notes", Order(window));
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
        Assert.False(Named<Border>(window, "DragFloat").IsVisible);
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

    private static Rect[] Positions(Window window) =>
        Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();

    private static void AssertSamePositions(Rect[] expected, Rect[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, actual[i].X, 2);
            Assert.Equal(expected[i].Y, actual[i].Y, 2);
            Assert.Equal(expected[i].Width, actual[i].Width, 2);
            Assert.Equal(expected[i].Height, actual[i].Height, 2);
        }
    }

    [AvaloniaFact]
    public void CrossingAMidline_MovesTheIndicator_NotTheBoard()
    {
        var window = ShowEditing();
        var belowMiddle = Within(window, "tasks", 0.7);
        var aboveMiddle = Within(window, "tasks", 0.3);
        var tasks = InWindow(Frame(window, "tasks"), window);
        var before = Positions(window);
        var line = Named<Border>(window, "DropIndicator");
        Assert.False(line.IsVisible);

        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X + 20, from.Y - 20));
        Assert.True(Named<Border>(window, "DragFloat").IsVisible);
        Assert.Contains("dragSource", Frame(window, "links").Classes);

        MoveTo(window, belowMiddle);
        Assert.True(line.IsVisible);
        Assert.InRange(InWindow(line, window).Center.Y, tasks.Bottom - 2, tasks.Bottom + 20);
        AssertSamePositions(before, Positions(window));

        MoveTo(window, aboveMiddle);
        Assert.InRange(InWindow(line, window).Center.Y, tasks.Top - 20, tasks.Top + 2);
        AssertSamePositions(before, Positions(window));

        Release(window, aboveMiddle);
        Assert.False(line.IsVisible);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("tasks", 0.8)] // gap above the widget's own slot
    [InlineData("links", 0.2)] // gap below it
    public void GapsNextToTheWidgetsOwnSlot_ShowNoIndicator(string neighbour, double fraction)
    {
        var window = ShowEditing();
        var line = Named<Border>(window, "DropIndicator");
        var from = Bar(window, "notes");
        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));

        MoveTo(window, Within(window, neighbour, fraction));
        Assert.False(line.IsVisible);

        MoveTo(window, Within(window, "tasks", 0.2));
        Assert.True(line.IsVisible);
        Release(window, Within(window, "tasks", 0.2));
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingOnTheHomePlaceholder_KeepsTheLayout()
    {
        var window = ShowEditing();
        var from = Bar(window, "notes");
        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        var home = Within(window, "notes", 0.5);
        MoveTo(window, home);

        Assert.Contains("dragSource", Frame(window, "notes").Classes);
        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        Release(window, home);

        Assert.Equal("schedule | tasks,notes,links", Order(window));
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void Dropping_PutsTheWidgetAtTheIndicatedSlot()
    {
        var window = ShowEditing();
        var tasksTop = InWindow(Frame(window, "tasks"), window).Top;

        Drag(window, "links", Within(window, "tasks", 0.3));

        Assert.Equal("schedule | links,tasks,notes", Order(window));
        Assert.Equal(tasksTop, InWindow(Frame(window, "links"), window).Top, 1.5);
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeDuringADrag_CancelsOnlyTheDrag()
    {
        var window = ShowEditing();
        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X, from.Y - 30));
        var before = Positions(window);
        var over = Within(window, "tasks", 0.2);
        MoveTo(window, over);
        Assert.True(Named<Border>(window, "DropIndicator").IsVisible);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        AssertNoDragVisuals(window);
        AssertSamePositions(before, Positions(window));
        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        Release(window, over);

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

    [AvaloniaFact]
    public void PickedUpWidget_IsLifted_AndSettlesOnDrop()
    {
        var window = ShowEditing();
        var floating = Named<Border>(window, "DragFloat");
        var from = Bar(window, "links");
        Press(window, from);
        Assert.DoesNotContain("lifted", floating.Classes);

        MoveTo(window, new Point(from.X + 20, from.Y - 20));
        Assert.Contains("lifted", floating.Classes);
        var lifted = Assert.IsAssignableFrom<ITransform>(floating.RenderTransform).Value;
        Assert.InRange(Math.Sqrt(lifted.M11 * lifted.M11 + lifted.M12 * lifted.M12), 1.0001, 1.035);
        Assert.Equal(0, lifted.M12, 6);
        Assert.Equal(0, lifted.M21, 6);
        Assert.True(floating.BoxShadow[0].OffsetY > 4);

        Release(window, Within(window, "tasks", 0.7));
        Assert.DoesNotContain("lifted", floating.Classes);
        AssertNoDragVisuals(window);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Drop_AnimatesOnlyTheBorderBrush_WithoutFlashingTheCard()
    {
        var window = ShowEditing();
        var frame = Frame(window, "links");
        var background = frame.Background;
        Drag(window, "links", Within(window, "tasks", 0.7));

        // Right after the drop the card is fully painted: visible, opaque, content shown, no gap look.
        Assert.True(frame.IsVisible);
        Assert.Equal(1, frame.Opacity);
        var normal = Frame(window, "tasks").Content.Opacity;
        Assert.Equal(normal, frame.Content!.Opacity);
        Assert.DoesNotContain("dragSource", frame.Classes);
        Assert.Contains("dropped", frame.Classes);
        Assert.Equal(background, frame.Background);

        // The amber ease runs after the layout update, on the border brush only.
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("snap", frame.Classes);
        var border = frame.Transitions!.OfType<Avalonia.Animation.BrushTransition>().Single(t => t.Property == Avalonia.Controls.Border.BorderBrushProperty);
        Assert.Equal(80, border.Duration.TotalMilliseconds);
        Assert.Equal(normal, frame.Content.Opacity);
        Assert.True(frame.IsVisible);

        await Task.Delay(500);
        Assert.DoesNotContain("snap", frame.Classes);
        Assert.DoesNotContain("snapFade", frame.Classes);
        Assert.DoesNotContain("dropped", frame.Classes);
        Assert.Equal(normal, frame.Content.Opacity);
        window.Close();
    }

    [AvaloniaFact]
    public void Snapshot_Drop()
    {
        var window = ShowEditing();
        Drag(window, "links", Within(window, "tasks", 0.7));
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "layout-drop-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void Cancelled_Drag_LevelsTheFloat()
    {
        var window = ShowEditing();
        var floating = Named<Border>(window, "DragFloat");
        var from = Bar(window, "links");
        Press(window, from);
        MoveTo(window, new Point(from.X + 20, from.Y - 20));
        Assert.Contains("lifted", floating.Classes);

        ViewModel(window).Layout.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("lifted", floating.Classes);
        AssertNoDragVisuals(window);
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
