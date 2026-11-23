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

// The "Customize layout" button puts the window in layout edit mode: every widget gets an editing frame
// with a title bar, the widgets' contents stop responding, and a toolbar offers Done, Cancel and Reset.
public class LayoutEditModeHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<WidgetFrame> Frames(Window window) =>
        window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).ToList();

    private static MainViewModel ViewModel(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickControl(Window window, Control control) =>
        Click(window, InWindow(control, window).Center);

    private static MainWindow Show(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 600);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void EnterEditMode(Window window) => ClickControl(window, Named<Button>(window, "CustomizeLayoutButton"));

    // A point on empty schedule space at a minute of the day.
    private static Point ScheduleAt(Window window, double minute)
    {
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        return blocks.TranslatePoint(new Point(blocks.Bounds.Width / 2, minute), window)!.Value;
    }

    [AvaloniaFact]
    public void Headers_KeepTheTitleClearOfTheRemoveBadge()
    {
        var window = Show();
        EnterEditMode(window);
        foreach (var frame in Frames(window))
        {
            var heading = frame.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("widgetHeading"));
            if (heading is null)
            {
                continue;
            }
            var badge = InWindow(frame.HeaderButton, window);
            var title = InWindow(heading, window);
            Assert.True(title.Left >= badge.Right, $"{frame.WidgetId}: title starts at {title.Left}, badge ends at {badge.Right}");
        }
        UiSnapshot.Render(window, "headers-edit-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void CustomizeButton_SitsNextToTheGearWithItsTooltip()
    {
        var window = Show();
        var customize = Named<Button>(window, "CustomizeLayoutButton");
        var gear = Named<Button>(window, "SettingsButton");

        Assert.Equal("Customize layout", ToolTip.GetTip(customize));
        var c = InWindow(customize, window);
        var g = InWindow(gear, window);
        Assert.True(c.Right <= g.Left && g.Left - c.Right < 12, "the button is just left of the gear");
        Assert.True(Math.Abs(c.Center.Y - g.Center.Y) < 1);
        Assert.False(Named<Border>(window, "LayoutToolbar").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingCustomize_FramesEveryWidgetWithATitleBarAndShowsTheToolbar()
    {
        var window = Show();

        EnterEditMode(window);

        Assert.True(ViewModel(window).Layout.IsEditing);
        Assert.Contains("active", Named<Button>(window, "CustomizeLayoutButton").Classes);
        Assert.Contains("editing", Named<Panel>(window, "Page").Classes);
        Assert.True(Named<Border>(window, "LayoutToolbar").IsVisible);
        Assert.False(Named<Button>(window, "SettingsButton").IsEffectivelyEnabled);

        var frames = Frames(window);
        Assert.Equal(4, frames.Count);
        foreach (var frame in frames)
        {
            Assert.Contains("editing", frame.Classes);
            Assert.True(frame.EditBar.IsEffectivelyVisible, frame.WidgetId);
            Assert.Empty(frame.EditBar.GetVisualDescendants().OfType<TextBlock>());
            Assert.Equal(1, frame.BorderThickness.Left);
            Assert.NotEqual(Colors.Transparent, ((ISolidColorBrush)frame.BorderBrush!).Color);
            Assert.True(frame.RemoveButton.IsEffectivelyVisible, frame.WidgetId);
            Assert.False(frame.Content.IsHitTestVisible, $"{frame.WidgetId} content should ignore the mouse");
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(frame.Content));
            Assert.True(frame.Content.Opacity < 1, $"{frame.WidgetId} content should be dimmed");
        }
        UiSnapshot.Render(window, "edit-mode-1200x760");
        window.Close();
    }

    [AvaloniaFact]
    public void Entering_TakesKeyboardFocusOutOfAWidget()
    {
        var window = Show();
        var newTask = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "Add a task…");
        newTask.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(newTask.IsFocused);

        EnterEditMode(window);
        window.KeyTextInput("abc");
        Dispatcher.UIThread.RunJobs();

        Assert.False(newTask.IsKeyboardFocusWithin);
        Assert.True(string.IsNullOrEmpty(newTask.Text));
        window.Close();
    }

    [AvaloniaFact]
    public void TheDragSurfaceCoversTheWholeWidget()
    {
        var window = Show();
        EnterEditMode(window);

        foreach (var frame in Frames(window))
        {
            var bar = InWindow(frame.EditBar, window);
            var content = InWindow(frame.Content, window);
            Assert.True(bar.Contains(content.TopLeft) && bar.Contains(content.BottomRight), $"{frame.WidgetId} content shows outside the drag surface");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void Toolbar_HasLibraryDoneCancelAndReset()
    {
        var window = Show();
        EnterEditMode(window);

        var toolbar = Named<Border>(window, "LayoutToolbar");
        var buttons = toolbar.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Content as string).Where(c => c is not null).ToList();

        Assert.Equal(new[] { "Widget Library", "Reset layout", "Cancel", "Done" }, buttons);
        var bounds = InWindow(toolbar, window);
        Assert.True(Math.Abs(bounds.Center.X - window.ClientSize.Width / 2) < 2, "toolbar is centered");
        Assert.True(bounds.Top < 40, "toolbar is at the top");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1000, 650)]
    [InlineData(1200, 760)]
    public void Toolbar_NeverCoversTheClockOrDate(int width, int height)
    {
        var window = Show(width, height);
        EnterEditMode(window);

        var toolbar = InWindow(Named<Border>(window, "LayoutToolbar"), window);
        var clock = InWindow(Named<StackPanel>(window, "ClockPanel"), window);

        Assert.True(toolbar.Left >= clock.Right, $"toolbar starts at {toolbar.Left}, the clock and date end at {clock.Right}");
        window.Close();
    }

    [AvaloniaFact]
    public void InEditMode_TheScheduleIgnoresHoverAndClicks()
    {
        var window = Show();
        EnterEditMode(window);

        window.MouseMove(ScheduleAt(window, 12 * 60 + 37));
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<Control>(window, "HoverLine").IsVisible);

        Click(window, ScheduleAt(window, 12 * 60 + 22));
        Assert.False(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);

        window.MouseDown(ScheduleAt(window, 16 * 60), MouseButton.Left);
        window.MouseMove(ScheduleAt(window, 17 * 60), RawInputModifiers.LeftMouseButton);
        window.MouseUp(ScheduleAt(window, 17 * 60), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(ViewModel(window).Schedule.IsPreviewVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void InEditMode_ListButtonsAndHeaderButtonsDoNothing()
    {
        var window = Show();
        var doneBefore = ViewModel(window).Tasks.Done.Count;
        Assert.True(doneBefore > 0);
        EnterEditMode(window);

        var add = Named<Button>(window, "AddBlockButton");
        ClickControl(window, add);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);

        var clear = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearDoneButton");
        ClickControl(window, clear);
        Assert.Equal(doneBefore, ViewModel(window).Tasks.Done.Count);
        window.Close();
    }

    [AvaloniaFact]
    public void Done_ExitsAndTheWidgetsWorkAgain()
    {
        var window = Show();
        EnterEditMode(window);

        ClickControl(window, Named<Button>(window, "LayoutDoneButton"));

        Assert.False(ViewModel(window).Layout.IsEditing);
        Assert.False(Named<Border>(window, "LayoutToolbar").IsVisible);
        Assert.True(Named<Button>(window, "SettingsButton").IsEffectivelyEnabled);
        Assert.All(Frames(window), f => Assert.False(f.EditBar.IsEffectivelyVisible));

        window.MouseMove(ScheduleAt(window, 12 * 60 + 37));
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<Control>(window, "HoverLine").IsVisible);
        Click(window, ScheduleAt(window, 12 * 60 + 22));
        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void CancelButtonAndEscape_ExitEditMode()
    {
        var window = Show();
        EnterEditMode(window);
        ClickControl(window, Named<Button>(window, "LayoutCancelButton"));
        Assert.False(ViewModel(window).Layout.IsEditing);

        EnterEditMode(window);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(ViewModel(window).Layout.IsEditing);
        window.Close();
    }

    [AvaloniaFact]
    public void Entering_ClosesTheSettingsPanelAndAnOpenEditor()
    {
        var window = Show();
        ViewModel(window).Settings.IsOpen = true;
        ViewModel(window).Schedule.AddBlockCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // The settings dialog is modal and covers the toolbar buttons, so use the command directly.
        ViewModel(window).CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Border>(window, "SettingsPanel").IsVisible);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1904, 984)]
    public void EnteringAndLeaving_DoesNotMoveOrResizeAnyWidget(int width, int height)
    {
        var window = Show(width, height);
        Rect[] Snapshot() => Frames(window).SelectMany(f => new[] { InWindow(f, window), f.View.Bounds }).ToArray();
        var before = Snapshot();

        EnterEditMode(window);
        var editing = Snapshot();
        if (width == 1904)
        {
            UiSnapshot.Render(window, "edit-mode-1904x984");
        }
        ClickControl(window, Named<Button>(window, "LayoutDoneButton"));
        var after = Snapshot();

        Assert.Equal(before, editing);
        Assert.Equal(before, after);
        window.Close();
    }

    [AvaloniaFact]
    public void InEditMode_TheDashboardStaysLive()
    {
        var window = Show();
        EnterEditMode(window);
        var schedule = ViewModel(window).Schedule;
        var nowBefore = schedule.NowMinute;

        ViewModel(window).Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(nowBefore, schedule.NowMinute);
        Assert.True(Named<Control>(window, "NowLine").IsVisible);
        window.Close();
    }
}
