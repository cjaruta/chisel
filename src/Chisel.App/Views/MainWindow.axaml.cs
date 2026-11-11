using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Chisel.App.Startup;
using Chisel.App.ViewModels;

namespace Chisel.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string _windowPlacementPath = AppPaths.GetWindowPlacementPath();

    public MainWindow()
    {
        InitializeComponent();

        _timer.Tick += (_, _) => (DataContext as MainViewModel)?.Refresh();
        Loaded += (_, _) =>
        {
            _timer.Start();
            ApplySavedPlacement();
        };
        Unloaded += (_, _) => _timer.Stop();
        Closing += (_, _) => SavePlacement();

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

    private void ApplySavedPlacement()
    {
        var placement = WindowPlacementStore.Load(_windowPlacementPath);
        if (placement is null)
        {
            return;
        }

        Width = placement.Width;
        Height = placement.Height;

        if (placement.X is { } x && placement.Y is { } y)
        {
            Position = new PixelPoint((int)x, (int)y);
        }
    }

    private void SavePlacement() =>
        WindowPlacementStore.Save(_windowPlacementPath, new WindowPlacement(Width, Height, Position.X, Position.Y));
}
