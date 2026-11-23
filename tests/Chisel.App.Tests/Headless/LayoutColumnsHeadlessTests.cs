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

// While a widget is dragged in layout edit mode, the dashboard's outer edges and the dividers between columns are
// new-column zones: entering one shows a phantom column, and dropping there makes a new column.
public class LayoutColumnsHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static string Order(Window window) =>
        string.Join(" | ", Host(window).Frames.Select(c => string.Join(",", c.Select(f => f.WidgetId))));

    private static Border Phantom(Window window) => Named<Border>(window, "DropPreview");

    private static Rect[] Positions(Window window) =>
        Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();

    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        // Measured from the center so a widget's own scale (the home slot's inset) doesn't count as movement.
        new(visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value
            - new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), visual.Bounds.Size);

    private static MainWindow ShowEditing(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Point Bar(Window window, string id) => InWindow(Frame(window, id).EditBar, window).Center;

    // A point inside the new-column zone at a gap (0 = left edge, column count = right edge), level with the columns.
    private static Point ZoneCenter(Window window, int index)
    {
        var host = InWindow(Host(window), window);
        var columns = Host(window).Frames.Select(c => InWindow(c[0], window)).ToList();
        var y = columns[0].Center.Y;
        if (index == 0)
        {
            return new Point(host.Left + host.Width * 0.05, y);
        }
        if (index == columns.Count)
        {
            return new Point(host.Right - host.Width * 0.05, y);
        }
        return new Point((columns[index - 1].Right + columns[index].Left) / 2, y);
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

    // Picks a widget up by its title bar and moves it a little, so the drag (and the zones) start.
    private static void PickUp(Window window, string id)
    {
        var from = Bar(window, id);
        Press(window, from);
        MoveTo(window, new Point(from.X + 15, from.Y + 15));
    }

    private static void DragToNewColumn(Window window, string id, int index)
    {
        PickUp(window, id);
        var zone = ZoneCenter(window, index);
        MoveTo(window, zone);
        Release(window, zone);
    }

    [AvaloniaFact]
    public void Phantom_ShowsOnlyWhileTheDragIsInsideANewColumnZone()
    {
        var window = ShowEditing();
        Assert.False(Phantom(window).IsVisible);

        PickUp(window, "notes");
        Assert.False(Phantom(window).IsVisible);

        MoveTo(window, ZoneCenter(window, 2));
        Assert.True(Phantom(window).IsVisible);
        MoveTo(window, Within(window, "notes"));
        Assert.False(Phantom(window).IsVisible);

        Release(window, Bar(window, "notes"));
        Assert.False(Phantom(window).IsVisible);
        window.Close();
    }

    private static Point Within(Window window, string id) => InWindow(Frame(window, id), window).Center;

    [AvaloniaFact]
    public void HoveringTheOuterEdge_ShowsAPhantomColumnAtTheEdge()
    {
        var window = ShowEditing();
        PickUp(window, "notes");

        MoveTo(window, ZoneCenter(window, 0));

        var phantom = InWindow(Phantom(window), window);
        var host = InWindow(Host(window), window);
        Assert.Equal(host.Left, phantom.Left, 1.5);
        Assert.InRange(phantom.Width, host.Width / 4, host.Width / 2);
        var label = Named<Border>(window, "DropPreviewCaption");
        Assert.Contains("New column", label.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Release(window, ZoneCenter(window, 0));
        window.Close();
    }

    [AvaloniaFact]
    public void HoveringADivider_ShowsAPhantomColumnOnIt()
    {
        var window = ShowEditing();
        PickUp(window, "notes");

        MoveTo(window, ZoneCenter(window, 1));

        var phantom = InWindow(Phantom(window), window);
        Assert.True(phantom.Left < ZoneCenter(window, 1).X && ZoneCenter(window, 1).X < phantom.Right);
        UiSnapshot.Render(window, "layout-new-column-drag");
        Release(window, ZoneCenter(window, 1));
        Assert.Equal("schedule | notes | tasks,scripture", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingAtTheRightEdge_CreatesAThirdColumn()
    {
        var window = ShowEditing();
        Assert.Equal(2, Host(window).Frames.Count);

        DragToNewColumn(window, "notes", 2);

        Assert.Equal(3, Host(window).Frames.Count);
        Assert.Equal("schedule | tasks,scripture | notes", Order(window));
        Assert.True(InWindow(Frame(window, "notes"), window).Left > InWindow(Frame(window, "tasks"), window).Right);
        Assert.False(Phantom(window).IsVisible);
        UiSnapshot.Render(window, "layout-three-columns-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingAtTheLeftEdge_CreatesAColumnThere()
    {
        var window = ShowEditing();

        DragToNewColumn(window, "scripture", 0);

        Assert.Equal("scripture | schedule | tasks,notes", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DuringADrag_NoColumnOrWidgetMoves()
    {
        var window = ShowEditing();
        var before = Positions(window);

        PickUp(window, "notes");
        MoveTo(window, ZoneCenter(window, 1));
        Assert.Equal(before, Positions(window));
        MoveTo(window, ZoneCenter(window, 2));
        Assert.Equal(before, Positions(window));
        MoveTo(window, Within(window, "schedule"));
        Assert.Equal(before, Positions(window));

        Release(window, new Point(100, 30));
        Assert.Equal(before, Positions(window));
        window.Close();
    }

    [AvaloniaFact]
    public void TooNarrowForAnotherColumn_NoPhantomIsShown()
    {
        var window = ShowEditing(820, 650);

        PickUp(window, "notes");
        MoveTo(window, ZoneCenter(window, 2));

        Assert.False(Phantom(window).IsVisible);
        Release(window, Bar(window, "notes"));
        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptiedColumn_CollapsesAsSoonAsTheDropCompletes_AndTheRestFillTheWidth()
    {
        var window = ShowEditing();
        DragToNewColumn(window, "notes", 2);
        Assert.Equal(3, Host(window).Frames.Count);
        UiSnapshot.Render(window, "collapse-before-1200x760");
        var hostWidth = Host(window).Bounds.Width;

        // Notes is alone in the third column; drop it under Quick links.
        PickUp(window, "notes");
        var links = InWindow(Frame(window, "scripture"), window);
        var below = new Point(links.Center.X, links.Bottom - 10);
        MoveTo(window, below);
        Assert.Equal(3, Host(window).Frames.Count); // only dimmed while dragging
        Release(window, below);

        Assert.Equal(2, Host(window).Frames.Count);
        Assert.Equal("schedule | tasks,scripture,notes", Order(window));
        var schedule = InWindow(Frame(window, "schedule"), window);
        var right = InWindow(Frame(window, "tasks"), window);
        var host = InWindow(Host(window), window);
        Assert.Equal(host.Left, schedule.Left, 1.5);
        Assert.Equal(host.Right, right.Right, 1.5);
        Assert.Equal(1.3, schedule.Width / right.Width, 1);
        Assert.Equal(hostWidth, Host(window).Bounds.Width);
        Assert.True(ViewModel(window).Layout.IsEditing);
        UiSnapshot.Render(window, "collapse-after-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void AnInvalidDropOrEscape_LeavesEveryColumnAsItWas()
    {
        var window = ShowEditing();
        DragToNewColumn(window, "notes", 2);
        var before = Order(window);

        PickUp(window, "notes");
        Release(window, new Point(100, 30)); // over the clock
        Assert.Equal(before, Order(window));

        PickUp(window, "notes");
        var tasks = InWindow(Frame(window, "tasks"), window);
        MoveTo(window, tasks.Center);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Release(window, tasks.Center);
        Assert.Equal(before, Order(window));

        PickUp(window, "notes");
        MoveTo(window, ZoneCenter(window, 0));
        Assert.True(Phantom(window).IsVisible);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Release(window, ZoneCenter(window, 0));
        Assert.False(Phantom(window).IsVisible);
        Assert.Equal(before, Order(window));
        Assert.True(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void ThreeColumnsInEditMode_AtTheWideSize()
    {
        var window = ShowEditing(1904, 984);

        DragToNewColumn(window, "notes", 2);

        Assert.Equal(3, Host(window).Frames.Count);
        UiSnapshot.Render(window, "layout-three-columns-1904x984");
        window.Close();
    }
}
