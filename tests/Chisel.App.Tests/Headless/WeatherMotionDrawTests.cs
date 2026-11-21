using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chisel.App.Views.Widgets.Backdrop;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

public class WeatherMotionDrawTests
{
    // The widget's size in the 1200x760 window, and the times shown on a motion sheet (read left to right, top to bottom).
    private static readonly Size WidgetSize = new(646, 184);
    private static readonly double[] SheetTimes = [0, 1, 2, 3, 5, 8];

    private static byte[] Png(RenderTargetBitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    // Particles only, on a transparent surface.
    private static RenderTargetBitmap Particles(string key, double seconds)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)WidgetSize.Width, (int)WidgetSize.Height));
        using (var context = bitmap.CreateDrawingContext())
        {
            WeatherMotionDrawing.Draw(context, key, WidgetSize, seconds);
        }
        return bitmap;
    }

    [AvaloniaFact]
    public void Draw_PaintsSomethingForEverySceneKey()
    {
        var blank = Png(new RenderTargetBitmap(new PixelSize((int)WidgetSize.Width, (int)WidgetSize.Height)));
        foreach (var key in WeatherBackdrop.Gradients.Keys)
        {
            Assert.NotEqual(blank, Png(Particles(key, 2.5)));
        }
    }

    // After the first frame (which builds the scene's cached brushes and shapes), drawing must not allocate.
    [AvaloniaFact]
    public void Draw_AllocatesNothingInSteadyState()
    {
        const int frames = 60;
        var offenders = new List<string>();
        foreach (var key in WeatherBackdrop.Gradients.Keys)
        {
            var bitmap = new RenderTargetBitmap(new PixelSize((int)WidgetSize.Width, (int)WidgetSize.Height));
            using var context = bitmap.CreateDrawingContext();
            for (var i = 0; i < 12; i++)
            {
                WeatherMotionDrawing.Draw(context, key, WidgetSize, i * 0.04);
            }
            // Best of three rounds, so one-off runtime noise (JIT, tiering) can't fail a steady scene.
            var perFrame = long.MaxValue;
            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < frames; i++)
                {
                    WeatherMotionDrawing.Draw(context, key, WidgetSize, 1 + i * 0.04);
                }
                perFrame = Math.Min(perFrame, (GC.GetAllocatedBytesForCurrentThread() - before) / frames);
            }
            if (perFrame > 64)
            {
                offenders.Add($"{key}: {perFrame} B/frame");
            }
        }
        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    // artifacts/ui/motion/<scene key>.png: the gradient with the motion at each of SheetTimes, one tile per time.
    [AvaloniaFact]
    public void MotionSheets_AreWrittenForEveryScene()
    {
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui", "motion");
        Directory.CreateDirectory(directory);
        const int gap = 8;
        foreach (var (key, gradient) in WeatherBackdrop.Gradients)
        {
            var width = (int)WidgetSize.Width;
            var height = (int)WidgetSize.Height;
            var sheet = new RenderTargetBitmap(new PixelSize(3 * width + 4 * gap, 2 * height + 3 * gap));
            using (var context = sheet.CreateDrawingContext())
            {
                context.FillRectangle(new SolidColorBrush(Color.Parse("#14161A")), new Rect(0, 0, sheet.Size.Width, sheet.Size.Height));
                for (var i = 0; i < SheetTimes.Length; i++)
                {
                    var origin = new Point(gap + i % 3 * (width + gap), gap + i / 3 * (height + gap));
                    var tile = new Rect(origin, WidgetSize);
                    var fill = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Top), 0), new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Bottom), 1) },
                    };
                    context.DrawRectangle(fill, null, new RoundedRect(tile, 12));
                    using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
                    {
                        WeatherMotionDrawing.Draw(context, key, WidgetSize, SheetTimes[i]);
                    }
                }
            }
            sheet.Save(Path.Combine(directory, key + ".png"));
        }
        Assert.True(File.Exists(Path.Combine(directory, "rain-day.png")));
    }
}
