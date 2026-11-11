using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chisel.App.Views;

public partial class ErrorWindow : Window
{
    public ErrorWindow()
    {
        InitializeComponent();
    }

    public ErrorWindow(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
