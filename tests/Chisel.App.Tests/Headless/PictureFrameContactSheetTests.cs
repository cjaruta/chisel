using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;
using Xunit;

namespace Chisel.App.Tests.Headless;

// Capture for picture frames, built from pictures generated on the spot: a landscape, a portrait, a tiny and a huge
// picture, an animated GIF, a slideshow, the placeholder while a dropped picture loads, the adjust state, and one drop
// preview per zone, in Default and Light, on one contact sheet (artifacts/ui/picture-frame-contact-sheet.png).
public class PictureFrameContactSheetTests
{
    private const string A = "pictureframe:aaa111";
    private const string B = "pictureframe:bbb222";
    private const int TileWidth = 480;
    private const int TileHeight = 300;

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static void WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            Settle();
            Thread.Sleep(10);
        }
        Settle();
    }

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.MemberIds.Contains(id));

    private static PictureFrameView View(Window window, string id)
    {
        var frame = Frame(window, id);
        return (PictureFrameView)frame.Views[frame.MemberIds.ToList().IndexOf(id)];
    }

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static WriteableBitmap Capture(string scene, string mode)
    {
        using var kit = new PictureKit();
        var window = UiSnapshot.ShowMainWindow(1200, 760, themeMode: mode, pictures: kit);
        var main = (MainViewModel)window.DataContext!;
        main.Layout.Apply(new WidgetLayout(new[]
        {
            new LayoutColumn(1.3, new[] { new WidgetPlacement(A) }),
            new LayoutColumn(1, new[] { new WidgetPlacement("tasks"), new WidgetPlacement(B), new WidgetPlacement("notes") }),
        }, WidgetRegistry.DefaultLayout.HiddenWidgetIds));
        Settle();

        void Put(string frame, params PictureAsset[] assets) =>
            main.Layout.UpdatePictures(p => p.AddPictures(frame, assets.Select(a => a.Id)));
        void Set(string frame, Func<FrameOptions, FrameOptions> change) =>
            main.Layout.UpdatePictures(p => p.SetOptions(frame, change(p.Get(frame).Options)));
        TaskCompletionSource<DownloadResult>? held = null;

        switch (scene)
        {
            case "fill-and-fit":
                Put(A, kit.Add(SampleImages.Landscape(), "landscape.png"));
                Put(B, kit.Add(SampleImages.Portrait(), "portrait.png"));
                Set(B, o => o with { Fit = FitMode.Fit, BlurFill = true });
                break;
            case "tiny-and-huge":
                Put(A, kit.Add(SampleImages.Tiny(), "tiny.png"));
                Put(B, kit.Add(SampleImages.Huge(), "huge.png"));
                Set(B, o => o with { Fit = FitMode.Fit });
                break;
            case "gif-and-slideshow":
                Put(A, kit.Add(SampleImages.Gif(48), "anim.gif"));
                Put(B, kit.Add(SampleImages.Landscape(), "one.png"), kit.Add(SampleImages.Portrait(), "two.png"), kit.Add(SampleImages.Tiny(), "three.png"));
                Set(B, o => o with { Mode = SlideshowMode.Cycle, IntervalSeconds = 30, Order = SlideshowOrder.Shuffle });
                break;
            case "loading-and-empty":
                held = new TaskCompletionSource<DownloadResult>();
                kit.Downloader.Held["https://example.com/slow.png"] = held.Task;
                main.Layout.EnterEditCommand.Execute(null);
                Settle();
                var point = InWindow(Frame(window, B), window).Center;
                Host(window).ExternalDragOver(window.TranslatePoint(point, Host(window))!.Value);
                var placement = Host(window).ExternalDrop(window.TranslatePoint(point, Host(window))!.Value, 1)!;
                _ = main.PictureFrames.DropAsync(placement, [new UrlSource("https://example.com/slow.png")]);
                main.Layout.DoneCommand.Execute(null);
                break;
            case "adjust":
                Put(A, kit.Add(SampleImages.Landscape(), "landscape.png"));
                main.Layout.EnterEditCommand.Execute(null);
                Settle();
                WaitUntil(() => View(window, A).Canvas.Current is not null);
                main.Layout.BeginAdjust(A);
                main.PictureFrames.For(A).AdjustZoom(6);
                break;
            case "drop-add":
            case "drop-tab":
            case "drop-newcolumn":
                Put(A, kit.Add(SampleImages.Landscape(), "landscape.png"));
                main.Layout.EnterEditCommand.Execute(null);
                Settle();
                var target = scene switch
                {
                    "drop-add" => InWindow(Frame(window, A), window).Center,
                    "drop-tab" => InWindow(Frame(window, "tasks"), window).Center,
                    _ => new Point(6, 300),
                };
                Host(window).ExternalDragOver(window.TranslatePoint(target, Host(window))!.Value);
                break;
        }
        Settle();
        bool Ready(string id) => View(window, id).Frame!.Count == 0 || View(window, id).Canvas.Current is not null || View(window, id).IsPlayingGif;
        WaitUntil(() => Ready(A) && Ready(B));
        Settle();

        UiSnapshot.Render(window, $"picture-{scene}-{mode}");
        var frame = window.CaptureRenderedFrame()!;
        held?.SetResult(new DownloadResult(null, DownloadError.Failed, "Couldn't download that picture."));
        Settle();
        window.Close();
        return frame;
    }

    [AvaloniaFact]
    public void Render_ContactSheet_OfPictureFrames()
    {
        var tiles = new List<(string Label, WriteableBitmap Image)>();
        foreach (var scene in new[] { "fill-and-fit", "tiny-and-huge", "gif-and-slideshow", "loading-and-empty", "adjust", "drop-add", "drop-tab", "drop-newcolumn" })
        {
            foreach (var mode in new[] { "default", "light" })
            {
                tiles.Add(($"{scene}-{mode}", Capture(scene, mode)));
            }
        }

        const int columns = 4;
        var grid = new Grid { Background = Brushes.Black };
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(TileWidth + 8, GridUnitType.Pixel));
        }
        var rows = (tiles.Count + columns - 1) / columns;
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(TileHeight + 26, GridUnitType.Pixel));
        }
        for (var i = 0; i < tiles.Count; i++)
        {
            var cell = new StackPanel { Margin = new Thickness(4, 2) };
            cell.Children.Add(new TextBlock { Text = tiles[i].Label, Foreground = Brushes.White, FontSize = 14, Height = 22 });
            cell.Children.Add(new Image { Source = tiles[i].Image, Width = TileWidth, Height = TileHeight, Stretch = Stretch.Uniform });
            Grid.SetColumn(cell, i % columns);
            Grid.SetRow(cell, i / columns);
            grid.Children.Add(cell);
        }
        var sheet = new Window { Width = columns * (TileWidth + 8), Height = rows * (TileHeight + 26), Content = grid };
        sheet.Show();
        Settle();
        using var frame = sheet.CaptureRenderedFrame()!;
        var directory = Path.GetDirectoryName(UiSnapshot.Render(UiSnapshot.ShowMainWindow(), "picture-reset"))!;
        frame.Save(Path.Combine(directory, "picture-frame-contact-sheet.png"));
        sheet.Close();
        // Every captured tile is a full-window bitmap: let them go now.
        foreach (var (_, image) in tiles)
        {
            image.Dispose();
        }
        Settle();
        // Leave the app in its default theme for the next test.
        UiSnapshot.ShowMainWindow().Close();
    }
}
