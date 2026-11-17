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

// A plain click on empty grid space opens the marker editor and shows a pending marker line.
public class ScheduleClickMarkerHeadlessTests
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

    // A point in the window at the given minute of the day, at a fraction of the way across the block column.
    private static Point At(Window window, double minute, double across = 0.5)
    {
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        return blocks.TranslatePoint(new Point(blocks.Bounds.Width * across, minute), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // Where the pending marker line sits, in minutes from midnight (the middle of its row).
    private static double PendingMinute(Window window)
    {
        var row = Named<Control>(window, "PendingMarker");
        var slot = row.GetVisualAncestors().OfType<Visual>().Prepend(row)
            .First(v => v.GetVisualParent() is Chisel.App.Controls.DayGridPanel).Bounds;
        return slot.Top + slot.Height / 2;
    }

    [AvaloniaFact]
    public void PendingMarker_IsHiddenAtFirst()
    {
        var window = Show();

        Assert.False(Named<Control>(window, "PendingMarker").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Click_OpensTheMarkerEditorAtTheSnappedTimeWithAPendingLine()
    {
        var window = Show();

        Click(window, At(window, 15 * 60 + 52));

        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("New marker", Schedule(window).MarkerEditor.Heading);
        Assert.Equal("15:45", Named<TextBox>(window, "MarkerTimeBox").Text);
        Assert.True(Named<TextBox>(window, "MarkerLabelBox").IsFocused);
        Assert.True(Named<Control>(window, "PendingMarker").IsVisible);
        Assert.Equal(15 * 60 + 45, PendingMinute(window), tolerance: 1.0);
        Assert.Equal("15:45  New marker", Named<TextBlock>(window, "PendingMarkerText").Text);
        UiSnapshot.Render(window, "schedule-click-marker");
        window.Close();
    }

    [AvaloniaFact]
    public void PendingLine_FollowsTheLabelTyped()
    {
        var window = Show();
        Click(window, At(window, 15 * 60 + 52));

        Named<TextBox>(window, "MarkerLabelBox").Text = "Stretch";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("15:45  Stretch", Named<TextBlock>(window, "PendingMarkerText").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_RemovesThePendingLine()
    {
        var window = Show();
        Click(window, At(window, 15 * 60 + 52));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.False(Named<Control>(window, "PendingMarker").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Save_ReplacesThePendingLineWithARealMarker()
    {
        var window = Show();
        Click(window, At(window, 15 * 60 + 52));
        Named<TextBox>(window, "MarkerLabelBox").Text = "Stretch";

        Named<Button>(window, "MarkerSaveButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Control>(window, "PendingMarker").IsVisible);
        Assert.Contains("15:45  Stretch", window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("markerText") && t.Name is null).Select(t => t.Text));
        window.Close();
    }

    [AvaloniaFact]
    public void ClickOnAnExistingMarker_StillOpensItForEditing()
    {
        var window = Show();
        var pill = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("markerPill") && b.GetVisualAncestors().OfType<Control>().Any(a => a.Name == "ScheduleMarkers"));
        var center = pill.TranslatePoint(new Point(pill.Bounds.Width / 2, pill.Bounds.Height / 2), window)!.Value;

        Click(window, center);

        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.Equal("Edit marker", Schedule(window).MarkerEditor.Heading);
        Assert.False(Named<Control>(window, "PendingMarker").IsVisible);
        window.Close();
    }
}
