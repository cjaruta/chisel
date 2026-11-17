using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string? _windowPlacementPath;

    public MainWindow() : this(AppPaths.GetWindowPlacementPath())
    {
    }

    // windowPlacementPath: where size/position are remembered; null turns that off (used by tests).
    public MainWindow(string? windowPlacementPath)
    {
        _windowPlacementPath = windowPlacementPath;
        InitializeComponent();

        // Keep the layout toolbar centered, but clear of the clock and date on narrow windows.
        LayoutToolbar.SizeChanged += (_, _) => PlaceLayoutToolbar();
        ClockPanel.SizeChanged += (_, _) => PlaceLayoutToolbar();
        SizeChanged += (_, _) => PlaceLayoutToolbar();

        _timer.Tick += (_, _) => (DataContext as MainViewModel)?.Refresh();
        Loaded += (_, _) =>
        {
            _timer.Start();
            ApplySavedPlacement();
            // Wait for layout so the grid has its size before scrolling to the current time.
            Dispatcher.UIThread.Post(ScrollToNow, DispatcherPriority.Background);
        };
        Unloaded += (_, _) => _timer.Stop();
        Closing += (_, _) => SavePlacement();
    }

    // Tray cards hand their pointer to the layout host, which shares the title bars' drag and drop.
    private void OnTrayCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control card || card.DataContext is not WidgetDefinition widget
            || !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }
        LayoutHost.TrayCardPressed(widget, e.GetPosition(LayoutHost), e.GetPosition(card));
        e.Pointer.Capture(card);
        e.Handled = true;
    }

    private void OnTrayCardMoved(object? sender, PointerEventArgs e) => LayoutHost.TrayCardMoved(e.GetPosition(LayoutHost));

    private void OnTrayCardReleased(object? sender, PointerReleasedEventArgs e)
    {
        LayoutHost.TrayCardReleased(e.GetPosition(LayoutHost));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnTrayCardCaptureLost(object? sender, PointerCaptureLostEventArgs e) => LayoutHost.TrayCardCaptureLost();

    // Centered over the window, or just right of the clock and date when centering would cover them.
    private void PlaceLayoutToolbar()
    {
        if (LayoutToolbar.Parent is not Control area || LayoutToolbar.Bounds.Width <= 0)
        {
            return;
        }
        var centered = (area.Bounds.Width - LayoutToolbar.Bounds.Width) / 2;
        var left = Math.Max(centered, ClockPanel.Bounds.Right + 16);
        LayoutToolbar.Margin = new Thickness(left, LayoutToolbar.Margin.Top, 0, 0);
    }

    private ScheduleWidgetView? ScheduleView => this.GetVisualDescendants().OfType<ScheduleWidgetView>().FirstOrDefault();

    // Scrolls the schedule so the now-line sits about a third of the way down.
    public void ScrollToNow() => ScheduleView?.ScrollToNow();

    public static double ScheduleScrollOffset(double nowY, double viewportHeight, double extentHeight) =>
        ScheduleWidgetView.ScheduleScrollOffset(nowY, viewportHeight, extentHeight);

    // One step of schedule auto-scroll while dragging near its top or bottom edge.
    public void AutoScrollTick() => ScheduleView?.AutoScrollTick();

    private void ApplySavedPlacement()
    {
        if (_windowPlacementPath is null)
        {
            return;
        }

        var placement = WindowPlacementStore.Load(_windowPlacementPath);
        if (placement is null)
        {
            return;
        }

        Width = placement.Width;
        Height = placement.Height;

        var workingAreas = Screens.All.Select(s => s.WorkingArea);
        if (placement.X is { } x && placement.Y is { } y && WindowPlacementPolicy.IsOnScreen(placement, workingAreas))
        {
            Position = new PixelPoint((int)x, (int)y);
        }
    }

    private void SavePlacement()
    {
        var placement = WindowPlacementPolicy.ForSave(WindowState, ClientSize.Width, ClientSize.Height, Position);
        if (placement is null || _windowPlacementPath is null)
        {
            return;
        }

        try
        {
            WindowPlacementStore.Save(_windowPlacementPath, placement);
        }
        catch (Exception)
        {
            // Remembering the window position is a convenience; never block closing over it.
        }
    }
}
