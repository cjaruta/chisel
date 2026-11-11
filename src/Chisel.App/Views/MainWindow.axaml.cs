using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Chisel.App.ViewModels;

namespace Chisel.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();

        _timer.Tick += (_, _) => (DataContext as MainViewModel)?.Refresh();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ScheduleEditorRequested += editor =>
                {
                    var dialog = new EditScheduleWindow(editor);
                    dialog.ShowDialog(this);
                };
            }
        };
    }
}
