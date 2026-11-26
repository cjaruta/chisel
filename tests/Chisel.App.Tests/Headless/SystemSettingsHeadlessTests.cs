using Avalonia.Controls;
using Avalonia.Input;
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
        window.Close();
    }

    [AvaloniaFact]
    public void TheShortcutBox_CapturesTheNextShortcut_AndExplainsABadOne()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.SelectedTab = SettingsTab.General;
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var system = main.Settings.SystemOptions!;
        var box = window.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "HotkeyBox");
        Assert.Equal("Ctrl+Alt+C", box.Text);

        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Press the new shortcut…", box.Text);
        Press(box, Key.LeftCtrl, KeyModifiers.Control);
        Assert.Null(system.HotkeyError);
        Press(box, Key.K, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.False(box.IsFocused);
        Assert.Equal("Ctrl+Alt+K", box.Text);

        box.Focus();
        Press(box, Key.K, KeyModifiers.Shift);
        Assert.True(box.IsFocused);
        var error = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HotkeyError");
        Assert.True(error.IsVisible);
        error.BringIntoView();
        Dispatcher.UIThread.RunJobs();

        UiSnapshot.Render(window, "settings-system");
        window.Close();
    }

    private static void Press(TextBox box, Key key, KeyModifiers modifiers)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        Dispatcher.UIThread.RunJobs();
    }
}
