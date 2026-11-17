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
using Avalonia.Media.Imaging;
using Chisel.App.Controls;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Views;

// Builds the window's columns and widget cards from a LayoutViewModel, and rebuilds them whenever its
// layout changes. Each widget's card (and so its view and state) is created once and reused.
// Each column shares its height between its cards by weight. If the window is too short for the cards'
// minimum heights, that column scrolls instead of cutting cards off.
//
// In edit mode a card can be dragged: past a small threshold the widget is picked up. A floating snapshot of
// it follows the pointer and its slot becomes an empty gap. The target (column and gap) comes from the card
// positions captured at pick-up, so holding still never flips it; the other cards reflow around the gap live
// (the layout the drop would give, see LayoutViewModel.PreviewDrop). Releasing drops the widget into the gap;
// Esc, or releasing outside any column, puts everything back. A column the drop empties then collapses.
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
    private readonly Border _float = new() { Name = "DragFloat", IsVisible = false };
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
    private IPointer? _pointer;

    // Where the columns and cards were at pick-up: targets are worked out from these, however the cards reflow.
    private IReadOnlyList<Rect> _pickColumns = [];
    private IReadOnlyList<IReadOnlyList<Rect>> _pickWidgets = [];

    public LayoutHost()
    {
        _float.Classes.Add("dragFloat");

        var labelText = new TextBlock { Text = "New column" };
        _newColumnLabel = new Border { Name = "NewColumnLabel", IsVisible = false, Child = labelText };
        _newColumnLabel.Classes.Add("newColumnLabel");

        _overlay.Children.Add(_newColumnLabel);
        _overlay.Children.Add(_float);
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

    private static string Structure(IReadOnlyList<LayoutColumnSlot> columns) =>
        string.Join("|", columns.Select(c => string.Join(",", c.Widgets.Select(w => w.Id))));

    private string Structure() => Layout is null ? "" : Structure(Layout.Columns);

    // Same widgets in the same places: just share the space out by the new weights, so nothing is rebuilt
    // (a live resize keeps hold of its handle).
    private void UpdateWeights()
    {
        var scrollers = ColumnScrollers;
        var columns = Layout!.Columns;
        for (var c = 0; c < scrollers.Count && c < columns.Count; c++)
        {
            var column = columns[c];
            WeightedStackPanel.SetWeight(scrollers[c], column.WidthWeight);
            foreach (var slot in column.Widgets)
            {
                WeightedStackPanel.SetWeight(_frames[slot.Id], slot.HeightWeight);
            }
        }
    }

    private bool _isEditing;

    private void Rebuild() => Rebuild(Layout?.Columns);

    // Builds the columns from the given slots: the layout itself, or the preview of a drop while dragging.
    private void Rebuild(IReadOnlyList<LayoutColumnSlot>? slots)
    {
        // Take the reused cards out of their old columns first, so each can join its new one.
        foreach (var column in ColumnPanels)
        {
            column.Children.Clear();
        }
        _columns.Children.Clear();
        _handles.Clear();
        if (slots is null)
        {
            _builtStructure = "";
            return;
        }
        _builtStructure = Structure(slots);

        for (var c = 0; c < slots.Count; c++)
        {
            var column = slots[c];
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
        MarkDragSource();
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
            StartDragVisuals(null, widget.Title);
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
        // The host holds the pointer: the cards are rebuilt during a drag, which would drop a capture held by one.
        _pointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        OnBarMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed is not null)
        {
            OnBarReleased(e);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        OnBarCaptureLost();
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
            StartDragVisuals(frame, frame.Widget.Title);
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
    private void StartDragVisuals(WidgetFrame? frame, string title)
    {
        _dragging = true;
        _pickColumns = ColumnScrollers.Select(BoundsInHost).ToList();
        _pickWidgets = Frames.Select(c => (IReadOnlyList<Rect>)c.Select(BoundsInHost).ToList()).ToList();
        ShowNewColumnZones(frame?.WidgetId);

        if (frame is not null)
        {
            _float.Width = frame.Bounds.Width;
            _float.Height = frame.Bounds.Height;
            _float.Child = Snapshot(frame);
        }
        else
        {
            var heading = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
            heading.Classes.Add("cardHeading");
            var card = new Border { Child = heading, Padding = new Thickness(16, 0) };
            card.Classes.Add("card");
            _float.Width = 260;
            _float.Height = 64;
            _float.Child = card;
        }
        _grabOffset = new Point(Math.Min(_grabOffset.X, _float.Width - 24), Math.Min(_grabOffset.Y, _float.Height - 8));
        // Slightly larger, scaled around the grab point so that point stays under the pointer.
        _float.RenderTransformOrigin = new RelativePoint(_grabOffset, RelativeUnit.Absolute);
        _float.RenderTransform = new ScaleTransform(1.03, 1.03);
        _float.IsVisible = true;
        Cursor = DraggingCursor;
        MarkDragSource();
    }

    // The widget as a bitmap, taken before its slot turns into a gap.
    private Image Snapshot(WidgetFrame frame)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(frame.Bounds.Width * scale)), Math.Max(1, (int)Math.Ceiling(frame.Bounds.Height * scale)));
        var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(frame);
        return new Image { Source = bitmap, Stretch = Stretch.Fill };
    }

    // The widget being dragged shows as an empty gap wherever it currently sits in the columns.
    private void MarkDragSource()
    {
        var id = _dragging ? Layout?.DraggedWidgetId : null;
        foreach (var (widgetId, frame) in _frames)
        {
            frame.Classes.Set("dragSource", widgetId == id);
        }
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

    // Moves the floating widget with the pointer and reflows the cards around the gap it would land in.
    private void UpdateDrag(Point point)
    {
        Canvas.SetLeft(_float, point.X - _grabOffset.X);
        Canvas.SetTop(_float, point.Y - _grabOffset.Y);

        // A "New column" zone under the pointer wins over dropping into a column.
        _newColumnTarget = null;
        _newColumnLabel.IsVisible = false;
        foreach (var (_, zone) in _newColumnZones)
        {
            zone.Classes.Set("active", false);
        }
        var gapIndex = LayoutDropTargets.FindNewColumn(point, _pickColumns, Gap, NewColumnReach);
        if (gapIndex is { } g && _newColumnZones.FirstOrDefault(z => z.Index == g).Zone is { } hovered)
        {
            _newColumnTarget = g;
            _target = null;
            hovered.Classes.Set("active", true);
            ShowNewColumnLabel(Canvas.GetLeft(hovered) + 3, Canvas.GetTop(hovered));
        }
        else
        {
            _target = LayoutDropTargets.Find(point, _pickColumns, _pickWidgets, Gap);
        }
        ShowPreview();
    }

    // Builds the columns as the drop on the current target would leave them (the layout as it is when there is
    // none), unless that is what is already shown.
    private void ShowPreview()
    {
        if (Layout is not { } layout)
        {
            return;
        }
        var preview = _target is { } t ? layout.PreviewDrop(t.Column, t.Index) : null;
        var slots = preview is null ? layout.Columns : LayoutViewModel.Slots(preview);
        if (Structure(slots) != _builtStructure)
        {
            Rebuild(slots);
        }
    }

    private void EndDrag()
    {
        _pressed = null;
        _trayWidget = null;
        _dragging = false;
        _target = null;
        _float.IsVisible = false;
        _float.Child = null;
        Cursor = null;
        ClearNewColumnZones();
        // Put the columns back as the layout has them (after a drop, the layout already has the new order).
        if (Layout is not null)
        {
            if (Structure() != _builtStructure)
            {
                Rebuild();
            }
            else
            {
                UpdateWeights();
            }
        }
        MarkDragSource();
        if (_pointer is { } pointer)
        {
            _pointer = null;
            if (pointer.Captured == this)
            {
                pointer.Capture(null);
            }
        }
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
