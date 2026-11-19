using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Chisel.App.Dev;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;
using Chisel.Core.Theme;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

// What the gradient costs, measured without a window or the compositor and written to artifacts/ui/media-gradient-cost.txt:
// the time from a track change to the palette being applied (real decoder, real extractor), the one-off cost of building a
// palette's bitmaps, and the cost of a drift frame and a cross-fade frame turned into a share of one core.
public class MediaGradientCostTests
{
    private static readonly Size Widget = new(646, 386);

    [AvaloniaFact]
    public void Gradient_CostsAFractionOfAPercentOfACore_AndThePaletteIsReadyWithinAFewHundredMilliseconds()
    {
        var lines = new List<string>();

        // Time to palette: the cover changes, and the view model decodes, shrinks, extracts and adjusts it on a worker thread.
        var service = new FakeMediaService { Current = FakeMediaService.Sample() };
        var media = new MediaViewModel(service, new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero))) { IsActive = true };
        var background = new MediaBackgroundViewModel(media, new MediaSettingsViewModel(null), null, new AvaloniaArtSampler());
        var worst = 0.0;
        foreach (var (kind, size) in new[] { ("warm", 160), ("cool", 160), ("contrast", 640), ("mono", 640), ("warm", 1000), ("cool", 1000) })
        {
            service.Current = FakeMediaService.Sample() with { AlbumArt = SampleArt.Cover(kind, size) };
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!background.Settled.IsCompleted && DateTime.UtcNow < until)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(1);
            }
            Dispatcher.UIThread.RunJobs();
            worst = Math.Max(worst, background.LastPaletteMilliseconds);
            lines.Add($"palette for a {size}px {kind} cover: {background.LastPaletteMilliseconds:F1} ms from track change to colors applied{(kind == "warm" && size == 160 ? " (first, includes JIT)" : "")}");
        }

        // One-off: building a palette's 48 bitmaps (on the UI thread when the palette is applied).
        var control = new AlbumGradientBackground { Animate = true, CornerRadius = new CornerRadius(0, 0, 12, 12) };
        var build = Stopwatch.StartNew();
        control.SetColors(background.Colors, fade: false);
        lines.Add($"building a palette's bitmaps: {build.Elapsed.TotalMilliseconds:F1} ms (once per track)");

        var bitmap = new RenderTargetBitmap(new PixelSize((int)Widget.Width, (int)Widget.Height));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 200; i++)
        {
            control.Advance(0.05);
            control.DrawTo(context, Widget);
        }

        // Drift: a tick every 50 ms does almost nothing; a redraw happens when the step changes (48 steps over 24 s = 2 a second).
        const int frames = 2000;
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < frames; i++)
        {
            control.DrawTo(context, Widget);
        }
        var drawMs = timer.Elapsed.TotalMilliseconds / frames;
        timer.Restart();
        for (var i = 0; i < frames; i++)
        {
            control.Advance(0.05);
        }
        var tickMs = timer.Elapsed.TotalMilliseconds / frames;
        var driftPercent = (2 * drawMs + 20 * tickMs) / 10;
        lines.Add($"drift: redraw {drawMs:F3} ms x 2 a second + tick {tickMs:F4} ms x 20 a second = {driftPercent:F2}% of one core");

        // Cross-fade: 20 redraws a second for half a second per track change.
        control.SetColors(background.Colors, fade: false);
        control.SetColors([new Rgb(10, 20, 30), new Rgb(40, 50, 60), new Rgb(70, 80, 90)], fade: true);
        timer.Restart();
        for (var i = 0; i < frames; i++)
        {
            control.DrawTo(context, Widget);
        }
        var fadeMs = timer.Elapsed.TotalMilliseconds / frames;
        lines.Add($"cross-fade frame: {fadeMs:F3} ms x 20 a second for 0.5 s per track change = {fadeMs * 20 / 10:F2}% of one core while it runs");

        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "media-gradient-cost.txt"), lines);
        Assert.True(driftPercent < 3, $"{driftPercent:F2}% of a core");
        Assert.True(worst < 1000, $"{worst:F0} ms to palette");
    }
}
