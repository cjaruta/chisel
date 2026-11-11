using Avalonia.Controls;
using Avalonia.Interactivity;
using Chisel.App.ViewModels;

namespace Chisel.App.Views;

public partial class EditScheduleWindow : Window
{
    public EditScheduleWindow()
    {
        InitializeComponent();
    }

    public EditScheduleWindow(ScheduleEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Saved += Close;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
