using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

// The analog stopwatch has to work where people really put the clock: a normal card in the right-hand column among other widgets,
// where the room is modest. The dial must be there, its hands must move, the readout beneath must follow in 0.1 s steps, the
// buttons must drive it exactly like the digital face, and switching between the two must not disturb the time or leak anything.
public class AnalogStopwatchWorksTests : IDisposable
{
    public AnalogStopwatchWorksTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    // The clock added to the right-hand column of the usual three-card layout, showing its stopwatch.
    private static (MainWindow Window, MainViewModel Main, FakeTimeProvider Clock, WidgetFrame Frame) Show(double width, double height, StopwatchDisplay display = StopwatchDisplay.Analog)
    {
        var clock = new FakeTimeProvider(UiSnapshot.SampleNow);
        var window = UiSnapshot.ShowMainWindow(width, height, clock: clock);
        var main = (MainViewModel)window.DataContext!;
        main.ClockSettings.StopwatchIndex = (int)display;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 1, layout.Columns[1].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        main.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch);
        Dispatcher.UIThread.RunJobs();
        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock);
        return (window, main, clock, frame);
    }

    private static StopwatchFace Face(MainWindow window) => window.GetVisualDescendants().OfType<StopwatchFace>().Single();

    private static T Named<T>(MainWindow w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

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

    private static byte[] Picture(Control control)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height));
        bitmap.Render(control);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private static int Handlers(object source) =>
        (typeof(ObservableObject).GetField(nameof(INotifyPropertyChanged.PropertyChanged), BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1000, 700)]
    [InlineData(1904, 984)]
    [InlineData(960, 700)]
    public void TheDial_IsShownInANormalCard_WithTheReadoutAndButtonsBelowItAndNothingClipped(double width, double height)
    {
        var (window, _, _, frame) = Show(width, height);
        var face = Face(window);

        Assert.True(face.IsVisible && face.IsEffectivelyVisible, $"the dial is hidden in a {frame.Bounds.Width:0}x{frame.Bounds.Height:0} card");
        Assert.True(face.Bounds.Width >= 56 && face.Bounds.Height >= 56, $"the dial is only {face.Bounds.Width:0}x{face.Bounds.Height:0}");
        var slot = face.GetVisualAncestors().OfType<ClockSlotView>().First();
        foreach (var name in new[] { "ClockStopwatchText", "ClockStopwatchStart", "ClockStopwatchLap", "ClockStopwatchReset" })
        {
            var control = Named<Control>(window, name);
            var bottom = control.TranslatePoint(new Point(0, control.Bounds.Height), slot)!.Value.Y;
            Assert.True(bottom <= slot.Bounds.Height + 1, $"{name} is pushed out of the card");
            Assert.True(control.TranslatePoint(new Point(0, 0), face)!.Value.Y >= face.Bounds.Height - 1, $"{name} sits beneath the dial");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void TheSecondsHandSweeps_AndTheMinutesSubDialAdvances()
    {
        var (window, main, clock, _) = Show(1200, 760);
        var face = Face(window);
        Press(window, "ClockStopwatchStart");

        var start = Picture(face);
        clock.Advance(TimeSpan.FromSeconds(12.5));
        var later = Picture(face);
        Assert.False(start.SequenceEqual(later), "the seconds hand did not move");

        window.Close();

        // 5 s and 65 s put the seconds hand in the same place: only the minutes sub-dial (on a large dial) tells them apart.
        var large = new StopwatchFace { Width = 300, Height = 300 };
        large.Measure(new Size(300, 300));
        large.Arrange(new Rect(0, 0, 300, 300));
        large.ElapsedSource = () => 5;
        var five = Picture(large);
        large.ElapsedSource = () => 65;
        var sixtyFive = Picture(large);
        Assert.False(five.SequenceEqual(sixtyFive), "the minutes sub-dial did not advance");
    }

    [AvaloniaFact]
    public void TheReadoutBeneath_UpdatesInStepsOfOneTenth_WhileTheHandsSweep()
    {
        var (window, main, clock, _) = Show(1200, 760);
        var readout = Named<TextBlock>(window, "ClockStopwatchText");
        Press(window, "ClockStopwatchStart");

        var seen = new List<string?>();
        for (var i = 0; i < 12; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            main.ClockTools.Tick();
            Dispatcher.UIThread.RunJobs();
            seen.Add(readout.Text);
        }

        Assert.Equal("00:01.2", seen[^1]);
        Assert.Equal(12, seen.Distinct().Count()); // 0.1, 0.2 ... 1.2: a new tenth for every tick
        Assert.True(Face(window).IsAnimating);
        window.Close();
    }

    [AvaloniaFact]
    public void StartPauseLapAndReset_DriveTheAnalogFaceExactlyLikeTheDigitalOne()
    {
        (string Text, string Button, int Laps, string Stopped, string AfterReset, bool Running) Run(StopwatchDisplay display)
        {
            var (window, main, clock, _) = Show(1200, 760, display);
            Press(window, "ClockStopwatchStart");
            clock.Advance(TimeSpan.FromSeconds(75.5));
            main.ClockTools.Tick();
            Press(window, "ClockStopwatchLap");
            clock.Advance(TimeSpan.FromSeconds(10.3));
            Press(window, "ClockStopwatchStart"); // pause
            var running = main.ClockTools.StopwatchRunning;
            var result = (main.ClockTools.StopwatchText, main.ClockTools.StopwatchButtonText, main.ClockTools.LapTexts.Count, string.Join("|", main.ClockTools.LapTexts), "", running);
            Press(window, "ClockStopwatchReset");
            result.Item5 = main.ClockTools.StopwatchText;
            window.Close();
            return result;
        }

        var digital = Run(StopwatchDisplay.Digital);
        var analog = Run(StopwatchDisplay.Analog);

        Assert.Equal(digital, analog);
        Assert.Equal("01:25.8", analog.Text); // 85.8 s
        Assert.Equal("Resume", analog.Button);
        Assert.False(analog.Running);
        Assert.Equal("00:00.0", analog.AfterReset);
    }

    [AvaloniaFact]
    public void SwitchingBetweenDigitalAndAnalogMidRun_KeepsTheTimeRunning()
    {
        var (window, main, clock, _) = Show(1200, 760, StopwatchDisplay.Digital);
        var face = Face(window);
        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(10));

        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog;
        Dispatcher.UIThread.RunJobs();
        Assert.True(face.IsVisible && face.IsAnimating && face.IsRunning);
        clock.Advance(TimeSpan.FromSeconds(5));

        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Digital;
        Dispatcher.UIThread.RunJobs();
        Assert.False(face.IsVisible);
        Assert.False(face.IsAnimating); // nothing animates behind a digital face
        clock.Advance(TimeSpan.FromSeconds(1));
        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(0.5));
        main.ClockTools.Tick();

        Assert.True(main.ClockTools.StopwatchRunning);
        Assert.Equal("00:16.5", main.ClockTools.StopwatchText); // 16.5 s: never reset, never behind
        Assert.Equal(16.5, main.ClockTools.StopwatchElapsedSeconds(), 3);
        Assert.True(face.IsAnimating);
        window.Close();
    }

    [AvaloniaFact]
    public void SwitchingTheDisplay_AddsNoSubscriptions_AndRemovingTheWidgetLeavesNone()
    {
        var clock = new FakeTimeProvider(UiSnapshot.SampleNow);
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: clock);
        var main = (MainViewModel)window.DataContext!;
        var toolsBefore = Handlers(main.ClockTools);
        var settingsBefore = Handlers(main.ClockSettings);

        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 1, layout.Columns[1].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        main.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch);
        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog;
        Dispatcher.UIThread.RunJobs();
        var face = Face(window);
        Press(window, "ClockStopwatchStart");
        var toolsWhileShown = Handlers(main.ClockTools);
        var settingsWhileShown = Handlers(main.ClockSettings);
        Assert.True(toolsWhileShown > toolsBefore && settingsWhileShown > settingsBefore); // it really does listen while it is there

        for (var i = 0; i < 6; i++)
        {
            main.ClockSettings.StopwatchIndex = i % 2; // back and forth
            Dispatcher.UIThread.RunJobs();
        }
        Assert.True(toolsWhileShown == Handlers(main.ClockTools), $"tools handlers grew from {toolsWhileShown} to {Handlers(main.ClockTools)} when switching");
        Assert.True(settingsWhileShown == Handlers(main.ClockSettings), $"settings handlers grew from {settingsWhileShown} to {Handlers(main.ClockSettings)} when switching");

        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog;
        Dispatcher.UIThread.RunJobs();
        Assert.True(face.IsAnimating);
        main.Layout.Apply(main.Layout.Layout.RemoveWidget(WidgetRegistry.Clock));
        Dispatcher.UIThread.RunJobs();

        Assert.False(face.IsAnimating); // no frames asked for after the face has gone
        Assert.True(toolsBefore == Handlers(main.ClockTools), $"tools: {toolsBefore} before, {toolsWhileShown} while shown, {Handlers(main.ClockTools)} after");
        Assert.True(settingsBefore == Handlers(main.ClockSettings), $"settings: {settingsBefore} before, {settingsWhileShown} while shown, {Handlers(main.ClockSettings)} after");
        Assert.True(main.ClockTools.StopwatchRunning); // the stopwatch itself is not the widget's: it keeps running
        window.Close();
    }

    [AvaloniaFact]
    public void ACardTooSmallForTheDial_FallsBackToDigits_WithEveryButtonStillThere()
    {
        var (window, main, clock, frame) = Show(1000, 600);
        var face = Face(window);

        Assert.False(face.IsVisible, $"a {frame.Bounds.Height:0} px card has no room for a dial");
        Assert.False(face.IsAnimating);
        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(3));
        main.ClockTools.Tick();
        Assert.Equal("00:03.0", Named<TextBlock>(window, "ClockStopwatchText").Text);
        Assert.False(face.IsAnimating); // still nothing animating: there is no dial to move
        foreach (var name in new[] { "ClockStopwatchStart", "ClockStopwatchLap", "ClockStopwatchReset" })
        {
            Assert.True(Named<Control>(window, name).IsEffectivelyVisible, name);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void Snapshot_AnalogStopwatchInANormalCard()
    {
        var (window, main, clock, frame) = Show(1200, 760);
        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(41.6));
        main.ClockTools.Tick();
        Dispatcher.UIThread.RunJobs();
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(new PixelSize((int)frame.Bounds.Width, (int)frame.Bounds.Height));
        bitmap.Render(frame);
        bitmap.Save(Path.Combine(directory, "clock-stopwatch-analog-card.png"));
        window.Close();
    }
}
