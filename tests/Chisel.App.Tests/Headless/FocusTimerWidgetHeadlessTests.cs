using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Focus;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class FocusTimerWidgetHeadlessTests
{
    private readonly FakeTimeProvider _clock = new(UiSnapshot.SampleNow);

    private MainWindow Open(double weight = 1.5, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock, themeMode: themeMode);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.FocusTimer, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static FocusTimerWidgetView View(Window w) => w.GetVisualDescendants().OfType<FocusTimerWidgetView>().Single();

    private static T Named<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static string Spoken(Window w) => AutomationProperties.GetName(Named<FocusDial>(w, "FocusDial")) ?? "";

    private void Advance(TimeSpan span)
    {
        _clock.Advance(span);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Control button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ALargeWidget_ShowsTheRingTheButtonsAndTheCount_AndCountsDown()
    {
        var window = Open(weight: 3);
        Assert.Equal(FocusDetail.Full, View(window).Layout.Detail);
        Assert.Equal("Focus, 25 minutes remaining", Spoken(window));

        Click(Named<Button>(window, "FocusPrimary"));
        Advance(TimeSpan.FromSeconds(6 * 60 + 40));

        Assert.Equal("Focus, 18 minutes 20 seconds remaining", Spoken(window));
        Assert.Equal("Pause", Named<Button>(window, "FocusPrimary").Content);
        Assert.Equal("0 sessions today", Named<TextBlock>(window, "FocusSessions").Text);
        Assert.True(Named<Control>(window, "FocusSkip").IsVisible && Named<Control>(window, "FocusReset").IsVisible);
        UiSnapshot.Render(window, "focus-timer");
        window.Close();
    }

    [AvaloniaFact]
    public void APhaseEnd_FlashesTheCard_InTheLightThemeToo()
    {
        var window = Open(weight: 3, themeMode: "light");
        var focus = Main(window).FocusTimer;
        focus.Start();
        Advance(TimeSpan.FromMinutes(25));

        Assert.Equal("Short Break, 5 minutes remaining", Spoken(window));
        Assert.True(focus.IsFlashing);
        Assert.NotEqual(Avalonia.Media.Brushes.Transparent, Named<Border>(window, "FocusFlash").Background);
        Assert.Equal("1 session today", Named<TextBlock>(window, "FocusSessions").Text);
        UiSnapshot.Render(window, "focus-timer-light");

        Advance(FocusTimerViewModel.FlashLength);
        Assert.Equal(Avalonia.Media.Brushes.Transparent, Named<Border>(window, "FocusFlash").Background);
        window.Close();
    }

    [AvaloniaFact]
    public void ASmallWidget_ShowsOnlyTheRingAndTheTime_AndTheDialStartsAndPauses()
    {
        var window = Open(weight: 0.25);
        Assert.Equal(FocusDetail.Minimal, View(window).Layout.Detail);
        Assert.False(Named<Control>(window, "FocusControls").IsVisible);

        var dial = Named<FocusDial>(window, "FocusDial");
        dial.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(FocusRun.Running, Main(window).FocusTimer.Run);
        UiSnapshot.Render(window, "focus-timer-small");
        window.Close();
    }

    [AvaloniaFact]
    public void TheTaskInFocus_IsShownByName()
    {
        var window = Open(weight: 3);
        var main = Main(window);
        var task = main.Tasks.Open.First();

        main.FocusTimer.ChooseTask(task.Id);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Task in focus: " + task.Text + ". Choose another", AutomationProperties.GetName(Named<Button>(window, "FocusTask")));
        window.Close();
    }

    [AvaloniaFact]
    public void TheTimer_UpdatesOnlyWhileTheWidgetIsOnScreen()
    {
        var window = Open();
        var focus = Main(window).FocusTimer;
        Assert.True(focus.IsWatched);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(focus.IsWatched);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(focus.IsWatched);

        Main(window).Layout.Apply(Main(window).Layout.Layout.RemoveWidget(WidgetRegistry.FocusTimer));
        Dispatcher.UIThread.RunJobs();
        Assert.False(focus.IsWatched);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingsPage_AppliesAPreset_AndChangesTheFace()
    {
        var window = Open(weight: 3);
        var main = Main(window);
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "focus");
        Dispatcher.UIThread.RunJobs();

        Click(window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "FocusPreset" && Equals(b.Content, "50 / 10")));
        Assert.Equal(50m, Named<NumericUpDown>(window, "FocusMinutes").Value);
        Named<CheckBox>(window, "FocusDigital").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((50, FocusFace.Digital), (main.FocusTimer.FocusMinutes, main.FocusTimer.Settings.Face));
        UiSnapshot.Render(window, "focus-timer-settings");
        window.Close();
    }

    [AvaloniaFact]
    public void AFrameOfTheDial_AllocatesNothingWorthMentioning()
    {
        var window = Open(weight: 3);
        Main(window).FocusTimer.Start();
        Advance(TimeSpan.FromSeconds(83.4));
        var dial = Named<FocusDial>(window, "FocusDial");
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 400));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 200; i++)
        {
            dial.Draw(context);
        }

        var perFrame = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 500; i++)
            {
                dial.Draw(context);
            }
            perFrame = Math.Min(perFrame, (GC.GetAllocatedBytesForCurrentThread() - before) / 500);
        }
        Assert.True(perFrame <= 64, $"{perFrame} B per frame");
        window.Close();
    }
}
