using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Theme;

namespace Chisel.App.Tests.Headless;

// Capture mode for the theme system: the dashboard and the settings screen in every mode with the default accent, and
// two modes with a custom accent, on one contact sheet (artifacts/ui/theme-contact-sheet.png). Each tile is also saved
// on its own so one can be looked at closely.
public class ThemeContactSheetTests
{
    private const int TileWidth = 560;
    private const int TileHeight = 355;

    private static WriteableBitmap Capture(string mode, string? accent, bool settings, string name)
    {
        var window = UiSnapshot.ShowMainWindow(themeMode: mode, accent: accent,
            media: new FakeMediaService { Current = FakeMediaService.Sample() }, volume: new FakeVolumeService());
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        foreach (var id in new[] { WidgetRegistry.Weather, WidgetRegistry.Media, WidgetRegistry.Clock })
        {
            layout = layout.AddWidget(id, 1, layout.Columns[1].Widgets.Count, 1);
        }
        main.Layout.Apply(layout);
        main.Settings.IsOpen = settings;
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, name);
        var frame = window.CaptureRenderedFrame()!;
        window.Close();
        return frame;
    }

    [AvaloniaFact]
    public void Render_ContactSheet_OfEveryModeAndTwoCustomAccents()
    {
        var scenes = ThemeModes.All.Select(m => (m.Id, (string?)null))
            .Concat([("light", "#4C9AF0"), ("plum", "#B5D43D")]).ToList();

        var tiles = new List<(string Label, WriteableBitmap Image)>();
        foreach (var (mode, accent) in scenes)
        {
            var tag = accent is null ? mode : $"{mode}-{accent.TrimStart('#')}";
            tiles.Add(($"{tag} dashboard", Capture(mode, accent, false, $"theme-{tag}-dashboard")));
            tiles.Add(($"{tag} settings", Capture(mode, accent, true, $"theme-{tag}-settings")));
        }

        const int columns = 3;
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
        var directory = Path.GetDirectoryName(UiSnapshot.Render(UiSnapshot.ShowMainWindow(), "theme-reset"))!;
        frame.Save(Path.Combine(directory, "theme-contact-sheet.png"));
        sheet.Close();
        // Leave the app in its default theme for the next test.
        UiSnapshot.ShowMainWindow().Close();
    }
}
