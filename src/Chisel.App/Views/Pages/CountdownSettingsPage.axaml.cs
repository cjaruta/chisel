using Avalonia.Controls;
using Avalonia.Interactivity;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Pages;

public partial class CountdownSettingsPage : UserControl
{
    public CountdownSettingsPage()
    {
        InitializeComponent();
    }

    private void OnDeleteArchived(object? sender, RoutedEventArgs e) => (DataContext as MainViewModel)?.Countdown.DeleteArchived();
}
