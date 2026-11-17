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

// Moving the real pointer over the day grid shows a guide line at the snapped time;
// clicking empty space there opens the block editor.
public class ScheduleHoverHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static ScheduleViewModel Schedule(MainWindow window) => ((MainViewModel)window.DataContext!).Schedule;

    // Shows the window scrolled so 10:00 is at the top, keeping midday on screen.
    private static MainWindow Show()
    {
        var window = UiSnapshot.ShowMainWindow();
        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 600);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    // A point in the window at the given minute of the day, halfway across the block column.
    private static Point At(Window window, double minute)
    {
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        return blocks.TranslatePoint(new Point(blocks.Bounds.Width / 2, minute), window)!.Value;
    }

    private static void Move(Window window, Point point)
    {
        window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // Where the guide line is drawn, in minutes from midnight (the line's middle).
    private static double LineMinute(Window window)
    {
        var line = Named<Control>(window, "HoverLine");
        return line.Bounds.Top + line.Bounds.Height / 2;
    }

    [AvaloniaFact]
    public void GuideLine_IsHiddenUntilThePointerIsOverTheGrid()
    {
        var window = Show();

        Assert.False(Named<Control>(window, "HoverLine").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void GuideLine_OverEmptySpace_AppearsAtTheSnappedTime()
    {
        var window = Show();

        Move(window, At(window, 12 * 60 + 37));

        Assert.True(Named<Control>(window, "HoverLine").IsVisible);
        Assert.Equal(12 * 60 + 30, LineMinute(window), tolerance: 1.0);
        Assert.Equal("12:30", Named<TextBlock>(window, "HoverLabel").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void GuideLine_FollowsThePointer()
    {
        var window = Show();
        Move(window, At(window, 12 * 60 + 37));

        Move(window, At(window, 16 * 60 + 8));

        Assert.Equal(16 * 60 + 15, LineMinute(window), tolerance: 1.0);
        Assert.Equal("16:15", Named<TextBlock>(window, "HoverLabel").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void GuideLine_OverABlock_IsHidden()
    {
        var window = Show();
        Move(window, At(window, 12 * 60 + 30));

        Move(window, At(window, 14 * 60 + 30)); // Workout, 14:00-15:00

        Assert.False(Named<Control>(window, "HoverLine").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void GuideLine_WhenThePointerLeavesTheGrid_IsHidden()
    {
        var window = Show();
        Move(window, At(window, 12 * 60 + 30));

        var side = Named<StackPanel>(window, "SidePanel");
        Move(window, side.TranslatePoint(new Point(20, 20), window)!.Value);

        Assert.False(Named<Control>(window, "HoverLine").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void GuideLine_WhenTheGridScrollsUnderAStillPointer_MovesToTheNewTime()
    {
        var window = Show();
        Move(window, At(window, 12 * 60 + 30));

        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 660);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Control>(window, "HoverLine").IsVisible);
        Assert.Equal(13 * 60 + 30, LineMinute(window), tolerance: 1.0);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickOnEmptySpace_OpensTheMarkerEditorAtTheLine()
    {
        var window = Show();

        Click(window, At(window, 12 * 60 + 22));

        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("New marker", Schedule(window).MarkerEditor.Heading);
        Assert.Equal("12:15", Named<TextBox>(window, "MarkerTimeBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickOnABlock_StillOpensItForEditing()
    {
        var window = Show();

        Click(window, At(window, 14 * 60 + 30));

        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("Edit block", Schedule(window).BlockEditor.Heading);
        Assert.Equal("14:00", Named<TextBox>(window, "BlockStartBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Snapshot_GuideLineVisible()
    {
        var window = Show();
        Move(window, At(window, 12 * 60 + 37));

        UiSnapshot.Render(window, "schedule-hover-line");
        window.Close();
    }
}
