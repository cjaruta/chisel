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

// In layout edit mode each title bar has a remove button, and the toolbar's "Widget Library" button opens a
// searchable list: a side panel on wide windows, an overlay on narrow ones. Adding from it keeps it open.
public class WidgetLibraryHeadlessTests
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

    private static Border? LibraryPanel(Window window) => window.GetVisualDescendants().OfType<Border>().SingleOrDefault(b => b.Name == "LibraryPanel");

    private static bool LibraryShown(Window window) => LibraryPanel(window)?.IsEffectivelyVisible == true;

    private static void OpenLibrary(Window window) => ClickControl(window, Named<Button>(window, "LibraryButton"));

    private static IEnumerable<string> RowTitles(Window window, string list) =>
        Named<ItemsControl>(window, list).GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("libraryRow")).Select(b => ((WidgetLibraryRow)b.DataContext!).Title);

    private static Button AddButton(Window window, string id) =>
        Named<ItemsControl>(window, "LibraryAvailable").GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("libraryAdd") && (string?)b.CommandParameter == id);

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

    [AvaloniaFact]
    public void LibraryButton_IsOnlyInEditMode_AndTheOldTrayIsGone()
    {
        var window = UiSnapshot.ShowMainWindow();
        Assert.False(Named<Button>(window, "LibraryButton").IsEffectivelyVisible);
        Assert.Equal("Widget Library", Named<Button>(window, "LibraryButton").Content);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "TrayToggle" or "LayoutTray");

        EnterEditMode(window);

        Assert.True(Named<Button>(window, "LibraryButton").IsEffectivelyVisible);
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.True(f.RemoveButton.IsEffectivelyVisible));
        window.Close();
    }

    [AvaloniaFact]
    public void TheLastWidgetsRemoveButton_IsDisabled()
    {
        var window = ShowEditing();
        Remove(window, "schedule");
        Remove(window, "tasks");
        Remove(window, "notes");

        Assert.Equal("scripture", Order(window));
        Assert.False(Frame(window, "scripture").RemoveButton.IsEffectivelyEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void Library_OpensOverTheDashboard_WithoutMovingAnyWidget()
    {
        var window = ShowEditing();
        Rect[] Positions() => Host(window).Frames.SelectMany(c => c).Select(f => InWindow(f, window)).ToArray();
        var before = Positions();
        Assert.False(LibraryShown(window));

        OpenLibrary(window);

        Assert.True(LibraryShown(window));
        Assert.Equal(before, Positions());
        Assert.Equal(new[] { "Calculator", "Calendar", "Clipboard history", "Clock", "Converter", "Countdown", "Focus timer", "Habits", "Media", "Picture frame", "System monitor", "Weather" }, RowTitles(window, "LibraryAvailable"));
        Assert.Equal(new[] { "Daily verse", "Notes", "Tasks", "Today" }, RowTitles(window, "LibraryOnDashboard"));
        window.Close();
    }

    [AvaloniaFact]
    public void AddingFromTheLibrary_PlacesTheWidget_AndKeepsTheLibraryOpen()
    {
        var window = ShowEditing();
        OpenLibrary(window);

        ClickControl(window, AddButton(window, WidgetRegistry.Clock));

        Assert.True(LibraryShown(window));
        Assert.Equal("schedule,clock | tasks,notes,scripture", Order(window));
        Assert.DoesNotContain("Clock", RowTitles(window, "LibraryAvailable"));
        Assert.Contains("Clock", RowTitles(window, "LibraryOnDashboard"));
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_ClosesTheLibraryFirst_AndTheCloseButtonDoesToo_WithoutLeavingEditMode()
    {
        var window = ShowEditing();
        OpenLibrary(window);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(LibraryShown(window));
        Assert.True(ViewModel(window).Layout.IsEditing);

        OpenLibrary(window);
        ClickControl(window, Named<Button>(window, "LibraryCloseButton"));
        Assert.False(LibraryShown(window));
        Assert.True(ViewModel(window).Layout.IsEditing);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void Searching_FiltersBothGroups_AndShowsAnEmptyState()
    {
        var window = ShowEditing();
        OpenLibrary(window);
        var vm = ViewModel(window).Library;

        vm.Query = "o";
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<TextBlock>(window, "LibraryEmpty").IsEffectivelyVisible);

        vm.Query = "zzz nothing";
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<TextBlock>(window, "LibraryEmpty").IsEffectivelyVisible);
        Assert.Empty(RowTitles(window, "LibraryAvailable"));
        window.Close();
    }

    [AvaloniaFact]
    public void AfterDoneAndReopening_ARemovedWidgetStaysAvailable()
    {
        var repository = new MemoryLayoutRepository();
        var first = ShowEditing(repository: repository);
        Remove(first, "notes");
        ViewModel(first).Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        first.Close();

        var reopened = UiSnapshot.ShowMainWindow(layoutRepository: repository);
        EnterEditMode(reopened);
        OpenLibrary(reopened);

        Assert.Equal("schedule | tasks", Order(reopened));
        Assert.Contains("Notes", RowTitles(reopened, "LibraryAvailable"));
        reopened.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(960, 760)]
    public void Snapshot_LibraryOpen(int width, int height)
    {
        var window = ShowEditing(width, height);
        Remove(window, "notes");
        OpenLibrary(window);
        Assert.True(LibraryShown(window));
        var panel = InWindow(LibraryPanel(window)!, window);
        Assert.True(panel.Left >= 0 && panel.Right <= width, "the library fits inside the window");
        UiSnapshot.Render(window, $"widget-library-{width}x{height}");
        window.Close();
    }
}
