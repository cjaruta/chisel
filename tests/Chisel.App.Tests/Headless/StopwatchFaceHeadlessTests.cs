using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

// The analog stopwatch: a dial with a sweeping seconds hand and a minutes sub-dial above the digital readout. It must behave exactly
// like the digital one, cost nothing while stopped, hidden or minimized, and allocate nothing per frame while it runs.
public class StopwatchFaceHeadlessTests : IDisposable
{
    public StopwatchFaceHeadlessTests() => SlideHost.SlideDuration = TimeSpan.Zero;

    public void Dispose() => SlideHost.SlideDuration = TimeSpan.FromMilliseconds(160);

    private static (MainWindow Window, MainViewModel Main, FakeTimeProvider Clock) Show(StopwatchDisplay display, double width = 1200, double height = 760)
    {
        var clock = new FakeTimeProvider(UiSnapshot.SampleNow);
        var window = UiSnapshot.ShowMainWindow(width, height, clock: clock);
        var main = (MainViewModel)window.DataContext!;
        main.ClockSettings.StopwatchIndex = (int)display;
        main.ClockTools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Stopwatch)]));
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        return (window, main, clock);
    }

    private static T Named<T>(MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

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

    // Runs the same little session and reports what the tool shows.
    private static (string Text, string Button, int Laps, string AfterReset) Session(StopwatchDisplay display)
    {
        var (window, main, clock) = Show(display);
        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(75.5));
        main.ClockTools.Tick();
        Press(window, "ClockStopwatchLap");
        clock.Advance(TimeSpan.FromSeconds(10));
        Press(window, "ClockStopwatchStart"); // pause
        var text = main.ClockTools.StopwatchText;
        var button = main.ClockTools.StopwatchButtonText;
        var laps = main.ClockTools.LapTexts.Count;
        Press(window, "ClockStopwatchReset");
        var afterReset = main.ClockTools.StopwatchText;
        window.Close();
        return (text, button, laps, afterReset);
    }

    [AvaloniaFact]
    public void TheAnalogDial_IsShownOnlyWhenTheLookAsksForIt()
    {
        var (digital, _, _) = Show(StopwatchDisplay.Digital);
        Assert.False(Named<StopwatchFace>(digital, "ClockStopwatchFace").IsVisible);
        digital.Close();

        var (analog, main, _) = Show(StopwatchDisplay.Analog);
        var face = Named<StopwatchFace>(analog, "ClockStopwatchFace");
        Assert.True(face.IsVisible);
        Assert.True(face.Width >= 80);
        Assert.Equal(face.Width, face.Height);

        main.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Digital;
        Dispatcher.UIThread.RunJobs();
        Assert.False(face.IsVisible);
        analog.Close();
    }

    [AvaloniaFact]
    public void StartLapAndReset_BehaveTheSame_InBothDisplays()
    {
        var digital = Session(StopwatchDisplay.Digital);
        var analog = Session(StopwatchDisplay.Analog);

        Assert.Equal(digital, analog);
        Assert.Equal("01:25.4", analog.Text);
        Assert.Equal("Resume", analog.Button);
        Assert.Equal(1, analog.Laps);
        Assert.Equal("00:00.0", analog.AfterReset);
    }

    [AvaloniaFact]
    public void FramesAreAskedForOnlyWhileItRunsAndIsOnScreen()
    {
        var (window, main, _) = Show(StopwatchDisplay.Analog);
        var face = Named<StopwatchFace>(window, "ClockStopwatchFace");
        Assert.False(face.IsAnimating); // stopped: nothing runs

        Press(window, "ClockStopwatchStart");
        Assert.True(face.IsRunning);
        Assert.True(face.IsAnimating);

        face.IsVisible = false; // hidden: it lets go
        Assert.False(face.IsAnimating);
        face.IsVisible = true;
        Assert.True(face.IsAnimating); // and picks up again when shown

        window.WindowState = WindowState.Minimized; // minimized: it lets go
        Dispatcher.UIThread.RunJobs();
        Assert.False(face.IsAnimating);
        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(face.IsAnimating);

        Press(window, "ClockStopwatchStart"); // pause
        Assert.False(face.IsRunning);
        Assert.False(face.IsAnimating);
        Assert.False(main.ClockTools.StopwatchRunning);
        window.Close();
    }

    [AvaloniaFact]
    public void TheFaceReadsTheTimeAloud_AndKeepsTheDigitalReadoutBeneath()
    {
        var (window, main, clock) = Show(StopwatchDisplay.Analog);
        var face = Named<StopwatchFace>(window, "ClockStopwatchFace");
        Assert.Equal("Stopwatch, 00:00.0", AutomationProperties.GetName(face));

        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(65.2));
        main.ClockTools.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Stopwatch, 01:05.2", AutomationProperties.GetName(face));
        Assert.Equal("01:05.2", Named<TextBlock>(window, "ClockStopwatchText").Text);
        var textTop = Named<TextBlock>(window, "ClockStopwatchText").TranslatePoint(new Point(0, 0), window)!.Value.Y;
        var faceBottom = face.TranslatePoint(new Point(0, face.Bounds.Height), window)!.Value.Y;
        Assert.True(textTop >= faceBottom - 1, "the readout sits beneath the dial");
        window.Close();
    }

    [AvaloniaFact]
    public void ASmallSlot_DropsTheDial_SoTheTimeAndButtonsStillFit()
    {
        var (window, _, _) = Show(StopwatchDisplay.Analog, 640, 330);
        var face = Named<StopwatchFace>(window, "ClockStopwatchFace");
        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock);

        if (!face.IsVisible)
        {
            Assert.True(Named<Button>(window, "ClockStopwatchStart").IsEffectivelyVisible);
        }
        else
        {
            var bottom = Named<Button>(window, "ClockStopwatchStart").TranslatePoint(new Point(0, Named<Button>(window, "ClockStopwatchStart").Bounds.Height), frame)!.Value.Y;
            Assert.True(bottom <= frame.Bounds.Height + 1, "the buttons are not pushed out of the card");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void AFrameOfTheRunningFace_AllocatesNothingWorthMentioning()
    {
        var face = new StopwatchFace { Style = ClockPresets.Apply("chrono", ClockStyle.Default), ElapsedSource = () => 83.4 };
        face.Measure(new Size(260, 260));
        face.Arrange(new Rect(0, 0, 260, 260));
        var bitmap = new RenderTargetBitmap(new PixelSize(260, 260));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 200; i++)
        {
            face.Draw(context);
        }

        // Best of three rounds: a round that happens to include one-off runtime work is not steady state.
        var perFrame = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 500; i++)
            {
                face.Draw(context);
            }
            perFrame = Math.Min(perFrame, (GC.GetAllocatedBytesForCurrentThread() - before) / 500);
        }

        Assert.True(perFrame <= 64, $"{perFrame} B per frame");
    }

    [AvaloniaTheory]
    [InlineData("classic")]
    [InlineData("chrono")]
    [InlineData("pilot")]
    [InlineData("railway")]
    public void EveryFilledLook_AlsoDrawsTheStopwatchWithoutAllocatingPerFrame(string preset)
    {
        var face = new StopwatchFace { Style = ClockPresets.Apply(preset, ClockStyle.Default), ElapsedSource = () => 1234.5 };
        face.Measure(new Size(200, 200));
        face.Arrange(new Rect(0, 0, 200, 200));
        var bitmap = new RenderTargetBitmap(new PixelSize(200, 200));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 100; i++)
        {
            face.Draw(context);
        }
        var perFrame = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 300; i++)
            {
                face.Draw(context);
            }
            perFrame = Math.Min(perFrame, (GC.GetAllocatedBytesForCurrentThread() - before) / 300);
        }

        Assert.True(perFrame <= 64, $"{preset}: {perFrame} B per frame");
    }

    [AvaloniaFact]
    public void Snapshot_StopwatchFaces()
    {
        var (window, main, clock) = Show(StopwatchDisplay.Analog, 900, 700);
        main.ClockSettings.PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == "chrono");
        Press(window, "ClockStopwatchStart");
        clock.Advance(TimeSpan.FromSeconds(95.3));
        main.ClockTools.Tick();
        Press(window, "ClockStopwatchLap");
        Dispatcher.UIThread.RunJobs();
        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == WidgetRegistry.Clock);
        var bitmap = new RenderTargetBitmap(new PixelSize((int)frame.Bounds.Width, (int)frame.Bounds.Height));
        bitmap.Render(frame);
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, "clock-stopwatch-analog.png"));
        window.Close();
    }
}
