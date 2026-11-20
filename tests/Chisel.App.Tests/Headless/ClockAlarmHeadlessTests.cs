using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

public class ClockAlarmHeadlessTests : IDisposable
{
    public ClockAlarmHeadlessTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static (MainWindow Window, MainViewModel Main) WithClock()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static T Named<T>(MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(MainWindow w, string name)
    {
        // A real click runs the button's Command (raising Click by hand does not), so do the same.
        var button = Named<Button>(w, name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shown(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().Where(c => c.Name == name).Any(c => c.IsEffectivelyVisible);

    [AvaloniaFact]
    public void TheAlarmSlot_ShowsTheNextAlarmLarge_AndManageOpensTheFullEditor()
    {
        var (window, main) = WithClock();
        main.ClockSlots.Slots[1].Select(ClockTool.Alarm);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shown(window, "ClockAlarmPanel"));
        Assert.True(Shown(window, "ClockFace")); // the clock stays pinned beside it
        Assert.Equal("--:--", Named<TextBlock>(window, "ClockAlarmNextTime").Text);
        Assert.False(Shown(window, "ClockAlarmToggle")); // nothing to switch yet

        // The full form does not sit in the slot: it opens from the one Manage button.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "ClockAlarmTime" or "ClockAlarmLabel" or "ClockAlarmAdd");
        main.Alarms.ManageCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var editor = (Control)((Avalonia.Controls.Flyout)Named<Button>(window, "ClockAlarmManage").Tag!).Content!;
        Assert.True(main.Alarms.IsManaging);
        T Find<T>(string name) where T : Control => editor.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

        Find<TextBox>("ClockAlarmTime").Text = "08:15";
        Find<TextBox>("ClockAlarmLabel").Text = "Stand up";
        Find<ToggleButton>("ClockAlarmDay_Monday").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        var add = Find<Button>("ClockAlarmAdd");
        add.Command!.Execute(add.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        var item = Assert.Single(main.Alarms.Items);
        Assert.Equal("08:15", item.TimeText);
        Assert.Equal("Mon", item.DaysText);
        Assert.Equal("08:15", Named<TextBlock>(window, "ClockAlarmNextTime").Text);
        Assert.Equal("Stand up · Mon", Named<TextBlock>(window, "ClockAlarmNextCaption").Text);
        Assert.True(Shown(window, "ClockAlarmToggle"));
        UiSnapshot.Render(window, "clock-tools-alarm");
    }


    [AvaloniaFact]
    public void AnAlert_ShowsABanner_UntilDismissed()
    {
        var (window, main) = WithClock();
        Assert.False(Shown(window, "AlertBanner"));

        main.Alerts.Show("Wake up", "07:00");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shown(window, "AlertBanner"));
        Assert.Equal("Wake up", Named<TextBlock>(window, "AlertTitle").Text);
        UiSnapshot.Render(window, "alert-banner");

        Click(window, "AlertDismiss");
        Assert.False(Shown(window, "AlertBanner"));
    }

    public void ATimerReachingZero_RaisesAnAlert_AndAnAlarmDoes()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(UiSnapshot.SampleNow);
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: clock);
        var main = (MainViewModel)window.DataContext!;

        main.ClockTools.AddTimerSecondsCommand.Execute(60);
        main.ClockTools.TimerStartPauseCommand.Execute(null);
        main.Alarms.NewTime = "09:35";
        main.Alarms.NewLabel = "Call back";
        main.Alarms.AddCommand.Execute(null);

        clock.Advance(TimeSpan.FromMinutes(1));
        main.Tick();
        Assert.Equal("Timer", main.Alerts.Current?.Title);

        clock.Advance(TimeSpan.FromMinutes(4));
        main.Tick();
        main.Alerts.DismissCommand.Execute(null);
        Assert.Equal("Call back", main.Alerts.Current?.Title);
    }
}
