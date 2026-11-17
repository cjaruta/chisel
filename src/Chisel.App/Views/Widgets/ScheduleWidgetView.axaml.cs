using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The schedule widget: the day grid with hover line, click-to-add, drag-to-create and its editors.
public partial class ScheduleWidgetView : WidgetView
{
    public ScheduleWidgetView()
    {
        InitializeComponent();
    }

    // Scrolls the schedule so the now-line sits about a third of the way down.
    public void ScrollToNow()
    {
        var scroller = ScheduleScroller;
        var nowLine = NowLine;
        var grid = ScheduleGrid;

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
    // A press starts tracking a drag; releasing without real movement is a plain click, which adds a marker.
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

        // The editor becomes visible on this change; focus its first box once it has been laid out.
        if (schedule.IsPending)
        {
            Dispatcher.UIThread.Post(() => BlockTitleBox.Focus(), DispatcherPriority.Background);
        }
        else if (schedule.IsMarkerPending)
        {
            Dispatcher.UIThread.Post(() => MarkerLabelBox.Focus(), DispatcherPriority.Background);
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
}
