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

// While a widget is dragged in layout edit mode, thin "New column" zones appear at the dashboard's edges and
// between columns; dropping on one makes a new column there.
public class LayoutColumnsHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static string Order(Window window) =>
        string.Join(" | ", Host(window).Frames.Select(c => string.Join(",", c.Select(f => f.WidgetId))));

    private static List<Control> Zones(Window window) =>
        window.GetVisualDescendants().OfType<Control>().Where(c => c.Name?.StartsWith("NewColumnZone") == true && c.IsVisible).ToList();

    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static MainWindow ShowEditing(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Point Bar(Window window, string id) => InWindow(Frame(window, id).EditBar, window).Center;

    private static Point ZoneCenter(Window window, int index) => InWindow(Named<Control>(window, $"NewColumnZone{index}"), window).Center;

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
    public void NewColumnZones_AppearOnlyWhileDragging()
    {
        var window = ShowEditing();
        Assert.Empty(Zones(window));

        PickUp(window, "notes");
        Assert.Equal(new[] { "NewColumnZone0", "NewColumnZone1", "NewColumnZone2" }, Zones(window).Select(z => z.Name));

        Release(window, Bar(window, "notes"));
        Assert.Empty(Zones(window));
        window.Close();
    }

    [AvaloniaFact]
    public void NewColumnZones_SitAtTheEdgesAndBetweenTheColumns()
    {
        var window = ShowEditing();
        PickUp(window, "notes");

        var schedule = InWindow(Frame(window, "schedule"), window);
        var tasks = InWindow(Frame(window, "tasks"), window);
        Assert.InRange(ZoneCenter(window, 0).X, schedule.Left - 16, schedule.Left + 4);
        Assert.InRange(ZoneCenter(window, 1).X, schedule.Right, tasks.Left);
        Assert.InRange(ZoneCenter(window, 2).X, tasks.Right - 4, tasks.Right + 16);
        Release(window, Bar(window, "notes"));
        window.Close();
    }

    [AvaloniaFact]
    public void HoveringAZone_HighlightsItWithANewColumnLabel()
    {
        var window = ShowEditing();
        PickUp(window, "notes");

        MoveTo(window, ZoneCenter(window, 2));

        Assert.Contains("active", Named<Control>(window, "NewColumnZone2").Classes);
        var label = Named<Border>(window, "NewColumnLabel");
        Assert.True(label.IsVisible);
        Assert.Contains("New column", label.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.False(Named<Border>(window, "DropIndicator").IsVisible);
        UiSnapshot.Render(window, "layout-new-column-drag");
        Release(window, ZoneCenter(window, 2));
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingAtTheRightEdge_CreatesAThirdColumn()
    {
        var window = ShowEditing();
        Assert.Equal(2, Host(window).Frames.Count);

        DragToNewColumn(window, "notes", 2);

        Assert.Equal(3, Host(window).Frames.Count);
        Assert.Equal("schedule | tasks,links | notes", Order(window));
        Assert.True(InWindow(Frame(window, "notes"), window).Left > InWindow(Frame(window, "tasks"), window).Right);
        Assert.Empty(Zones(window));
        UiSnapshot.Render(window, "layout-three-columns-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingAtTheLeftEdge_CreatesAColumnThere()
    {
        var window = ShowEditing();

        DragToNewColumn(window, "links", 0);

        Assert.Equal("links | schedule | tasks,notes", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DuringADrag_NoWidgetChangesSize()
    {
        var window = ShowEditing();
        Rect[] Positions() => Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();
        var before = Positions();

        PickUp(window, "notes");
        MoveTo(window, ZoneCenter(window, 1));
        MoveTo(window, ZoneCenter(window, 2));

        Assert.Equal(before, Positions());
        Release(window, new Point(100, 30));
        Assert.Equal(before, Positions());
        window.Close();
    }

    [AvaloniaFact]
    public void TooNarrowForAnotherColumn_NoZonesAreOffered()
    {
        var window = ShowEditing(820, 650);

        PickUp(window, "notes");

        Assert.Empty(Zones(window));
        Release(window, Bar(window, "notes"));
        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptiedColumn_ShowsDropHereInEditMode_AndIsGoneAfterDone()
    {
        var window = ShowEditing();
        PickUp(window, "schedule");
        var target = InWindow(Frame(window, "links"), window);
        var bottom = new Point(target.Center.X, target.Bottom - 10);
        MoveTo(window, bottom);
        Release(window, bottom);
        Assert.True(Named<Control>(window, "DropZone0").IsEffectivelyVisible);
        Assert.Equal(2, Host(window).Frames.Count);

        ViewModel(window).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(Host(window).Frames);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "DropZone0");
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
