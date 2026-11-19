using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Dev;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;

namespace Chisel.App.Tests.Headless;

// Renders the media widget over generated covers (warm, cool, high contrast, monochrome, none) in Default, Light and one color
// scheme, and puts every tile on one contact sheet: artifacts/ui/media-gradient-contact-sheet.png.
public class MediaGradientContactSheetTests
{
    private static readonly string[] Modes = ["default", "light", "ocean"];
    private static readonly string[] Covers = ["warm", "cool", "contrast", "mono", "none"];
    private const double Scale = 0.6;
    private const int Label = 16;

    [AvaloniaFact]
    public void ContactSheet_ShowsEveryCoverInEveryMode()
    {
        var tiles = new (string Mode, string Cover, WriteableBitmap Image, Rect Crop)[Modes.Length * Covers.Length];
        var index = 0;
        foreach (var mode in Modes)
        {
            foreach (var cover in Covers)
            {
                var info = FakeMediaService.Sample() with { AlbumArt = cover == "none" ? null : SampleArt.Cover(cover) };
                var window = UiSnapshot.ShowMainWindow(media: new FakeMediaService { Current = info }, themeMode: mode);
                var main = (MainViewModel)window.DataContext!;
                var layout = main.Layout.Layout;
                main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1.6));
                var until = DateTime.UtcNow.AddSeconds(5);
                do
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(5);
                }
                while (!main.MediaBackground.Settled.IsCompleted && DateTime.UtcNow < until);
                Dispatcher.UIThread.RunJobs();

                var frame = window.GetVisualDescendants().OfType<MediaWidgetView>().Single().FindAncestorOfType<WidgetFrame>()!;
                var origin = frame.TranslatePoint(default, window)!.Value;
                var image = window.CaptureRenderedFrame()!;
                tiles[index++] = (mode, cover, image, new Rect(origin, frame.Bounds.Size));
                window.Close();
            }
        }

        var tileWidth = (int)(tiles.Max(t => t.Crop.Width) * Scale);
        var tileHeight = (int)(tiles.Max(t => t.Crop.Height) * Scale);
        const int gap = 8;
        var width = Modes.Length * (tileWidth + gap) + gap;
        var height = Covers.Length * (tileHeight + Label + gap) + gap;
        using var sheet = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = sheet.CreateDrawingContext())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x34)), null, new Rect(0, 0, width, height));
            for (var i = 0; i < tiles.Length; i++)
            {
                var (mode, cover, image, crop) = tiles[i];
                var column = i / Covers.Length;
                var row = i % Covers.Length;
                var x = gap + column * (tileWidth + gap);
                var y = gap + row * (tileHeight + Label + gap);
                var text = new FormattedText($"{mode} / {cover}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 11, Brushes.White);
                context.DrawText(text, new Point(x, y));
                context.DrawImage(image, crop, new Rect(x, y + Label, crop.Width * Scale, crop.Height * Scale));
            }
        }

        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, "media-gradient-contact-sheet.png"));
        sheet.Save(file);
        foreach (var tile in tiles)
        {
            tile.Image.Dispose();
        }
        // Leave the shared app resources on the default theme for the tests that follow.
        UiSnapshot.ShowMainWindow().Close();
    }
}
