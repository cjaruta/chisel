using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Tests.Headless;

// Settings > General > System: what closing the window does and how Chisel starts.
public class SystemSettingsHeadlessTests
{
    [AvaloniaFact]
    public void TheSystemSection_ChoosesWhatClosingDoes()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.SelectedTab = SettingsTab.General;
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var section = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "SystemSection");
        section.BringIntoView();
        Dispatcher.UIThread.RunJobs();

        var quit = window.GetVisualDescendants().OfType<RadioButton>().Single(r => r.Name == "CloseQuitsOption");
        var tray = window.GetVisualDescendants().OfType<RadioButton>().Single(r => r.Name == "CloseToTrayOption");
        Assert.True(quit.IsChecked);
        tray.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(main.Settings.SystemOptions!.CloseToTray);
        Assert.False(quit.IsChecked);

        UiSnapshot.Render(window, "settings-system");
        window.Close();
    }
}
