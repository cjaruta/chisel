using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

public class ClockToolsHeadlessTests
{
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
        Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shown(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().Where(c => c.Name == name).Any(c => c.IsEffectivelyVisible);

    [AvaloniaFact]
    public void Collapsed_ShowsOnlyTheClockAndAToolsButton()
    {
        var (window, _) = WithClock();
        Assert.True(Shown(window, "ClockFace"));
        Assert.True(Shown(window, "ClockOpenButton"));
        Assert.False(Shown(window, "ClockStrip"));
        Assert.False(Shown(window, "ClockChip"));
    }

    [AvaloniaFact]
    public void Opening_ShowsTheModeStrip_AndEachModeShowsItsTool()
    {
        var (window, main) = WithClock();
        Click(window, "ClockOpenButton");
        Assert.True(Shown(window, "ClockStrip"));
        Assert.True(main.ClockTools.IsExpanded);
        Assert.True(Shown(window, "ClockFace")); // Clock mode

        Click(window, "ClockTab_Timer");
        Assert.True(Shown(window, "ClockTimerPanel"));
        Assert.False(Shown(window, "ClockFace"));

        Click(window, "ClockTab_Stopwatch");
        Assert.True(Shown(window, "ClockStopwatchPanel"));
        Assert.False(Shown(window, "ClockTimerPanel"));

        Click(window, "ClockCloseButton");
        Assert.False(Shown(window, "ClockStrip"));
        UiSnapshot.Render(window, "clock-tools-closed");
    }

    [AvaloniaFact]
    public void TheTimerPanel_SetsAndStartsATimer()
    {
        var (window, main) = WithClock();
        Click(window, "ClockOpenButton");
        Click(window, "ClockTab_Timer");
        Click(window, "ClockTimerAdd5m");
        Assert.Equal("05:00", Named<TextBlock>(window, "ClockTimerText").Text);

        Click(window, "ClockTimerStart");
        Assert.Equal(TimerStatus.Running, main.ClockTools.TimerStatus);
        Assert.Equal("Pause", Named<Button>(window, "ClockTimerStart").Content);
        UiSnapshot.Render(window, "clock-tools-timer");
    }

    [AvaloniaFact]
    public void TheStopwatchPanel_StartsAndLaps()
    {
        var (window, main) = WithClock();
        Click(window, "ClockOpenButton");
        Click(window, "ClockTab_Stopwatch");
        Click(window, "ClockStopwatchStart");
        Click(window, "ClockStopwatchLap");
        Assert.True(main.ClockTools.StopwatchRunning);
        Assert.Single(main.ClockTools.LapTexts);
        UiSnapshot.Render(window, "clock-tools-stopwatch");
    }

    [AvaloniaFact]
    public void ARunningStopwatch_ShowsAChip_WhileCollapsed()
    {
        var (window, main) = WithClock();
        main.ClockTools.StopwatchStartPauseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shown(window, "ClockChip"));
        UiSnapshot.Render(window, "clock-tools-chip");

        Click(window, "ClockOpenButton");
        Assert.False(Shown(window, "ClockChip"));
    }

    [AvaloniaFact]
    public void InEditMode_OnlyTheConfigurationShows_AndItSwitchesToolsOff()
    {
        var (window, main) = WithClock();
        Click(window, "ClockOpenButton");
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shown(window, "ClockConfig"));
        Assert.False(Shown(window, "ClockStrip"));
        UiSnapshot.Render(window, "clock-tools-edit");

        Named<CheckBox>(window, "ClockToolToggle_Stopwatch").IsChecked = false;
        Click(window, "ClockStripPlacement");
        Assert.DoesNotContain(ClockMode.Stopwatch, main.ClockTools.EnabledModes);
        Assert.True(main.ClockTools.StripAtBottom);

        main.Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Shown(window, "ClockConfig"));
        Assert.False(Shown(window, "ClockTab_Stopwatch"));
    }
}
