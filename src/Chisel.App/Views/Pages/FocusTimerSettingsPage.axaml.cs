using Avalonia.Controls;
using Avalonia.Interactivity;
using Chisel.App.ViewModels;
using Chisel.Core.Focus;

namespace Chisel.App.Views.Pages;

public partial class FocusTimerSettingsPage : UserControl
{
    public FocusTimerSettingsPage()
    {
        InitializeComponent();
        foreach (var preset in FocusPresets.All)
        {
            var button = new Button { Name = "FocusPreset", Content = preset.Name, Tag = preset };
            button.Click += (_, _) => (DataContext as MainViewModel)?.FocusTimer.ApplyPreset(preset);
            FocusPresetButtons.Children.Add(button);
        }
    }

    private void OnPlayChime(object? sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.FocusTimer.PlayChime();
}
