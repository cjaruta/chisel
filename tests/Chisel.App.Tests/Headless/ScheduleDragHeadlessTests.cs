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

// Pressing on empty grid space and dragging shows a live preview, then a pending block in the editor.
public class ScheduleDragHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static ScheduleViewModel Schedule(MainWindow window) => ((MainViewModel)window.DataContext!).Schedule;

    private static ScrollViewer Scroller(Window window) => Named<ScrollViewer>(window, "ScheduleScroller");

    // Shows the window scrolled so 10:00 is at the top, keeping midday on screen.
    private static MainWindow Show()
    {
        var window = UiSnapshot.ShowMainWindow();
        Scroller(window).Offset = new Vector(0, 600);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    // A point in the window at the given minute of the day, halfway across the block column.
    private static Point At(Window window, double minute)
    {
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        return blocks.TranslatePoint(new Point(blocks.Bounds.Width / 2, minute), window)!.Value;
    }

    private static void Press(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // Press at one minute of the day and drag to another, in a few steps.
    private static void Drag(Window window, double fromMinute, double toMinute)
    {
        Press(window, At(window, fromMinute));
        Move(window, At(window, fromMinute + (toMinute - fromMinute) / 3));
        Move(window, At(window, toMinute));
    }

    // The preview box's span on the grid, in minutes from midnight (1 px per minute).
    private static (double Start, double End) PreviewSpan(Window window)
    {
        var slot = Named<Border>(window, "DragPreview").Bounds;
        return (slot.Top, slot.Bottom);
    }

    private static void AssertSpan(Window window, double start, double end)
    {
        var (top, bottom) = PreviewSpan(window);
        Assert.Equal(start, top, tolerance: 2);
        Assert.Equal(end, bottom, tolerance: 2);
    }

    [AvaloniaFact]
    public void MidDrag_ThePreviewSpansTheDraggedTimesAndTheGuideLineHides()
    {
        var window = Show();

        Drag(window, 12 * 60, 15 * 60 + 15);

        Assert.True(Named<Border>(window, "DragPreview").IsVisible);
        AssertSpan(window, 12 * 60, 15 * 60 + 15);
        Assert.Equal("12:00-15:15", Named<TextBlock>(window, "DragPreviewLabel").Text);
        Assert.False(Named<Control>(window, "HoverLine").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        UiSnapshot.Render(window, "schedule-drag-preview");
        window.Close();
    }

    [AvaloniaFact]
    public void Release_OpensTheEditorWithThePendingBlockAndFocusesTheTitle()
    {
        var window = Show();
        Drag(window, 12 * 60, 15 * 60 + 15);

        Release(window, At(window, 15 * 60 + 15));

        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("New block", Schedule(window).BlockEditor.Heading);
        Assert.Equal("12:00", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("15:15", Named<TextBox>(window, "BlockEndBox").Text);
        Assert.True(Named<TextBox>(window, "BlockTitleBox").IsFocused);
        Assert.True(Named<Border>(window, "DragPreview").IsVisible);
        AssertSpan(window, 12 * 60, 15 * 60 + 15);
        UiSnapshot.Render(window, "schedule-drag-pending");
        window.Close();
    }

    [AvaloniaFact]
    public void DragUpward_TheEarlierTimeIsTheStart()
    {
        var window = Show();
        Drag(window, 15 * 60 + 15, 12 * 60);

        Release(window, At(window, 12 * 60));

        Assert.Equal("12:00", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("15:15", Named<TextBox>(window, "BlockEndBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void PendingBlock_SaveKeepsIt()
    {
        var window = Show();
        Drag(window, 12 * 60, 13 * 60 + 30);
        Release(window, At(window, 13 * 60 + 30));

        Named<TextBox>(window, "BlockTitleBox").Text = "Deep work";
        Named<Button>(window, "BlockSaveButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Border>(window, "DragPreview").IsVisible);
        Assert.Contains("Deep work", window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("blockTitle") && t.Name is null).Select(t => t.Text));
        window.Close();
    }

    [AvaloniaFact]
    public void PendingBlock_EscapeDiscardsIt()
    {
        var window = Show();
        Drag(window, 12 * 60, 13 * 60 + 30);
        Release(window, At(window, 13 * 60 + 30));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.False(Named<Border>(window, "DragPreview").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeDuringTheDrag_CancelsWithNothingCreated()
    {
        var window = Show();
        Drag(window, 12 * 60, 13 * 60 + 30);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Move(window, At(window, 16 * 60));
        Release(window, At(window, 16 * 60));

        Assert.False(Named<Border>(window, "DragPreview").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void PressAndReleaseWithoutMoving_IsStillAClick()
    {
        var window = Show();
        var point = At(window, 12 * 60 + 22);
        Press(window, point);
        Move(window, point + new Point(0, 2));

        Release(window, point + new Point(0, 2));

        Assert.False(Named<Border>(window, "DragPreview").IsVisible);
        Assert.Equal("12:15", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("13:15", Named<TextBox>(window, "BlockEndBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void DragFromABlock_CreatesNothingAndEditsTheBlock()
    {
        var window = Show();

        Drag(window, 14 * 60 + 30, 16 * 60 + 30); // starts on Workout, 14:00-15:00
        Release(window, At(window, 16 * 60 + 30));

        Assert.False(Named<Border>(window, "DragPreview").IsVisible);
        Assert.False(Schedule(window).IsPending);
        window.Close();
    }

    [AvaloniaFact]
    public void DragOutsideTheWindow_KeepsTracking()
    {
        var window = Show();
        var below = At(window, 21 * 60);
        Assert.True(below.Y > window.Bounds.Height);

        Drag(window, 12 * 60, 21 * 60);

        AssertSpan(window, 12 * 60, 21 * 60);
        Release(window, below);
        Assert.Equal("21:00", Named<TextBox>(window, "BlockEndBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void DragNearTheBottomEdge_AutoScrollsAndExtendsTheDrag()
    {
        var window = Show();
        var scroller = Scroller(window);
        var bottom = scroller.TranslatePoint(new Point(scroller.Bounds.Width / 2, scroller.Bounds.Height - 4), window)!.Value;
        Press(window, At(window, 12 * 60));
        Move(window, bottom);
        var offsetBefore = scroller.Offset.Y;
        var endBefore = PreviewSpan(window).End;

        for (var i = 0; i < 10; i++)
        {
            window.AutoScrollTick();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(scroller.Offset.Y > offsetBefore);
        Assert.True(PreviewSpan(window).End > endBefore);
        Release(window, bottom);
        window.Close();
    }
}
