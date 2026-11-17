using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

// In layout edit mode, the vertical handle between two columns resizes them: one grows as its right
// neighbour shrinks, the total width stays, and neither goes under the minimum.
public class LayoutColumnResizeHeadlessTests
{
    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static Control Handle(Window window, int column) =>
        window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == $"ColumnResizeHandle_{column}");

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    // The first widget of each column stands for the column.
    private static double ColumnWidth(Window window, int column) =>
        InWindow(Host(window).Frames[column][0], window).Width;

    // A window with three columns (the notes widget moved into its own), in edit mode.
    private static MainWindow ShowThreeColumns()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var layout = ViewModel(window).Layout;
        layout.EnterEditCommand.Execute(null);
        layout.BeginWidgetDrag("notes");
        layout.DropWidgetInNewColumn(2);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void DragHandle(Window window, int column, double dx)
    {
        var start = InWindow(Handle(window, column), window).Center;
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + dx / 2, start.Y), RawInputModifiers.LeftMouseButton);
        window.MouseMove(new Point(start.X + dx, start.Y), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Window window, double x, double y)
    {
        window.MouseUp(new Point(x, y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DraggingAHandle_ResizesTheTwoNeighboursOppositeWays_AndKeepsTheTotal()
    {
        var window = ShowThreeColumns();
        var before = Enumerable.Range(0, 3).Select(c => ColumnWidth(window, c)).ToArray();
        var handle = InWindow(Handle(window, 0), window).Center;

        DragHandle(window, 0, 60);
        Release(window, handle.X + 60, handle.Y);

        var after = Enumerable.Range(0, 3).Select(c => ColumnWidth(window, c)).ToArray();
        Assert.InRange(after[0], before[0] + 58.5, before[0] + 61.5);
        Assert.InRange(after[1], before[1] - 61.5, before[1] - 58.5);
        Assert.Equal(before[2], after[2], 1);
        Assert.InRange(after.Sum(), before.Sum() - 1, before.Sum() + 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_RestoresTheStartingWidths_AndStaysInEditMode()
    {
        var window = ShowThreeColumns();
        var before = ColumnWidth(window, 0);

        DragHandle(window, 0, 60);
        Assert.NotEqual(before, ColumnWidth(window, 0), 1);
        ViewModel(window).CloseOverlaysCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, ColumnWidth(window, 0), 1);
        Assert.True(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void ColumnsStopAtTheMinimumWidth()
    {
        var window = ShowThreeColumns();
        var handle = InWindow(Handle(window, 0), window).Center;

        DragHandle(window, 0, -2000);
        Release(window, handle.X - 2000, handle.Y);

        Assert.Equal(260, ColumnWidth(window, 0), 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Handles_ShowOnlyInEditMode_AndMoveNothingWhenTheyAppear()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var handle = Handle(window, 0);
        var grip = handle.GetVisualDescendants().OfType<Control>().Single(c => c.Classes.Contains("columnResizeGrip"));
        var widths = Enumerable.Range(0, 2).Select(c => ColumnWidth(window, c)).ToArray();
        Assert.False(handle.IsHitTestVisible);
        Assert.False(grip.IsEffectivelyVisible);

        ViewModel(window).Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(handle.IsHitTestVisible);
        Assert.True(grip.IsEffectivelyVisible);
        Assert.Equal(widths, Enumerable.Range(0, 2).Select(c => ColumnWidth(window, c)).ToArray());
        window.Close();
    }

    [AvaloniaFact]
    public void Render_ThreeColumnsInEditMode()
    {
        var window = ShowThreeColumns();
        UiSnapshot.Render(window, "column-resize-handles");
        window.Close();
    }
}
