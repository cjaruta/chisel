using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;

namespace Chisel.App.Tests.Headless;

public class SpectrumBarsTests
{
    private sealed class RecordingSource : ISpectrumSource
    {
        public int BandCount => 8;
        public bool IsRunning { get; private set; }
        public int Starts, Stops;
        public float Level = 0.5f;
        public void Start() { IsRunning = true; Starts++; }
        public void Stop() { IsRunning = false; Stops++; }
        public void ReadLevels(Span<float> levels, double elapsedSeconds) => levels.Fill(IsRunning ? Level : 0);
        public void Dispose() { }
    }

    private static readonly Size BarsSize = new(120, 24);

    private static byte[] Draw(float[] levels)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)BarsSize.Width, (int)BarsSize.Height));
        using (var context = bitmap.CreateDrawingContext())
        {
            SpectrumBars.Draw(context, levels, BarsSize, Brushes.Orange);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    [AvaloniaFact]
    public void Draw_ShowsTheLevels_AndLoudBarsLookDifferentFromFlatOnes()
    {
        var flat = Draw(new float[8]);
        var loud = Draw([0.2f, 0.9f, 0.5f, 1f, 0.3f, 0.7f, 0.1f, 0.6f]);

        Assert.NotEqual(flat, loud);
        Assert.Equal(flat, Draw(new float[8]));
    }

    [AvaloniaFact]
    public void Draw_AllocatesNothingInSteadyState()
    {
        const int frames = 200;
        var levels = new float[20];
        var bitmap = new RenderTargetBitmap(new PixelSize((int)BarsSize.Width, (int)BarsSize.Height));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 12; i++)
        {
            SpectrumBars.Draw(context, levels, BarsSize, Brushes.Orange);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < frames; i++)
        {
            for (var b = 0; b < levels.Length; b++)
            {
                levels[b] = (b + i) % 10 / 10f;
            }
            SpectrumBars.Draw(context, levels, BarsSize, Brushes.Orange);
        }
        var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
        Assert.True(perFrame <= 64, $"{perFrame} B/frame");
    }

    // A window with the media widget, a recording audio source and the given session.
    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main, FakeMediaService Service, RecordingSource Source) Show(
        MediaSessionInfo? info)
    {
        var service = new FakeMediaService { Current = info };
        var source = new RecordingSource();
        var window = UiSnapshot.ShowMainWindow(media: service, spectrum: source);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1.4));
        Dispatcher.UIThread.RunJobs();
        return (window, main, service, source);
    }

    private static SpectrumBars? Bars(Chisel.App.Views.MainWindow w) => w.GetVisualDescendants().OfType<SpectrumBars>().FirstOrDefault();

    [AvaloniaFact]
    public void CapturesOnlyWhileTheWidgetIsPlayingAndShowing()
    {
        var (window, main, service, source) = Show(FakeMediaService.Sample());
        Assert.True(source.IsRunning);
        Assert.True(Bars(window)!.IsVisible);

        service.Current = FakeMediaService.Sample(MediaPlaybackState.Paused);
        Assert.False(source.IsRunning);

        service.Current = FakeMediaService.Sample();
        Assert.True(source.IsRunning);

        service.Current = null;
        Assert.False(source.IsRunning);

        service.Current = FakeMediaService.Sample();
        main.Layout.Apply(main.Layout.Layout.RemoveWidget(WidgetRegistry.Media));
        Dispatcher.UIThread.RunJobs();
        Assert.False(source.IsRunning);
    }

    [AvaloniaFact]
    public void Capture_StopsWhenTheWindowIsMinimized_AndResumesWhenRestored()
    {
        var (window, _, _, source) = Show(FakeMediaService.Sample());
        Assert.True(source.IsRunning);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(source.IsRunning);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(source.IsRunning);
    }

    [AvaloniaFact]
    public void TheSetting_HidesTheBarsAndNeverStartsCapture()
    {
        var (window, main, _, source) = Show(FakeMediaService.Sample());

        main.MediaSettings.ShowVisualizer = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(source.IsRunning);
        Assert.False(Bars(window)!.IsVisible);

        main.MediaSettings.ShowVisualizer = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(source.IsRunning);
    }

    [AvaloniaFact]
    public void AdvancingTheBars_ReadsLevelsFromTheSource()
    {
        var (window, _, _, source) = Show(FakeMediaService.Sample());
        var bars = Bars(window)!;

        bars.Advance(0.033);
        Assert.Equal(0.5f, bars.Levels[0]);

        source.Level = 0;
        bars.Advance(0.033);
        Assert.All(bars.Levels.ToArray(), l => Assert.Equal(0f, l));
    }

    [AvaloniaFact]
    public void SyntheticSignal_LightsLowAndHighBands_AndFallsFlatWhenStopped()
    {
        using var source = new SyntheticSpectrumSource();
        var levels = new float[source.BandCount];
        source.Start();
        for (var i = 0; i < 30; i++)
        {
            source.ReadLevels(levels, 0.033);
        }
        Assert.True(levels[..5].Max() > 0.3f);
        Assert.True(levels[^6..].Max() > 0.1f);

        source.Stop();
        for (var i = 0; i < 120; i++)
        {
            source.ReadLevels(levels, 0.033);
        }
        Assert.All(levels, l => Assert.Equal(0f, l));
    }
}
