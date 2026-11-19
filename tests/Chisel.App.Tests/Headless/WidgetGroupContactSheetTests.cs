using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

// Capture mode for tabbed groups: Notes + Tasks, Weather + Media + Clock, and five widgets at the narrowest width,
// in Default and Light, normal and edit mode, on one contact sheet (artifacts/ui/widget-groups-contact-sheet.png).
// Each tile is also saved on its own.
public class WidgetGroupContactSheetTests
{
    private const int TileWidth = 480;
    private const int TileHeight = 300;

    private static readonly WeatherSnapshot Rain = new(14, 12, WeatherCondition.Rain, 16, 9,
        Enumerable.Range(0, 8).Select(i => new HourlyWeather(new TimeOnly(10 + i, 0), WeatherCondition.Rain, 14 - i)).ToList());

    private static WriteableBitmap Capture(string scene, string mode, bool editing, string name)
    {
        var narrow = scene == "five";
        var window = UiSnapshot.ShowMainWindow(narrow ? 300 : 1200, narrow ? 620 : 760, themeMode: mode,
            media: new FakeMediaService { Current = FakeMediaService.Sample() }, volume: new FakeVolumeService());
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(WeatherState.Ready, Rain);
        var layout = main.Layout.Layout;
        switch (scene)
        {
            case "notes-tasks":
                layout = layout.Combine("notes", "tasks");
                break;
            case "weather-media-clock":
                foreach (var id in new[] { WidgetRegistry.Weather, WidgetRegistry.Media, WidgetRegistry.Clock })
                {
                    layout = layout.AddWidget(id, 1, layout.Columns[1].Widgets.Count, 1);
                }
                layout = layout.Combine(WidgetRegistry.Media, WidgetRegistry.Weather).Combine(WidgetRegistry.Clock, WidgetRegistry.Weather);
                break;
            default:
                var ids = new[] { "tasks", "notes", "links", "schedule", "clock" };
                layout = new WidgetLayout(new[]
                {
                    new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1, null, ids.Select(id => new GroupMember(id)).ToList(), 2) }),
                }, new[] { WidgetRegistry.Weather, WidgetRegistry.Media });
                break;
        }
        main.Layout.Apply(layout);
        if (editing)
        {
            main.Layout.EnterEditCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, name);
        var frame = window.CaptureRenderedFrame()!;
        window.Close();
        return frame;
    }

    [AvaloniaFact]
    public void Render_ContactSheet_OfTheGroupsInBothModes()
    {
        var tiles = new List<(string Label, WriteableBitmap Image)>();
        foreach (var scene in new[] { "notes-tasks", "weather-media-clock", "five" })
        {
            foreach (var mode in new[] { "default", "light" })
            {
                foreach (var editing in new[] { false, true })
                {
                    var tag = $"{scene}-{mode}-{(editing ? "edit" : "normal")}";
                    tiles.Add((tag, Capture(scene, mode, editing, $"group-{tag}")));
                }
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
        var directory = Path.GetDirectoryName(UiSnapshot.Render(UiSnapshot.ShowMainWindow(), "group-reset"))!;
        frame.Save(Path.Combine(directory, "widget-groups-contact-sheet.png"));
        sheet.Close();
        // Leave the app in its default theme for the next test.
        UiSnapshot.ShowMainWindow().Close();
    }
}
