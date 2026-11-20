using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class ClockToolsHeadlessTests : IDisposable
{
    public ClockToolsHeadlessTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static (MainWindow Window, MainViewModel Main) WithClock(FakeTimeProvider? clock = null)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: clock);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static T Named<T>(MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static bool Shown(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().Where(c => c.Name == name).Any(c => c.IsEffectivelyVisible);

    private static List<ClockSlotView> SlotViews(MainWindow w) => w.GetVisualDescendants().OfType<ClockSlotView>().ToList();

    private static Point CenterOf(MainWindow w, Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;

    private static void Press(MainWindow w, string name)
    {
        var button = Named<Button>(w, name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void RightClick(MainWindow w, Control over)
    {
        var point = CenterOf(w, over);
        w.MouseMove(point);
        w.MouseDown(point, MouseButton.Right);
        w.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Wheel(MainWindow w, Control over, double notches)
    {
        w.MouseMove(CenterOf(w, over));
        w.MouseWheel(CenterOf(w, over), new Vector(0, notches));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ByDefault_TheClockIsPinnedAndOneSlotShowsTheTimer_WithNoToolsButtonOrStrip()
    {
        var (window, main) = WithClock();

        var views = SlotViews(window);
        Assert.Equal(2, views.Count);
        Assert.True(Shown(window, "ClockFace"));
        Assert.True(Shown(window, "ClockTimerPanel"));
        Assert.False(Shown(window, "ClockStopwatchPanel"));
        var widget = window.GetVisualDescendants().OfType<ClockWidgetView>().Single();
        Assert.DoesNotContain(widget.GetVisualDescendants().OfType<Button>(), b => b.Content is "Tools" or "Close");
        Assert.True(views[0].Bounds.Width <= views[0].Bounds.Height + 1); // the clock takes a square, no unused width
        Assert.True(views[1].Bounds.Width > 150);
        Assert.True(main.ClockSlots.Horizontal);
        UiSnapshot.Render(window, "clock-slots-default");
    }

    [AvaloniaTheory]
    [InlineData(640, 300, true)]
    [InlineData(300, 640, false)]
    public void TheSlots_RunAcrossAWideWidget_AndDownATallOne(double width, double height, bool horizontal)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        var view = new ClockWidgetView { DataContext = main };
        var host = new Window { Width = width, Height = height, Content = view };
        host.Show();
        Dispatcher.UIThread.RunJobs();

        var views = view.GetVisualDescendants().OfType<ClockSlotView>().ToList();
        Assert.Equal(2, views.Count);
        if (horizontal)
        {
            Assert.True(views[1].Bounds.X > views[0].Bounds.X);
            Assert.Equal(views[0].Bounds.Y, views[1].Bounds.Y);
        }
        else
        {
            Assert.True(views[1].Bounds.Y > views[0].Bounds.Y);
            Assert.Equal(views[0].Bounds.X, views[1].Bounds.X);
        }
        host.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(main.ClockSlots.AlarmReachable); // released with the widget
    }

    [AvaloniaFact]
    public void TheWheelOverAScrollingSlot_CyclesTheToolsBothWays_AndWraps()
    {
        var (window, main) = WithClock();
        var scrolling = SlotViews(window)[1];
        var slot = main.ClockSlots.Slots[1];

        Wheel(window, scrolling, -1); // down: forward
        Assert.Equal(ClockTool.Stopwatch, slot.Current);
        Assert.True(Shown(window, "ClockStopwatchPanel"));
        Assert.False(Shown(window, "ClockTimerPanel"));

        Wheel(window, scrolling, 1); // up: back
        Wheel(window, scrolling, 1);
        Assert.Equal(ClockTool.Empty, slot.Current); // wrapped past the first
        Wheel(window, scrolling, -1);
        Assert.Equal(ClockTool.Timer, slot.Current);
    }

    [AvaloniaFact]
    public void TheWheel_OnlyCycles_ItNeverChangesAValue()
    {
        var (window, main) = WithClock();
        main.ClockTools.AddTimerSecondsCommand.Execute(300);
        var scrolling = SlotViews(window)[1];
        var text = Named<TextBlock>(window, "ClockTimerText");

        Wheel(window, scrolling, 3);
        Wheel(window, scrolling, -2);

        Assert.Equal("05:00", main.ClockTools.TimerText);
        Assert.Equal("05:00", text.Text);
    }

    [AvaloniaFact]
    public void TheWheelOverAPinnedSlot_DoesNothing()
    {
        var (window, main) = WithClock();
        var pinned = SlotViews(window)[0];

        Wheel(window, pinned, -1);

        Assert.Equal(ClockTool.Clock, main.ClockSlots.Slots[0].Current);
        Assert.Equal(ClockTool.Timer, main.ClockSlots.Slots[1].Current);
    }

    [AvaloniaFact]
    public void InEditMode_TheWheelDoesNotCycle()
    {
        var (window, main) = WithClock();
        var scrolling = SlotViews(window)[1];
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Wheel(window, scrolling, -1);

        Assert.Equal(ClockTool.Timer, main.ClockSlots.Slots[1].Current);
    }

    [AvaloniaFact]
    public void TheDots_ShowOnHover_AndStayForARunningToolThatIsOffScreen()
    {
        var (window, main) = WithClock();
        var scrolling = SlotViews(window)[1];
        var dots = scrolling.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "ClockSlotDots");
        window.MouseMove(new Point(2, 2)); // pointer away
        Dispatcher.UIThread.RunJobs();
        Assert.False(dots.IsVisible);

        window.MouseMove(CenterOf(window, scrolling));
        Dispatcher.UIThread.RunJobs();
        Assert.True(dots.IsVisible);
        Assert.Equal(4, dots.GetVisualDescendants().OfType<Ellipse>().Count());

        window.MouseMove(new Point(2, 2));
        main.ClockTools.StopwatchStartPauseCommand.Execute(null);
        main.ClockTools.Tick();
        main.ClockSlots.Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.True(dots.IsVisible); // the stopwatch is running off screen
        var running = dots.GetVisualDescendants().OfType<Ellipse>().Single(e => e.Name == "ClockDot_Stopwatch");
        var idle = dots.GetVisualDescendants().OfType<Ellipse>().Single(e => e.Name == "ClockDot_Alarm");
        Assert.NotEqual(idle.Fill, running.Fill);
        Assert.Contains(dots.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "00:00");
        UiSnapshot.Render(window, "clock-slots-dots");
    }

    [AvaloniaFact]
    public void RightClickingAScrollingSlot_OffersEveryToolOfTheSet_AndJumpsToTheChosenOne()
    {
        var (window, main) = WithClock();
        var scrolling = SlotViews(window)[1];

        RightClick(window, scrolling);
        Dispatcher.UIThread.RunJobs();
        var items = scrolling.JumpMenu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(["ClockJump_Timer", "ClockJump_Stopwatch", "ClockJump_Alarm", "ClockJump_Empty"], items.Select(i => i.Name));
        Assert.True(items[0].IsChecked);

        items.Single(i => i.Name == "ClockJump_Alarm").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ClockTool.Alarm, main.ClockSlots.Slots[1].Current);
        Assert.True(Shown(window, "ClockAlarmPanel"));
    }

    [AvaloniaFact]
    public void RightClickingAPinnedSlot_OpensNoMenu()
    {
        var (window, _) = WithClock();
        var pinned = SlotViews(window)[0];
        RightClick(window, pinned);
        Dispatcher.UIThread.RunJobs();
        Assert.False(pinned.JumpMenu.IsOpen);
    }

    [AvaloniaFact]
    public void TheTimerAndStopwatchPanels_WorkInsideTheirSlot()
    {
        var (window, main) = WithClock();
        Press(window, "ClockTimerAdd5m");
        Assert.Equal("05:00", Named<TextBlock>(window, "ClockTimerText").Text);
        Press(window, "ClockTimerStart");
        Assert.Equal(TimerStatus.Running, main.ClockTools.TimerStatus);
        Assert.Equal("Pause", Named<Button>(window, "ClockTimerStart").Content);

        main.ClockSlots.Slots[1].Select(ClockTool.Stopwatch);
        Dispatcher.UIThread.RunJobs();
        Press(window, "ClockStopwatchStart");
        Press(window, "ClockStopwatchLap");
        Assert.True(main.ClockTools.StopwatchRunning);
        Assert.Single(main.ClockTools.LapTexts);
        UiSnapshot.Render(window, "clock-slots-stopwatch");
    }

    [AvaloniaFact]
    public void AFiringAlarm_SwitchesItsSlotToTheAlarm_WithDismissAndSnooze()
    {
        var clock = new FakeTimeProvider(UiSnapshot.SampleNow);
        var (window, main) = WithClock(clock);
        main.Alarms.NewTime = "09:35";
        main.Alarms.NewLabel = "Call back";
        main.Alarms.AddCommand.Execute(null);

        clock.Advance(TimeSpan.FromMinutes(6));
        main.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ClockTool.Alarm, main.ClockSlots.Slots[1].Current);
        Assert.True(Shown(window, "ClockAlarmRinging"));
        Assert.Equal("Call back", Named<TextBlock>(window, "ClockAlarmRingingTitle").Text);
        Assert.Null(main.Alerts.Current);
        UiSnapshot.Render(window, "clock-slots-ringing");

        Press(window, "ClockAlarmSnooze");
        Assert.False(Shown(window, "ClockAlarmRinging"));
        clock.Advance(TimeSpan.FromMinutes(5));
        main.Tick();
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shown(window, "ClockAlarmRinging"));

        Press(window, "ClockAlarmDismiss");
        Assert.False(Shown(window, "ClockAlarmRinging"));
    }

    [AvaloniaFact]
    public void InEditMode_TheWidgetBodyHasNoToolControls_AndAClickInsideASlotIsNotADrag()
    {
        var (window, main) = WithClock();
        var scrolling = SlotViews(window)[1];
        var before = main.Layout.Layout;

        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is not null && c.Name.StartsWith("ClockToolToggle_"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "ClockConfig" or "ClockStripPlacement" or "ClockOpenButton");

        var point = CenterOf(window, scrolling);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(main.Layout.IsDraggingWidget);
        Assert.Equal(before, main.Layout.Layout);
        UiSnapshot.Render(window, "clock-slots-edit");
    }

    [AvaloniaFact]
    public void AClockWidgetThatIsNotOnTheDashboard_LeavesTheAlarmToTheBanner()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        Assert.False(main.ClockSlots.AlarmReachable);
        Assert.Empty(main.ClockSlots.Slots);
    }
}
