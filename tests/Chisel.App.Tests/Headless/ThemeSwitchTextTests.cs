using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Theme;

namespace Chisel.App.Tests.Headless;

// Switching modes while the app runs: the text that takes its color from the base theme (headers, the clock, buttons, the whole
// settings menu) follows the mode every time, in every direction, not only when the app starts in it.
public class ThemeSwitchTextTests
{
    private static Rgb Of(IBrush? brush) => brush is ISolidColorBrush { Color: var c } ? new Rgb(c.R, c.G, c.B) : new Rgb(1, 2, 3);

    private static IEnumerable<TextBlock> Texts(Visual root, params string[] texts) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && texts.Contains(t.Text));

    private static void AssertFollows(MainWindow window, ThemeDefinition mode, params string[] texts)
    {
        var found = Texts(window, texts).ToList();
        Assert.NotEmpty(found);
        foreach (var text in found)
        {
            Assert.True(Of(text.Foreground) == mode.Text, $"\"{text.Text}\" is {Of(text.Foreground).ToHex()} in {mode.Id}, not {mode.Text.ToHex()}");
        }
    }

    [AvaloniaFact]
    public void TheDashboardsText_FollowsTheMode_InEverySwitch()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        var order = new[] { "light", "default", "light", "ultra-dark", "light", "slate", "ocean", "light", "forest", "plum", "default", "light" };

        foreach (var id in order)
        {
            main.Settings.SelectedThemeMode = main.Settings.ThemeModeOptions.Single(o => o.Id == id);
            Dispatcher.UIThread.RunJobs();
            var mode = ThemeModes.ById(id);

            AssertFollows(window, mode, "Tasks", "Notes", "Daily verse");
            var clock = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text is { Length: 8 } s && s[2] == ':');
            Assert.True(Of(clock.Foreground) == mode.Text, $"the clock is {Of(clock.Foreground).ToHex()} in {id}");
            var now = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NowButton");
            var label = now.GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.True(Of(label.Foreground) == mode.Text, $"the Now button is {Of(label.Foreground).ToHex()} in {id}");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingsMenu_FollowsTheMode_InEverySwitch()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        foreach (var id in new[] { "light", "default", "light", "ocean", "light" })
        {
            main.Settings.SelectedThemeMode = main.Settings.ThemeModeOptions.Single(o => o.Id == id);
            Dispatcher.UIThread.RunJobs();
            var mode = ThemeModes.ById(id);

            AssertFollows(window, mode, "Settings");
        }
        UiSnapshot.Render(window, "settings-light-after-switches");
        window.Close();
    }
}
