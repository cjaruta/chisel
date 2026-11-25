using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Theme;

namespace Chisel.App.Tests.Headless;

public class ThemeHeadlessTests
{
    private static Color Brush(string key) =>
        Application.Current!.TryFindResource(key, null, out var found) && found is ISolidColorBrush b ? b.Color : default;

    private static Color Of(Rgb c) => Color.FromRgb(c.R, c.G, c.B);

    private static T Named<T>(MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static SettingsViewModel Settings(MainWindow w) => ((MainViewModel)w.DataContext!).Settings;

    // The app's resources are shared by every test, so each one closes its window and puts the default theme back.
    private static void Finish(MainWindow window)
    {
        window.Close();
        UiSnapshot.ShowMainWindow().Close();
    }

    // True if any pixel inside the area looks warm (orange) or cool (blue). The widget can be partly faded in a headless
    // frame, so this checks the hue rather than the exact color.
    private static bool HasHue(MainWindow window, Rect area, bool warm)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        using var bitmap = frame.Lock();
        var pixels = new byte[bitmap.RowBytes * bitmap.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.Address, pixels, 0, pixels.Length);
        for (var y = (int)area.Top; y < (int)area.Bottom && y < bitmap.Size.Height; y++)
        {
            for (var x = (int)area.Left; x < (int)area.Right && x < bitmap.Size.Width; x++)
            {
                var i = y * bitmap.RowBytes + x * 4;
                // Which channel comes first depends on the frame's pixel format.
                var bgra = bitmap.Format == Avalonia.Platform.PixelFormat.Bgra8888;
                int blue = bgra ? pixels[i] : pixels[i + 2], red = bgra ? pixels[i + 2] : pixels[i];
                if (warm ? red - blue > 40 : blue - red > 40)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [AvaloniaFact]
    public void TheSavedModeAndAccent_ShowFromTheFirstWindow()
    {
        var window = UiSnapshot.ShowMainWindow(themeMode: "light", accent: "#4C9AF0");

        Assert.Equal(ThemeVariant.Light, Application.Current!.ActualThemeVariant);
        Assert.Equal(Of(ThemeModes.ById("light").Window), Brush("WindowBackgroundBrush"));
        Assert.Equal(Of(ThemeModes.Accent(ThemeModes.ById("light"), Rgb.Parse("#4C9AF0")).Fill), Brush("AccentBrush"));
        Finish(window);
    }

    [AvaloniaFact]
    public void TheModeList_OffersSystemFirst()
    {
        var window = UiSnapshot.ShowMainWindow();
        Settings(window).IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("System", Settings(window).ThemeModeOptions[0].Name);
        UiSnapshot.Render(window, "theme-system-option");
        Finish(window);
    }

    [AvaloniaFact]
    public void SwitchingMode_RestylesTokensAndFluentControlsLive()
    {
        var window = UiSnapshot.ShowMainWindow();
        Settings(window).IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var progress = Named<ProgressBar>(window, "AccentPreviewBar");
        var forest = ThemeModes.ById("forest");

        Settings(window).SelectedThemeMode = Settings(window).ThemeModeOptions.Single(o => o.Id == "forest");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Of(forest.Card), Brush("CardBackgroundBrush"));
        Assert.Equal(Of(forest.Text), Brush("TextBrush"));
        // A plain Fluent control follows the same accent, not Fluent's own blue.
        var expected = Of(ThemeModes.Accent(forest, Rgb.Parse(ThemeModes.DefaultAccent)).Fill);
        Assert.Equal(expected, ((ISolidColorBrush)progress.Foreground!).Color);
        Finish(window);
    }

    [AvaloniaFact]
    public void ChangingTheAccent_UpdatesAStandardControlAndRepaintsTheCustomDrawnVolumeSlider()
    {
        var window = UiSnapshot.ShowMainWindow(media: new FakeMediaService { Current = FakeMediaService.Sample() }, volume: new FakeVolumeService());
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        var slider = window.GetVisualDescendants().OfType<VolumeSlider>().Single();
        var area = new Rect(slider.TranslatePoint(default, window) ?? default, slider.Bounds.Size);
        Settings(window).IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var progress = Named<ProgressBar>(window, "AccentPreviewBar");

        Assert.True(HasHue(window, area, warm: true), "the slider starts in the default (orange) accent");

        Settings(window).SelectAccentCommand.Execute("#4C9AF0");
        var fill = Brush("AccentBrush");

        Assert.NotEqual(Of(Rgb.Parse(ThemeModes.DefaultAccent)), fill);
        Assert.True(HasHue(window, area, warm: false), "the slider repaints in the new (blue) accent");
        Assert.False(HasHue(window, area, warm: true), "no orange is left on the slider");
        Assert.Equal(fill, ((ISolidColorBrush)progress.Foreground!).Color);
        Finish(window);
    }
}
