using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

public class SettingsTabsHeadlessTests
{
    private static T Named<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static SettingsViewModel Open(MainWindow window)
    {
        var settings = ((MainViewModel)window.DataContext!).Settings;
        settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        return settings;
    }

    [AvaloniaFact]
    public void Tabs_SwitchPages_AndEveryWidgetWithOptionsIsListed()
    {
        var window = UiSnapshot.ShowMainWindow();
        var settings = Open(window);

        Assert.True(Named<Control>(window, "GeneralPage").IsVisible);
        Assert.False(Named<Control>(window, "WidgetsPage").IsVisible);
        UiSnapshot.Render(window, "settings-general");

        var widgetsTab = Named<Button>(window, "WidgetsTab");
        widgetsTab.Command!.Execute(widgetsTab.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Control>(window, "GeneralPage").IsVisible);
        Assert.True(Named<Control>(window, "WidgetsPage").IsVisible);
        var titles = Named<ListBox>(window, "WidgetPageList").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);
        Assert.Equal(new[] { "Schedule", "Clock", "Weather", "Media", "Picture frame" }, titles);
        UiSnapshot.Render(window, "settings-widgets");
        window.Close();
    }

    [AvaloniaFact]
    public void EverySetting_IsReachable_AndSavesFromTheDialog()
    {
        var window = UiSnapshot.ShowMainWindow();
        var settings = Open(window);

        var radios = Named<Control>(window, "GeneralPage").GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(2, radios.Count);
        radios[0].IsChecked = false;
        radios[1].IsChecked = true;
        Assert.True(settings.UseTwelveHour);

        settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        Dispatcher.UIThread.RunJobs();
        var palettes = Named<ListBox>(window, "PaletteList");
        Assert.Equal(settings.PaletteOptions.Count, palettes.ItemCount);
        palettes.SelectedIndex = 1;
        Assert.Same(settings.PaletteOptions[1], settings.SelectedPalette);
        window.Close();
    }
}
