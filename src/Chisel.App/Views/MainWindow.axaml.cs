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

    // Scrolls the schedule so the now-line sits about a third of the way down.
    public void ScrollToNow()
    {
        var scroller = this.FindControl<ScrollViewer>("ScheduleScroller");
        var nowLine = this.FindControl<Control>("NowLine");
        var grid = this.FindControl<Control>("ScheduleGrid");
        if (scroller is null || nowLine is null || grid is null)
        {
            return;
        }

        var nowY = nowLine.TranslatePoint(new Point(0, nowLine.Bounds.Height / 2), grid)?.Y ?? 0;
        scroller.Offset = new Vector(0, ScheduleScrollOffset(nowY + grid.Margin.Top, scroller.Viewport.Height, scroller.Extent.Height));
    }

    public static double ScheduleScrollOffset(double nowY, double viewportHeight, double extentHeight)
    {
        var max = Math.Max(0, extentHeight - viewportHeight);
        return Math.Clamp(nowY - viewportHeight / 3, 0, max);
    }

    private void OnNowClick(object? sender, RoutedEventArgs e) => ScrollToNow();

    // Last pointer position over the schedule, relative to the scroller; null when outside it.
    private Point? _schedulePointer;

    private ScheduleViewModel? Schedule => (DataContext as MainViewModel)?.Schedule;

    private bool IsDragActive => Schedule?.IsDragActive == true;

    // Pointer y in the viewport, measured from where midnight would be with no scrolling.
    private double GridY(Point pointer) => pointer.Y - ScheduleGrid.Margin.Top;

    private void OnSchedulePointerMoved(object? sender, PointerEventArgs e)
    {
        _schedulePointer = e.GetPosition(ScheduleScroller);
        if (IsDragActive)
        {
            UpdateDrag();
            UpdateAutoScroll();
            return;
        }
        UpdateHover();
    }

    private void OnSchedulePointerExited(object? sender, PointerEventArgs e)
    {
        // During a drag the pointer is captured and keeps being tracked outside the grid.
        if (IsDragActive)
        {
            return;
        }
        _schedulePointer = null;
        Schedule?.HideHover();
    }

    // The grid can scroll under a still pointer (mouse wheel or auto-scroll), which changes the time under it.
    private void OnScheduleScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (IsDragActive)
        {
            UpdateDrag();
            return;
        }
        UpdateHover();
    }

    // Blocks and markers are buttons and handle their own presses, so this only sees empty space.
    // A press starts tracking a drag; releasing without real movement is a plain click.
    private void OnScheduleGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Schedule is not { } schedule || !e.GetCurrentPoint(ScheduleGrid).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _schedulePointer = e.GetPosition(ScheduleScroller);
        UpdateHover();
        schedule.BeginDrag(GridY(_schedulePointer.Value), ScheduleScroller.Offset.Y);
        e.Pointer.Capture(ScheduleGrid);
        e.Handled = true;
    }

    private void OnScheduleGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopAutoScroll();
        if (Schedule is not { } schedule || !schedule.IsDragActive)
        {
            return;
        }
        _schedulePointer = e.GetPosition(ScheduleScroller);
        UpdateDrag();
        schedule.EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (schedule.IsPending)
        {
            // The editor becomes visible on this change; focus once it has been laid out.
            Dispatcher.UIThread.Post(() => BlockTitleBox.Focus(), DispatcherPriority.Background);
        }
    }

    // Losing the pointer mid-drag (another window took it) cancels the drag.
    private void OnScheduleGridPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        StopAutoScroll();
        if (IsDragActive)
        {
            Schedule?.CancelDrag();
        }
    }

    private void UpdateDrag()
    {
        if (_schedulePointer is { } pointer)
        {
            Schedule?.UpdateDrag(GridY(pointer), ScheduleScroller.Offset.Y);
        }
    }

    private DispatcherTimer? _autoScrollTimer;

    private void UpdateAutoScroll()
    {
        if (_schedulePointer is not { } pointer || ScheduleGridLayout.AutoScrollStep(pointer.Y, ScheduleScroller.Viewport.Height) == 0)
        {
            StopAutoScroll();
            return;
        }
        if (_autoScrollTimer is null)
        {
            _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _autoScrollTimer.Tick += (_, _) => AutoScrollTick();
        }
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll() => _autoScrollTimer?.Stop();

    // One step of auto-scroll while dragging near the top or bottom; the scroll change extends the drag.
    public void AutoScrollTick()
    {
        if (!IsDragActive || _schedulePointer is not { } pointer)
        {
            StopAutoScroll();
            return;
        }
        var step = ScheduleGridLayout.AutoScrollStep(pointer.Y, ScheduleScroller.Viewport.Height);
        var max = Math.Max(0, ScheduleScroller.Extent.Height - ScheduleScroller.Viewport.Height);
        ScheduleScroller.Offset = new Vector(ScheduleScroller.Offset.X, Math.Clamp(ScheduleScroller.Offset.Y + step, 0, max));
    }

    private void UpdateHover()
    {
        if (Schedule is not { } schedule || _schedulePointer is not { } pointer)
        {
            return;
        }

        var inGrid = ScheduleScroller.TranslatePoint(pointer, ScheduleGrid);
        if (inGrid is not { } p || p.X < 0 || p.Y < 0 || p.X > ScheduleGrid.Bounds.Width || p.Y > ScheduleGrid.Bounds.Height)
        {
            schedule.HideHover();
            return;
        }

        var time = ScheduleGridLayout.SnapTime(pointer.Y - ScheduleGrid.Margin.Top, ScheduleScroller.Offset.Y);
        schedule.HoverAt(time, overItem: IsOverGridItem(ScheduleGrid.InputHitTest(p)));
    }

    private static bool IsOverGridItem(IInputElement? hit) =>
        hit is Visual visual && visual.GetSelfAndVisualAncestors().OfType<Button>().Any(b => b.Classes.Contains("gridItem"));

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
