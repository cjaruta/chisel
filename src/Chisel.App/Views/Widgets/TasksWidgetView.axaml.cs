using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The Tasks widget: an always-there add box, the open tasks (drag or Alt+Up/Down to reorder, F2 or double-click to edit,
// due dates from a small flyout) and a collapsible Completed section. Nothing here runs unless the user does something:
// no timers, no animations, and the drag only works while the pointer is down on a handle.
public partial class TasksWidgetView : WidgetView
{
    private TaskRowViewModel? _dragRow;
    private Border? _dragBorder;
    private int _dropIndex;

    public TasksWidgetView()
    {
        InitializeComponent();
    }

    protected override void OnHeaderShownChanged() => TasksRoot.Margin = BodyMargin(HeaderShown);

    private TaskListViewModel? Tasks => (DataContext as MainViewModel)?.Tasks;

    private static bool IsInside<T>(object? source, Visual limit) where T : Visual =>
        source is Visual v && v.FindAncestorOfType<T>(includeSelf: true) is { } found && found != limit && limit.IsVisualAncestorOf(found);

    // Rows -----------------------------------------------------------------------------------------------------------

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is TaskRowViewModel { IsDone: false } row
            && !IsInside<Button>(e.Source, border) && !IsInside<CheckBox>(e.Source, border) && !IsInside<TextBox>(e.Source, border))
        {
            row.BeginEditCommand.Execute(null);
        }
    }

    private void OnEditLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TaskRowViewModel row })
        {
            row.CommitEditCommand.Execute(null);
        }
    }

    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys typed into the in-place editor (or pressed on a button in the row) belong to that control.
        if (sender is not Border border || border.DataContext is not TaskRowViewModel row || !ReferenceEquals(e.Source, border) || Tasks is not { } tasks)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F2 when !row.IsDone:
                row.BeginEditCommand.Execute(null);
                break;
            case Key.Space:
                row.ToggleCommand.Execute(null);
                break;
            case Key.Delete:
                row.DeleteCommand.Execute(null);
                NewTaskBox.Focus();
                break;
            case Key.Up or Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                if (!row.IsDone)
                {
                    tasks.MoveBy(row, e.Key == Key.Up ? -1 : 1);
                    // The row's container may have been rebuilt by the move: put the focus back on it.
                    Dispatcher.UIThread.Post(() => RowBorder(row)?.Focus(), DispatcherPriority.Background);
                }
                break;
            case Key.Up:
                FocusNeighbour(row, -1);
                break;
            case Key.Down:
                FocusNeighbour(row, 1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnNewTaskKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && Tasks is { Open.Count: > 0 } tasks)
        {
            RowBorder(tasks.Open[0])?.Focus();
            e.Handled = true;
        }
    }

    private IEnumerable<Border> AllRowBorders()
    {
        foreach (var items in new[] { OpenItems, DoneItems })
        {
            if (!items.IsVisible || items.ItemsPanelRoot is not { } panel)
            {
                continue;
            }
            foreach (var child in panel.Children)
            {
                if (child is ContentPresenter { Child: Border border })
                {
                    yield return border;
                }
            }
        }
    }

    private Border? RowBorder(TaskRowViewModel row)
    {
        foreach (var border in AllRowBorders())
        {
            if (ReferenceEquals(border.DataContext, row))
            {
                return border;
            }
        }
        return null;
    }

    private void FocusNeighbour(TaskRowViewModel row, int step)
    {
        Border? previous = null;
        var takeNext = false;
        foreach (var border in AllRowBorders())
        {
            if (takeNext)
            {
                border.Focus();
                return;
            }
            if (ReferenceEquals(border.DataContext, row))
            {
                if (step < 0)
                {
                    (previous ?? (Control)NewTaskBox).Focus();
                    return;
                }
                takeNext = true;
            }
            previous = border;
        }
    }

    // Drag to reorder -------------------------------------------------------------------------------------------------

    private const double DragThreshold = 5;

    private Point _pressPoint;
    private bool _dragging;

    // A press on the row's own area (not its checkbox, buttons or editor) arms a drag; it only starts after the pointer
    // moves a few pixels, so plain clicks and double-clicks are untouched. Completed rows do not drag.
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TaskRowViewModel { IsDone: false } row
            || !e.GetCurrentPoint(border).Properties.IsLeftButtonPressed
            || IsInside<Button>(e.Source, border) || IsInside<CheckBox>(e.Source, border) || IsInside<TextBox>(e.Source, border))
        {
            return;
        }
        border.Focus();
        _dragRow = row;
        _dragBorder = border;
        _pressPoint = e.GetPosition(this);
        _dragging = false;
    }

    private void OnRowMoved(object? sender, PointerEventArgs e)
    {
        if (_dragRow is null || sender is not Border border || !ReferenceEquals(border, _dragBorder))
        {
            return;
        }
        if (!_dragging)
        {
            var moved = e.GetPosition(this) - _pressPoint;
            if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
            {
                return;
            }
            _dragging = true;
            border.Opacity = 0.5;
            e.Pointer.Capture(border);
        }
        UpdateDrop(e.GetPosition(OpenItems).Y);
    }

    private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        var row = _dragRow;
        var drop = _dropIndex;
        var wasDragging = _dragging;
        EndDrag();
        if (!wasDragging)
        {
            return;
        }
        e.Pointer.Capture(null);
        if (row is not null && Tasks is { } tasks)
        {
            var from = tasks.Open.IndexOf(row);
            // The drop index counts the places before the row is lifted out.
            tasks.MoveTo(row, drop > from ? drop - 1 : drop);
        }
    }

    private void OnRowCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    // Finds where the pointer's height falls among the open rows and shows the drop line there.
    private void UpdateDrop(double y)
    {
        if (OpenItems.ItemsPanelRoot is not { } panel || panel.Children.Count == 0)
        {
            return;
        }
        var index = 0;
        foreach (var child in panel.Children)
        {
            if (y > child.Bounds.Top + child.Bounds.Height / 2)
            {
                index++;
            }
        }
        _dropIndex = index;
        var line = index < panel.Children.Count ? panel.Children[index].Bounds.Top : panel.Children[^1].Bounds.Bottom;
        DropLine.Margin = new Thickness(0, Math.Max(0, line - 1), 0, 0);
        DropLine.IsVisible = true;
    }

    private void EndDrag()
    {
        if (_dragBorder is not null)
        {
            _dragBorder.Opacity = 1;
        }
        _dragRow = null;
        _dragBorder = null;
        _dragging = false;
        DropLine.IsVisible = false;
    }

    // Due date --------------------------------------------------------------------------------------------------------

    private void OnDueClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not TaskRowViewModel row || Tasks is not { } tasks)
        {
            return;
        }

        var today = tasks.Today();
        var calendar = new Calendar
        {
            SelectedDate = row.DueDate?.ToDateTime(TimeOnly.MinValue),
            DisplayDate = (row.DueDate ?? today).ToDateTime(TimeOnly.MinValue),
        };
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight };

        Button Quick(string label, DateOnly? date)
        {
            var quick = new Button { Content = label };
            quick.Click += (_, _) =>
            {
                tasks.SetDueDate(row, date);
                flyout.Hide();
            };
            return quick;
        }

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        quickRow.Children.Add(Quick("Today", today));
        quickRow.Children.Add(Quick("Tomorrow", today.AddDays(1)));
        if (row.HasDue)
        {
            quickRow.Children.Add(Quick("No date", null));
        }
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(quickRow);
        content.Children.Add(calendar);
        flyout.Content = content;

        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is { } picked)
            {
                tasks.SetDueDate(row, DateOnly.FromDateTime(picked));
                flyout.Hide();
            }
        };
        flyout.ShowAt(button);
        e.Handled = true;
    }
}
