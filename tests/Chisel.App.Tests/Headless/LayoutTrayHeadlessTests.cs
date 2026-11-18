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
using Chisel.Core.Layout;

namespace Chisel.App.Tests.Headless;

// In layout edit mode each title bar has a remove button, and the edit toolbar holds a tray of the widgets
// that are not on the dashboard: drag one onto the dashboard, or click it to add it to the first column.
public class LayoutTrayHeadlessTests
{
    private sealed class MemoryLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

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

    private static List<Border> TrayCards(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("trayCard") && b.IsEffectivelyVisible && ((WidgetDefinition)b.DataContext!).Id != WidgetRegistry.Clock && ((WidgetDefinition)b.DataContext!).Id != WidgetRegistry.Weather).ToList();

    private static Border TrayCard(Window window, string id) =>
        TrayCards(window).Single(b => ((WidgetDefinition)b.DataContext!).Id == id);

    private static void EnterEditMode(Window window)
    {
        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static MainWindow ShowEditing(double width = 1200, double height = 760, IWidgetLayoutRepository? repository = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, layoutRepository: repository);
        EnterEditMode(window);
        return window;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickControl(Window window, Control control) => Click(window, InWindow(control, window).Center);

    private static void Remove(Window window, string id) => ClickControl(window, Frame(window, id).RemoveButton);

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

    [AvaloniaFact]
    public void RemoveButtonsAndTray_AreOnlyInEditMode()
    {
        var window = UiSnapshot.ShowMainWindow();
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.False(f.RemoveButton.IsEffectivelyVisible));
        Assert.False(Named<Control>(window, "LayoutTray").IsEffectivelyVisible);

        EnterEditMode(window);

        Assert.All(Host(window).Frames.SelectMany(c => c), f =>
        {
            Assert.True(f.RemoveButton.IsEffectivelyVisible);
            Assert.Equal("Remove from dashboard", ToolTip.GetTip(f.RemoveButton));
        });
        // Only the clock waits in the tray; none of the four original widgets are hidden yet.
        Assert.Empty(TrayCards(window));
        window.Close();
    }

    [AvaloniaFact]
    public void RemovingAWidget_TakesItOffTheDashboardAndPutsItInTheTray()
    {
        var window = ShowEditing();

        Remove(window, "notes");

        Assert.Equal("schedule | tasks,links", Order(window));
        Assert.True(Named<Control>(window, "LayoutTray").IsEffectivelyVisible);
        Assert.Equal(new[] { "notes" }, TrayCards(window).Select(c => ((WidgetDefinition)c.DataContext!).Id));
        Assert.Contains("Notes", TrayCard(window, "notes").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        window.Close();
    }

    [AvaloniaFact]
    public void TheLastWidgetsRemoveButton_IsDisabled()
    {
        var window = ShowEditing();
        Remove(window, "schedule");
        Remove(window, "tasks");
        Remove(window, "notes");

        Assert.Equal("links", Order(window));
        Assert.False(Frame(window, "links").RemoveButton.IsEffectivelyEnabled);
        Remove(window, "links");
        Assert.Equal("links", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingATrayCard_PlacesTheWidgetAtTheIndicatedSlot()
    {
        var window = ShowEditing();
        Remove(window, "notes");
        var card = InWindow(TrayCard(window, "notes"), window).Center;
        var target = InWindow(Frame(window, "tasks"), window);
        var above = new Point(target.Center.X, target.Top + 20);

        Press(window, card);
        MoveTo(window, new Point(card.X + 10, card.Y + 30));
        MoveTo(window, above);

        Assert.Equal("schedule | tasks,links", Order(window));
        Assert.True(Named<Border>(window, "DropIndicator").IsVisible);
        Assert.True(Named<Border>(window, "DragFloat").IsVisible);
        Release(window, above);

        Assert.Equal("schedule | notes,tasks,links", Order(window));
        Assert.False(Named<Border>(window, "DragFloat").IsVisible);
        Assert.Empty(TrayCards(window));
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingATrayCard_ToTheRightEdge_MakesAColumn()
    {
        var window = ShowEditing();
        Remove(window, "notes");
        var card = InWindow(TrayCard(window, "notes"), window).Center;

        Press(window, card);
        MoveTo(window, new Point(card.X, card.Y + 40));
        var host = InWindow(Host(window), window);
        var zone = new Point(host.Right - host.Width * 0.05, InWindow(Frame(window, "schedule"), window).Center.Y);
        MoveTo(window, zone);
        Release(window, zone);

        Assert.Equal("schedule | tasks,links | notes", Order(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ATrayDragDroppedNowhereOrCancelled_LeavesItInTheTray()
    {
        var window = ShowEditing();
        Remove(window, "notes");

        var card = InWindow(TrayCard(window, "notes"), window).Center;
        Press(window, card);
        MoveTo(window, new Point(100, 30));
        Release(window, new Point(100, 30));
        Assert.Equal("schedule | tasks,links", Order(window));

        card = InWindow(TrayCard(window, "notes"), window).Center;
        Press(window, card);
        MoveTo(window, InWindow(Frame(window, "tasks"), window).Center);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Release(window, InWindow(Frame(window, "tasks"), window).Center);

        Assert.Equal("schedule | tasks,links", Order(window));
        Assert.Single(TrayCards(window));
        Assert.True(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingATrayCard_AddsTheWidgetToTheFirstColumn()
    {
        var window = ShowEditing();
        Remove(window, "links");

        ClickControl(window, TrayCard(window, "links"));

        Assert.Equal("schedule,links | tasks,notes", Order(window));
        Assert.Empty(TrayCards(window));
        window.Close();
    }

    [AvaloniaFact]
    public void OpeningAndClosingTheTray_MovesNoWidget()
    {
        var window = ShowEditing();
        Remove(window, "notes");
        Rect[] Positions() => Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();
        var open = Positions();
        Assert.NotEmpty(TrayCards(window));

        ClickControl(window, Named<Button>(window, "TrayToggle"));
        Assert.Empty(TrayCards(window));
        Assert.Equal(open, Positions());

        ClickControl(window, Named<Button>(window, "TrayToggle"));
        Assert.NotEmpty(TrayCards(window));
        Assert.Equal(open, Positions());
        window.Close();
    }

    [AvaloniaFact]
    public void AfterDoneAndReopening_AHiddenWidgetStaysHidden()
    {
        var repository = new MemoryLayoutRepository();
        var first = ShowEditing(repository: repository);
        Remove(first, "notes");
        ViewModel(first).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        first.Close();

        var reopened = UiSnapshot.ShowMainWindow(layoutRepository: repository);

        Assert.Equal("schedule | tasks,links", Order(reopened));
        EnterEditMode(reopened);
        Assert.Equal(new[] { "notes" }, TrayCards(reopened).Select(c => ((WidgetDefinition)c.DataContext!).Id));
        reopened.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1904, 984)]
    public void Snapshot_TrayOpenWithTwoWidgetsHidden(int width, int height)
    {
        var window = ShowEditing(width, height);
        Remove(window, "notes");
        Remove(window, "links");

        var toolbar = InWindow(Named<Border>(window, "LayoutToolbar"), window);
        var clock = InWindow(Named<StackPanel>(window, "ClockPanel"), window);
        var gear = InWindow(Named<Button>(window, "CustomizeLayoutButton"), window);
        Assert.True(toolbar.Left >= clock.Right && toolbar.Right <= gear.Left, "the toolbar with the tray fits between the clock and the buttons");
        Assert.Equal(2, TrayCards(window).Count);
        UiSnapshot.Render(window, $"layout-tray-{width}x{height}");
        window.Close();
    }
}
