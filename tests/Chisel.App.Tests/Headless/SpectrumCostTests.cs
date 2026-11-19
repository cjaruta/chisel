using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
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

public class SpectrumCostTests
{
    // What one visualizer frame costs (levels from the real processor fed a synthetic signal, then the bars drawn), measured
    // without a window or the compositor and written to artifacts/ui/visualizer-cost.txt. At 30 frames a second this gives
    // the extra CPU as a share of one core. The capture scene "media-visualizer" measures the same thing in the running app.
    [AvaloniaFact]
    public void OneFrame_CostsAFractionOfAPercentOfACore()
    {
        using var source = new SyntheticSpectrumSource();
        source.Start();
        var levels = new float[source.BandCount];
        var bitmap = new RenderTargetBitmap(new PixelSize(120, 22));
        using var context = bitmap.CreateDrawingContext();
        const double frame = 1.0 / 30;

        void Frame()
        {
            source.ReadLevels(levels, frame);
            SpectrumBars.Draw(context, levels, new Size(120, 22), Brushes.Orange);
        }

        for (var i = 0; i < 200; i++)
        {
            Frame();
        }
        const int frames = 1000;
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < frames; i++)
        {
            Frame();
        }
        var perFrameMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds / frames;
        var percentOfCore = perFrameMs * 30 / 10;

        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "visualizer-cost.txt"),
            $"per frame {perFrameMs:F3} ms (incl. synthetic signal), at 30 fps = {percentOfCore:F2}% of one core\n");
        Assert.True(percentOfCore < 3, $"{percentOfCore:F2}% of a core");
    }

    [AvaloniaFact]
    public void TheWidget_WithMovingBars_RendersToAPng()
    {
        var service = new FakeMediaService { Current = FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png() } };
        var source = new SyntheticSpectrumSource();
        var window = UiSnapshot.ShowMainWindow(1200, 760, media: service, spectrum: source);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1.4));
        Dispatcher.UIThread.RunJobs();

        var bars = window.GetVisualDescendants().OfType<SpectrumBars>().Single();
        for (var i = 0; i < 40; i++)
        {
            bars.Advance(1.0 / 30);
        }
        Assert.True(bars.IsCapturing);
        UiSnapshot.Render(window, "media-visualizer");
    }
}
