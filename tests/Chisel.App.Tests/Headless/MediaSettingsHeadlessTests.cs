using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Tests.Headless;

public class MediaSettingsHeadlessTests
{
    [AvaloniaFact]
    public void TheMediaPage_ListsTheVisualizerToggle_AndItDrivesTheSetting()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "media");
        Dispatcher.UIThread.RunJobs();

        var toggle = window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "MediaVisualizerToggle");
        Assert.True(toggle.IsChecked);

        toggle.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(main.MediaSettings.ShowVisualizer);
    }
}
