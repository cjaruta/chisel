using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

public class FirstRunHeadlessTests
{
    private static Control Named(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    [AvaloniaFact]
    public void AFreshInstall_ShowsTheStarterDashboardAndTheWelcome_UntilDismissed()
    {
        var window = UiSnapshot.ShowMainWindow(firstRun: true);
        UiSnapshot.Render(window, "first-run");

        Assert.True(Named(window, "WelcomeHint").IsVisible);
        ((Button)Named(window, "WelcomeDismiss")).Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named(window, "WelcomeHint").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void HelpInSettings_ShowsTheWelcomeAgain()
    {
        var window = UiSnapshot.ShowMainWindow();
        var settings = ((MainViewModel)window.DataContext!).Settings;
        Assert.False(Named(window, "WelcomeHint").IsVisible);

        settings.IsOpen = true;
        settings.SelectTabCommand.Execute(SettingsTab.Help);
        settings.ShowWelcomeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named(window, "WelcomeHint").IsVisible);
        Assert.False(settings.IsOpen);
        window.Close();
    }

    [AvaloniaFact]
    public void HelpInSettings_ShowsTheVersionAndDiagnosticsButtons()
    {
        var window = UiSnapshot.ShowMainWindow();
        var settings = ((MainViewModel)window.DataContext!).Settings;
        settings.IsOpen = true;
        settings.SelectTabCommand.Execute(SettingsTab.Help);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "help-about");

        Assert.True(Named(window, "AboutSection").IsVisible);
        Assert.Equal("Chisel 1.2.3", ((TextBlock)Named(window, "AboutVersion")).Text);
        Assert.True(Named(window, "CopyDiagnosticsButton").IsEffectivelyVisible);
        window.Close();
    }
}
