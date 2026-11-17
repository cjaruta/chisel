using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.Controls;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Views;

// Builds the window's columns and widget cards from a LayoutViewModel, and rebuilds them whenever its
// layout changes. Each widget's card (and so its view and state) is created once and reused.
// Each column shares its height between its cards by weight. If the window is too short for the cards'
// minimum heights, that column scrolls instead of cutting cards off.
//
// In edit mode a card's title bar can be dragged: past a small threshold the widget is picked up,
// a ghost follows the pointer, an accent line shows where it will land, and releasing drops it there.
// Nothing else moves until the drop; a column the drop empties then collapses.
//
// While dragging, thin "New column" zones also show at the left and right edges and between columns
// (when another column fits: see LayoutViewModel.CanOfferNewColumn); dropping on one makes a new column.
//
// Between stacked cards sits a resize handle in the gap (always there, so showing it moves nothing; usable
// only in edit mode). Dragging it moves height between the two neighbours live; release keeps it.
public sealed class LayoutHost : Decorator
{
    public const double Gap = 16;

    // How far the pointer must move before a press on a title bar becomes a drag.
    public const double DragThreshold = 4;

    // How close (horizontally) the pointer must be to a "New column" zone's center to target it.
    public const double NewColumnReach = 14;

    public static readonly StyledProperty<LayoutViewModel?> LayoutProperty =
        AvaloniaProperty.Register<LayoutHost, LayoutViewModel?>(nameof(Layout));

    private static readonly Cursor GrabCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor DraggingCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeNorthSouth);

    private readonly Dictionary<string, WidgetFrame> _frames = new();
    private readonly WeightedStackPanel _columns = new() { Orientation = Orientation.Horizontal, Spacing = Gap };
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _indicator = new() { Name = "DropIndicator", IsVisible = false };
    private readonly Border _ghost;
    private readonly TextBlock _ghostTitle = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<Panel> _handles = new();
    private readonly List<(int Index, Border Zone)> _newColumnZones = new();
    private readonly Border _newColumnLabel;
    private int? _newColumnTarget;

    // The columns' widget ids as last built; a layout with the same ones only needs new weights.
    private string _builtStructure = "";

    // The handle being dragged: the widget above it, the pointer's starting height, and the column's
    // height available to its widgets (weights share this out).
    private Panel? _resizeHandle;
    private string? _resizeWidget;
    private double _resizeStartY;
    private double _resizeSpace;

    // The title bar press (or tray card press) that may turn into a drag.
    private WidgetFrame? _pressed;
    private WidgetDefinition? _trayWidget;
    private Point _pressPoint;
    private Point _grabOffset;
    private bool _dragging;
    private (int Column, int Index)? _target;

    public LayoutHost()
    {
        _indicator.Classes.Add("dropIndicator");

        _ghostTitle.Classes.Add("editTitle");
        var grip = new Path { Data = Geometry.Parse(WidgetFrame.GripData), VerticalAlignment = VerticalAlignment.Center };
        grip.Classes.Add("editGrip");
        var ghostContent = new StackPanel { Orientation = Orientation.Horizontal, Children = { grip, _ghostTitle } };
        ghostContent.Classes.Add("editBarContent");
        _ghost = new Border { Name = "DragGhost", IsVisible = false, Child = ghostContent };
        _ghost.Classes.Add("dragGhost");

        var labelText = new TextBlock { Text = "New column" };
        _newColumnLabel = new Border { Name = "NewColumnLabel", IsVisible = false, Child = labelText };
        _newColumnLabel.Classes.Add("newColumnLabel");

        _overlay.Children.Add(_indicator);
        _overlay.Children.Add(_newColumnLabel);
        _overlay.Children.Add(_ghost);
        Child = new Panel { Children = { _columns, _overlay } };
    }

    public LayoutViewModel? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    // The cards currently shown, by column.
    public IReadOnlyList<IReadOnlyList<WidgetFrame>> Frames =>
        ColumnPanels.Select(c => (IReadOnlyList<WidgetFrame>)c.Children.OfType<WidgetFrame>().ToList()).ToList();

    private IReadOnlyList<ScrollViewer> ColumnScrollers => _columns.Children.OfType<ScrollViewer>().ToList();

    private IEnumerable<WeightedStackPanel> ColumnPanels =>
        ColumnScrollers.Select(s => s.Content).OfType<WeightedStackPanel>();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
        {
            if (change.OldValue is LayoutViewModel old)
            {
                old.PropertyChanged -= OnLayoutChanged;
            }
            IsEditing = change.NewValue is LayoutViewModel { IsEditing: true };
            if (change.NewValue is LayoutViewModel layout)
            {
                layout.PropertyChanged += OnLayoutChanged;
            }
            Rebuild();
        }
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LayoutViewModel.Columns):
                if (Structure() == _builtStructure)
                {
                    UpdateWeights();
                }
                else
                {
                    Rebuild();
                }
                break;
            case nameof(LayoutViewModel.IsResizing) when Layout?.IsResizing != true:
                // Committed, or cancelled (Esc): stop following the pointer.
                EndResize();
                break;
            case nameof(LayoutViewModel.IsEditing):
                IsEditing = Layout?.IsEditing == true;
                if (!IsEditing)
                {
                    EndDrag();
                }
                break;
            case nameof(LayoutViewModel.CanRemoveWidgets):
                UpdateRemoveButtons();
                break;
            case nameof(LayoutViewModel.DraggedWidgetId) when Layout?.IsDraggingWidget != true:
                // Dropped, or cancelled (Esc): clear the drag visuals.
                EndDrag();
                break;
        }
    }

    // Puts every card (and the resize handles) in or out of the editing look.
    private bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            foreach (var frame in _frames.Values)
            {
                frame.IsEditing = value;
            }
            foreach (var handle in _handles)
            {
                SetHandleEditing(handle, value);
            }
        }
    }

    private string Structure() =>
        Layout is null ? "" : string.Join("|", Layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.Id))));

    // Same widgets in the same places: just share the space out by the new weights, so nothing is rebuilt
    // (a live resize keeps hold of its handle).
    private void UpdateWeights()
    {
        var scrollers = ColumnScrollers;
        for (var c = 0; c < scrollers.Count && c < Layout!.Columns.Count; c++)
        {
            var column = Layout.Columns[c];
            WeightedStackPanel.SetWeight(scrollers[c], column.WidthWeight);
            foreach (var slot in column.Widgets)
            {
                WeightedStackPanel.SetWeight(_frames[slot.Id], slot.HeightWeight);
            }
        }
    }

    private bool _isEditing;

    private void Rebuild()
    {
        // Take the reused cards out of their old columns first, so each can join its new one.
        foreach (var column in ColumnPanels)
        {
            column.Children.Clear();
        }
        _columns.Children.Clear();
        _handles.Clear();
        _builtStructure = Structure();

        if (Layout is null)
        {
            return;
        }

        for (var c = 0; c < Layout.Columns.Count; c++)
        {
            var column = Layout.Columns[c];
            // Cards with a resize handle filling each gap between them (weight 0, so each gets exactly Gap).
            var panel = new WeightedStackPanel { Orientation = Orientation.Vertical };
            panel.Classes.Add("layoutColumn");
            for (var i = 0; i < column.Widgets.Count; i++)
            {
                var slot = column.Widgets[i];
                var frame = Frame(slot.Widget);
                WeightedStackPanel.SetWeight(frame, slot.HeightWeight);
                panel.Children.Add(frame);
                if (i + 1 < column.Widgets.Count)
                {
                    panel.Children.Add(ResizeHandle(slot.Id, panel));
                }
            }

            var minimum = Math.Max(0, column.Widgets.Sum(w => w.MinHeight) + Gap * (column.Widgets.Count - 1));
            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = panel,
            };
            scroller.Classes.Add("layoutColumnScroller");
            // A scroller measures its content with unlimited height, so give the column a real height:
            // the visible height, or the cards' minimums when those don't fit.
            panel.Height = minimum;
            scroller.SizeChanged += (_, e) => panel.Height = Math.Max(e.NewSize.Height, minimum);
            WeightedStackPanel.SetWeight(scroller, column.WidthWeight);
            _columns.Children.Add(scroller);
        }
    }

    // A slim bar in the gap below a card: an 8px hit area with a short grip mark in the middle.
    private Panel ResizeHandle(string aboveId, WeightedStackPanel column)
    {
        var line = new Border();
        line.Classes.Add("resizeLine");
        var grip = new Border();
        grip.Classes.Add("resizeGrip");
        var hit = new Border { Background = Brushes.Transparent, Cursor = ResizeCursor };
        hit.Classes.Add("resizeHit");

        var handle = new Panel { Name = $"ResizeHandle_{aboveId}", MinHeight = Gap, Children = { line, grip, hit } };
        handle.Classes.Add("resizeHandle");
        WeightedStackPanel.SetWeight(handle, 0);
        SetHandleEditing(handle, _isEditing);

        hit.PointerPressed += (_, e) => OnHandlePressed(handle, aboveId, column, e);
        hit.PointerMoved += (_, e) => OnHandleMoved(e);
        hit.PointerReleased += (_, e) => OnHandleReleased(e);
        hit.PointerCaptureLost += (_, _) => OnHandleCaptureLost();
        _handles.Add(handle);
        return handle;
    }

    private static void SetHandleEditing(Panel handle, bool editing)
    {
        handle.IsHitTestVisible = editing;
        handle.Classes.Set("editing", editing);
    }

    private void OnHandlePressed(Panel handle, string aboveId, WeightedStackPanel column, PointerPressedEventArgs e)
    {
        if (!IsEditing || Layout is not { } layout || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            return;
        }
        layout.BeginResize();
        if (!layout.IsResizing)
        {
            return;
        }
        _resizeHandle = handle;
        _resizeWidget = aboveId;
        _resizeStartY = e.GetPosition(this).Y;
        _resizeSpace = column.Children.OfType<WidgetFrame>().Sum(f => f.Bounds.Height);
        handle.Classes.Add("active");
        e.Pointer.Capture((IInputElement?)e.Source);
        e.Handled = true;
    }

    private void OnHandleMoved(PointerEventArgs e)
    {
        if (_resizeWidget is not { } id || Layout is not { IsResizing: true } layout || _resizeSpace <= 0)
        {
            return;
        }
        var dy = e.GetPosition(this).Y - _resizeStartY;
        layout.PreviewResize(id, dy / _resizeSpace, WidgetRegistry.MinimumHeight / _resizeSpace);
    }

    private void OnHandleReleased(PointerReleasedEventArgs e)
    {
        if (_resizeWidget is not null)
        {
            OnHandleMoved(e);
            Layout?.CommitResize();
        }
        EndResize();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnHandleCaptureLost()
    {
        if (_resizeWidget is not null)
        {
            Layout?.CancelResize();
        }
        EndResize();
    }

    private void EndResize()
    {
        _resizeHandle?.Classes.Remove("active");
        _resizeHandle = null;
        _resizeWidget = null;
    }

    private WidgetFrame Frame(WidgetDefinition widget)
    {
        if (!_frames.TryGetValue(widget.Id, out var frame))
        {
            var view = (WidgetView)Activator.CreateInstance(widget.ViewType)!;
            frame = new WidgetFrame(widget, view) { IsEditing = _isEditing };
            frame.EditBar.Cursor = GrabCursor;
            frame.EditBar.PointerPressed += (_, e) => OnBarPressed(frame, e);
            frame.EditBar.PointerMoved += (_, e) => OnBarMoved(e);
            frame.EditBar.PointerReleased += (_, e) => OnBarReleased(e);
            frame.EditBar.PointerCaptureLost += (_, _) => OnBarCaptureLost();
            frame.RemoveButton.Click += (_, _) => Layout?.RemoveWidget(frame.WidgetId);
            frame.RemoveButton.IsEnabled = Layout?.CanRemoveWidgets ?? true;
            _frames[widget.Id] = frame;
        }
        return frame;
    }

    // The last widget on the dashboard cannot be removed.
    private void UpdateRemoveButtons()
    {
        foreach (var frame in _frames.Values)
        {
            frame.RemoveButton.IsEnabled = Layout?.CanRemoveWidgets ?? true;
        }
    }

    // Tray cards (in the edit toolbar) hand their pointer over here, in this host's coordinates. A press that
    // moves past the threshold drags the widget onto the dashboard like a title bar; one that doesn't is a click,
    // which adds the widget at the bottom of the first column.
    public void TrayCardPressed(WidgetDefinition widget, Point point, Point grabOffset)
    {
        if (!IsEditing)
        {
            return;
        }
        _trayWidget = widget;
        _pressPoint = point;
        _grabOffset = grabOffset;
    }

    public void TrayCardMoved(Point point)
    {
        if (_trayWidget is not { } widget || Layout is not { } layout)
        {
            return;
        }
        if (!_dragging)
        {
            var moved = point - _pressPoint;
            if (Math.Sqrt(moved.X * moved.X + moved.Y * moved.Y) < DragThreshold)
            {
                return;
            }
            layout.BeginTrayDrag(widget.Id);
            if (!layout.IsDraggingWidget)
            {
                _trayWidget = null;
                return;
            }
            StartDragVisuals(null, widget.Title, 260);
        }
        if (layout.IsDraggingWidget)
        {
            UpdateDrag(point);
        }
    }

    public void TrayCardReleased(Point point)
    {
        if (_trayWidget is { } widget && Layout is { } layout)
        {
            if (_dragging && layout.IsDraggingWidget)
            {
                UpdateDrag(point);
                Drop(layout);
            }
            else if (!_dragging)
            {
                layout.AddWidgetFromTray(widget.Id);
            }
        }
        EndDrag();
    }

    public void TrayCardCaptureLost()
    {
        if (_dragging)
        {
            Layout?.CancelWidgetDrag();
        }
        EndDrag();
    }

    // Drops the dragged widget on the current target, or puts it back when there is none.
    private void Drop(LayoutViewModel layout)
    {
        if (_newColumnTarget is { } newColumn)
        {
            layout.DropWidgetInNewColumn(newColumn);
        }
        else if (_target is { } target)
        {
            layout.DropWidget(target.Column, target.Index);
        }
        else
        {
            layout.CancelWidgetDrag();
        }
    }

    private void OnBarPressed(WidgetFrame frame, PointerPressedEventArgs e)
    {
        if (!IsEditing || !e.GetCurrentPoint(frame.EditBar).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = frame;
        _pressPoint = e.GetPosition(this);
        _grabOffset = e.GetPosition(frame.EditBar);
        e.Pointer.Capture(frame.EditBar);
        e.Handled = true;
    }

    private void OnBarMoved(PointerEventArgs e)
    {
        if (_pressed is not { } frame || Layout is not { } layout)
        {
            return;
        }
        var point = e.GetPosition(this);

        if (!_dragging)
        {
            var moved = point - _pressPoint;
            if (Math.Sqrt(moved.X * moved.X + moved.Y * moved.Y) < DragThreshold)
            {
                return;
            }
            layout.BeginWidgetDrag(frame.WidgetId);
            if (!layout.IsDraggingWidget)
            {
                _pressed = null;
                return;
            }
            StartDragVisuals(frame, frame.Widget.Title, Math.Min(frame.Bounds.Width, 360));
        }

        if (layout.IsDraggingWidget)
        {
            UpdateDrag(point);
        }
    }

    private void OnBarReleased(PointerReleasedEventArgs e)
    {
        var layout = Layout;
        if (_dragging && layout is { IsDraggingWidget: true })
        {
            UpdateDrag(e.GetPosition(this));
            Drop(layout);
        }
        EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    // Another window took the pointer mid-drag: put the widget back.
    private void OnBarCaptureLost()
    {
        if (_dragging)
        {
            Layout?.CancelWidgetDrag();
        }
        EndDrag();
    }

    // frame: the card being moved, or null for a widget coming from the tray.
    private void StartDragVisuals(WidgetFrame? frame, string title, double ghostWidth)
    {
        _dragging = true;
        ShowNewColumnZones(frame?.WidgetId);
        if (frame is not null)
        {
            frame.Classes.Add("dragSource");
            frame.EditBar.Cursor = DraggingCursor;
        }
        _ghostTitle.Text = title;
        _ghost.Width = ghostWidth;
        _grabOffset = new Point(Math.Min(_grabOffset.X, _ghost.Width - 24), _grabOffset.Y);
        _ghost.IsVisible = true;
    }

    // A thin zone in each gap where a new column could go. A widget alone in its column skips the two gaps
    // beside its own column (it would land where it is).
    private void ShowNewColumnZones(string? placedWidgetId)
    {
        ClearNewColumnZones();
        if (Layout is not { } layout || !layout.CanOfferNewColumn(Bounds.Width, Gap))
        {
            return;
        }

        var columns = ColumnScrollers.Select(BoundsInHost).ToList();
        var centers = LayoutDropTargets.NewColumnCenters(columns, Gap);
        var at = placedWidgetId is null ? null : layout.Layout.Find(placedWidgetId);
        var alone = at is { } a && layout.Layout.Columns[a.Column].Widgets.Count == 1;
        var top = columns.Min(c => c.Top);
        var height = columns.Max(c => c.Bottom) - top;
        for (var i = 0; i < centers.Length; i++)
        {
            if (alone && (i == at!.Value.Column || i == at.Value.Column + 1))
            {
                continue;
            }
            var zone = new Border { Name = $"NewColumnZone{i}", Height = height };
            zone.Classes.Add("newColumnZone");
            Canvas.SetLeft(zone, centers[i] - 3);
            Canvas.SetTop(zone, top);
            _overlay.Children.Insert(_newColumnZones.Count, zone); // behind the label and ghost, in gap order
            _newColumnZones.Add((i, zone));
        }
    }

    private void ClearNewColumnZones()
    {
        foreach (var (_, zone) in _newColumnZones)
        {
            _overlay.Children.Remove(zone);
        }
        _newColumnZones.Clear();
        _newColumnTarget = null;
        _newColumnLabel.IsVisible = false;
    }

    // Moves the ghost with the pointer and shows where the widget would land.
    private void UpdateDrag(Point point)
    {
        Canvas.SetLeft(_ghost, point.X - _grabOffset.X);
        Canvas.SetTop(_ghost, point.Y - _grabOffset.Y);

        var scrollers = ColumnScrollers;
        var frames = Frames;
        var columnRects = scrollers.Select(BoundsInHost).ToList();

        // A "New column" zone under the pointer wins over dropping into a column.
        _newColumnTarget = null;
        _newColumnLabel.IsVisible = false;
        foreach (var (_, zone) in _newColumnZones)
        {
            zone.Classes.Set("active", false);
        }
        var gapIndex = LayoutDropTargets.FindNewColumn(point, columnRects, Gap, NewColumnReach);
        if (gapIndex is { } g && _newColumnZones.FirstOrDefault(z => z.Index == g).Zone is { } hovered)
        {
            _newColumnTarget = g;
            _target = null;
            _indicator.IsVisible = false;
            hovered.Classes.Set("active", true);
            ShowNewColumnLabel(Canvas.GetLeft(hovered) + 3, Canvas.GetTop(hovered));
            return;
        }
        var widgetRects = frames.Select(c => (IReadOnlyList<Rect>)c.Select(BoundsInHost).ToList()).ToList();
        var target = LayoutDropTargets.Find(point, columnRects, widgetRects, Gap);

        // Dropping where it already is would change nothing, so that is not shown as a target.
        if (target is { } t && Layout is { DraggedWidgetId: { } id, IsDraggingFromTray: false } layout
            && ReferenceEquals(layout.Layout.Move(id, t.Column, t.Index), layout.Layout))
        {
            target = null;
        }
        _target = target;

        _indicator.IsVisible = false;
        if (target is not { } drop || widgetRects[drop.Column].Count == 0)
        {
            return;
        }

        var column = columnRects[drop.Column];
        var cards = widgetRects[drop.Column];

        // The line sits in the middle of the gap the widget would land in (just above the first card,
        // just below the last).
        var y = drop.Index == 0 ? cards[0].Top - Gap / 2
            : drop.Index == cards.Count ? cards[^1].Bottom + Gap / 2
            : (cards[drop.Index - 1].Bottom + cards[drop.Index].Top) / 2;
        _indicator.Width = column.Width;
        Canvas.SetLeft(_indicator, column.Left);
        Canvas.SetTop(_indicator, y - _indicator.Height / 2);
        _indicator.IsVisible = true;
    }

    private void EndDrag()
    {
        if (_pressed is { } frame)
        {
            frame.Classes.Remove("dragSource");
            frame.EditBar.Cursor = GrabCursor;
        }
        foreach (var f in _frames.Values)
        {
            f.Classes.Remove("dragSource");
        }
        _pressed = null;
        _trayWidget = null;
        _dragging = false;
        _target = null;
        _indicator.IsVisible = false;
        _ghost.IsVisible = false;
        ClearNewColumnZones();
    }

    // The "New column" pill near the top of a highlighted zone, kept inside the dashboard.
    private void ShowNewColumnLabel(double centerX, double top)
    {
        _newColumnLabel.IsVisible = true;
        _newColumnLabel.Measure(Size.Infinity);
        var width = _newColumnLabel.DesiredSize.Width;
        Canvas.SetLeft(_newColumnLabel, Math.Clamp(centerX - width / 2, 0, Math.Max(0, Bounds.Width - width)));
        Canvas.SetTop(_newColumnLabel, top + 12);
    }

    private Rect BoundsInHost(Visual visual) =>
        new(visual.TranslatePoint(new Point(0, 0), this) ?? default, visual.Bounds.Size);
}
