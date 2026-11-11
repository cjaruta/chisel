using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

public class SettingsPanelHeadlessTests
{
    private static Control Named(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    private static IEnumerable<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);

    private static SettingsViewModel Settings(MainWindow window) => ((MainViewModel)window.DataContext!).Settings;

    [AvaloniaFact]
    public void GearButton_OpensAndClosesTheSettingsPanel()
    {
        var window = UiSnapshot.ShowMainWindow();
        var gear = (Button)Named(window, "SettingsButton");
        Assert.False(Named(window, "SettingsPanel").IsVisible);

        gear.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named(window, "SettingsPanel").IsVisible);

        gear.Command.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named(window, "SettingsPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_ClosesTheSettingsPanel()
    {
        var window = UiSnapshot.ShowMainWindow();
        Settings(window).IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named(window, "SettingsPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TwelveHourMode_UpdatesTheClockAndScheduleInPlace()
    {
        var window = UiSnapshot.ShowMainWindow();
        Assert.Contains("09:30:00", Texts(window));

        Settings(window).IsOpen = true;
        Settings(window).UseTwelveHour = true;
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "settings-12h");

        var texts = Texts(window).ToList();
        Assert.Contains("9:30:00 AM", texts);
        Assert.Contains("8:00 AM-11:00 AM", texts);
        Assert.Contains("1:00 PM", texts);
        Assert.True(Named(window, "SettingsPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TwelveHourMode_TimeLabelsAreNotClipped()
    {
        var window = UiSnapshot.ShowMainWindow();
        Settings(window).UseTwelveHour = true;
        Dispatcher.UIThread.RunJobs();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("timeLabel")).ToList();

        Assert.NotEmpty(labels);
        Assert.All(labels, label =>
            Assert.True(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 0.5,
                $"'{label.Text}' needs {label.TextLayout.WidthIncludingTrailingWhitespace:F0}px but has {label.Bounds.Width:F0}px"));
        window.Close();
    }
}
