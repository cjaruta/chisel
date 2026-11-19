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
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

// Capture mode for split view: Clock + Weather, Notes + Tasks + Media, a narrow card falling back to tabs, and one drop
// preview per zone, in Default and Light, on one contact sheet (artifacts/ui/group-split-contact-sheet.png).
public class GroupSplitContactSheetTests
{
    private const int TileWidth = 480;
    private const int TileHeight = 300;

    private static readonly WeatherSnapshot Rain = new(14, 12, WeatherCondition.Rain, 16, 9,
        Enumerable.Range(0, 8).Select(i => new HourlyWeather(new TimeOnly(10 + i, 0), WeatherCondition.Rain, 14 - i)).ToList());

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static WidgetLayout WideSplit(params string[] ids) =>
        new(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement(ids[0], 1, null, ids.Select(id => new GroupMember(id)).ToList(), 0, GroupViewMode.Split),
            }),
        }, WidgetRegistry.All.Select(w => w.Id).Where(id => !ids.Contains(id)));

    private static WriteableBitmap Capture(string scene, string mode)
    {
        var width = scene == "narrow-fallback" ? 640 : 1200;
        var window = UiSnapshot.ShowMainWindow(width, 760, themeMode: mode,
            media: new FakeMediaService { Current = FakeMediaService.Sample() }, volume: new FakeVolumeService());
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(WeatherState.Ready, Rain);
        var layout = main.Layout.Layout;
        switch (scene)
        {
            case "clock-weather":
                layout = layout.AddWidget(WidgetRegistry.Clock, 1, 0, 1).AddWidget(WidgetRegistry.Weather, 1, 1, 1)
                    .Combine(WidgetRegistry.Weather, WidgetRegistry.Clock, 500, LayoutViewModel.MinWidthOf);
                layout = layout.SetViewMode(WidgetRegistry.Clock, GroupViewMode.Split, 500, LayoutViewModel.MinWidthOf).Layout;
                break;
            case "notes-tasks-media":
                layout = WideSplit("notes", "tasks", WidgetRegistry.Media);
                break;
            case "narrow-fallback":
                layout = WideSplit("notes", "tasks", WidgetRegistry.Media);
                break;
        }
        main.Layout.Apply(layout);
        Dispatcher.UIThread.RunJobs();

        if (scene.StartsWith("drop-"))
        {
            main.Layout.EnterEditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var frames = window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).ToList();
            WidgetFrame Frame(string id) => frames.Single(f => f.WidgetId == id);
            Point Within(string id, double fraction)
            {
                var bounds = InWindow(Frame(id), window);
                return new Point(bounds.Center.X, bounds.Top + bounds.Height * fraction);
            }
            var bar = InWindow(Frame("links").EditBar, window).Center;
            window.MouseMove(bar);
            window.MouseDown(bar, MouseButton.Left);
            window.MouseMove(new Point(bar.X + 10, bar.Y + 10), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            var to = scene switch
            {
                "drop-merge" => Within("tasks", 0.5),
                "drop-above" => Within("notes", 0.03),
                "drop-below" => Within("notes", 0.97),
                _ => new Point(6, 300),
            };
            window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
        }

        UiSnapshot.Render(window, $"split-{scene}-{mode}");
        var frame = window.CaptureRenderedFrame()!;
        window.Close();
        return frame;
    }

    [AvaloniaFact]
    public void Render_ContactSheet_OfSplitViewAndTheDropPreviews()
    {
        var tiles = new List<(string Label, WriteableBitmap Image)>();
        foreach (var scene in new[] { "clock-weather", "notes-tasks-media", "narrow-fallback", "drop-merge", "drop-above", "drop-below", "drop-newcolumn" })
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
        Dispatcher.UIThread.RunJobs();
        using var frame = sheet.CaptureRenderedFrame()!;
        var directory = Path.GetDirectoryName(UiSnapshot.Render(UiSnapshot.ShowMainWindow(), "split-reset"))!;
        frame.Save(Path.Combine(directory, "group-split-contact-sheet.png"));
        sheet.Close();
        // Leave the app in its default theme for the next test.
        UiSnapshot.ShowMainWindow().Close();
    }
}
