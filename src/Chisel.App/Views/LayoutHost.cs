using Chisel.App.Theming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Chisel.App.Controls;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;

namespace Chisel.App.Views;

// Builds the window's columns and widget cards from a LayoutViewModel, and rebuilds them whenever its
// layout changes. Each widget's card (and so its view and state) is created once and reused.
// Each column shares its height between its cards by weight. If the window is too short for the cards'
// minimum heights, that column scrolls instead of cutting cards off.
//
// In edit mode a card can be dragged: past a small threshold the widget is picked up. A floating snapshot of
// it follows the pointer and its slot becomes an empty gap. The board itself never moves during the drag: the
// target (column and gap) comes from the card positions captured at pick-up, and an amber line marks the gap in
// the hovered column. Releasing drops the widget there; Esc, or releasing outside any column, puts everything
// back. A column the drop empties then collapses.
//
// While dragging, the outer edges of the dashboard and a strip on each divider between columns are
// "new column" zones (when another column fits: see LayoutViewModel.CanOfferNewColumn). Entering one shows a
// translucent phantom column where it would land; dropping there makes a new column.
//
// Between stacked cards sits a resize handle in the gap (always there, so showing it moves nothing; usable
// only in edit mode). Dragging it moves height between the two neighbours live; release keeps it.
// Likewise a slim vertical handle floats over each gap between columns (a layer above the columns, so it
// never shifts anything) and moves width between the two neighbours; Esc puts the starting widths back.
public sealed partial class LayoutHost : Decorator
{
    public const double Gap = 16;

    // How far the pointer must move before a press on a title bar becomes a drag.
    public const double DragThreshold = 4;

    // The share of the dashboard's width, at each outer edge, that counts as a new-column zone.
    public const double EdgeZoneFraction = 0.15;

    // The width of the new-column zone centered on each divider between columns.
    public const double DividerZoneWidth = 24;

    public static readonly StyledProperty<LayoutViewModel?> LayoutProperty =
        AvaloniaProperty.Register<LayoutHost, LayoutViewModel?>(nameof(Layout));

    private static readonly Cursor GrabCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor DraggingCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor ColumnResizeCursor = new(StandardCursorType.SizeWestEast);

    private readonly Dictionary<string, WidgetFrame> _frames = new();
    private readonly WeightedStackPanel _columns = new() { Orientation = Orientation.Horizontal, Spacing = Gap };
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _float = new() { Name = "DragFloat", IsVisible = false };
    private readonly List<Panel> _handles = new();
    private readonly ColumnHandleLayer _columnHandleLayer;
    private readonly List<Panel> _columnHandles = new();
    // The one drop preview: a translucent accent rectangle with a caption, shown for the zone under the pointer.
    private readonly Border _preview;
    private readonly TextBlock _previewCaption;
    private IReadOnlyCollection<int> _newColumnGaps = [];
    private int? _newColumnTarget;

    // The columns' widget ids as last built; a layout with the same ones only needs new weights.
    private string _builtStructure = "";

    // The handle being dragged: the widget above it, the pointer's starting height, and the column's
    // height available to its widgets (weights share this out).
    private Panel? _resizeHandle;
    private string? _resizeWidget;
    private double _resizeStartY;
    private double _resizeSpace;

    // The column handle being dragged: the column to its left, the pointer's starting x, and the width
    // the columns share (weights share this out).
    private int? _resizeColumn;
    private double _resizeStartX;
    private double _resizeColumnSpace;

    // The title bar press (or tray card press) that may turn into a drag.
    private WidgetFrame? _pressed;
    private WidgetDefinition? _trayWidget;
    private Point _pressPoint;
    private Point _grabOffset;
    private bool _dragging;
    private (int Column, int Index)? _target;
    // Where the dragged widget started (null for a tray widget).
    private (int Column, int Index)? _origin;
    private IPointer? _pointer;

    // Where the columns and cards were at pick-up: targets are worked out from these, however the cards reflow.
    private IReadOnlyList<Rect> _pickColumns = [];
    private IReadOnlyList<IReadOnlyList<Rect>> _pickWidgets = [];
    private IReadOnlyList<IReadOnlyList<WidgetFrame>> _pickFrames = [];

    // The card the dragged widget would merge into (highlighted while the pointer is over it).
    private WidgetFrame? _mergeTarget;

    // The zone the pointer is in. It only changes to another zone once the pointer has moved a few pixels from where
    // the current one was chosen, so a pointer resting on a boundary does not flicker between two previews.
    private enum ZoneKind { None, NewColumn, Merge, Gap }

    private readonly record struct DropZone(ZoneKind Kind, int A = 0, int B = 0);

    public const double ZoneHysteresis = 4;

    private DropZone _zone;
    private Point _zonePoint;

    // The card being merged into, while a drag hovers it (for tests).
    public WidgetFrame? MergeTarget => _mergeTarget;

    private IBrush BackingBrush() => Theming.ThemeService.Brush(this, "WindowBackgroundBrush");

    public LayoutHost()
    {
        _columnHandleLayer = new ColumnHandleLayer(_columns);
        _float.Classes.Add("dragFloat");

        _previewCaption = new TextBlock();
        var caption = new Border { Name = "DropPreviewCaption", Child = _previewCaption, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        caption.Classes.Add("dropPreviewCaption");
        _preview = new Border { Name = "DropPreview", IsVisible = false, Child = caption };
        _preview.Classes.Add("dropPreview");

        _overlay.Children.Add(_preview);
        _overlay.Children.Add(_float);
        // An explicit dark backing, so no layout pass can ever show the default (light) fallback behind the cards.
        Child = new Panel { Name = "LayoutBacking", Background = BackingBrush(), Children = { _columns, _columnHandleLayer, _overlay } };
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
                    // Edit mode is over: a deleted instance can no longer be brought back by Cancel.
                    ReleaseDeletedInstances();
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
            foreach (var handle in _handles.Concat(_columnHandles))
            {
                SetHandleEditing(handle, value);
            }
        }
    }


    // Which cards are in which columns, and which widgets are on each group (not which tab is active: that
    // only changes what a card shows).
    private static string Structure(IReadOnlyList<LayoutColumnSlot> columns) =>
        string.Join("|", columns.Select(c => string.Join(",", c.Widgets.Select(w =>
            w.IsGroup ? $"{w.Id}({string.Join("+", w.Members.Select(m => m.Id))})" : w.Id))));

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
                var frame = _frames[slot.Id];
                WeightedStackPanel.SetWeight(frame, slot.HeightWeight);
                frame.ShowHeader = slot.ShowHeader;
                SetMembers(frame, slot);
            }
        }
    }

    private bool _isEditing;

    // Builds the columns from the layout.
    private void Rebuild()
    {
        var slots = Layout?.Columns;
        // Take the reused cards out of their old columns first, so each can join its new one.
        foreach (var column in ColumnPanels)
        {
            column.Children.Clear();
        }
        _columns.Children.Clear();
        _handles.Clear();
        _columnHandleLayer.Children.Clear();
        _columnHandles.Clear();
        if (slots is null)
        {
            _builtStructure = "";
            return;
        }
        _builtStructure = Structure(slots);

        // A card that is no longer on the dashboard lets go of its bodies, so the cards that are can take them.
        var placed = slots.SelectMany(c => c.Widgets).Select(w => w.Id).ToHashSet();
        foreach (var (id, gone) in _frames.Where(f => !placed.Contains(f.Key)))
        {
            gone.Release();
        }

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
                frame.ShowHeader = slot.ShowHeader;
                SetMembers(frame, slot);
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
        for (var c = 0; c + 1 < slots.Count; c++)
        {
            var handle = ColumnResizeHandle(c);
            _columnHandles.Add(handle);
            _columnHandleLayer.Children.Add(handle);
        }
        MarkDragSource();
        ReleaseDeletedInstances();
    }

    // A slim vertical pill in the gap right of a column, with a hit area as wide as the gap.
    private Panel ColumnResizeHandle(int columnIndex)
    {
        var grip = new Border();
        grip.Classes.Add("columnResizeGrip");
        var hit = new Border { Background = Brushes.Transparent, Cursor = ColumnResizeCursor };

        var handle = new Panel { Name = $"ColumnResizeHandle_{columnIndex}", Children = { grip, hit } };
        handle.Classes.Add("columnResizeHandle");
        SetHandleEditing(handle, _isEditing);

        hit.PointerPressed += (_, e) => OnColumnHandlePressed(handle, columnIndex, e);
        hit.PointerMoved += (_, e) => OnColumnHandleMoved(e);
        hit.PointerReleased += (_, e) => OnColumnHandleReleased(e);
        hit.PointerCaptureLost += (_, _) => OnColumnHandleCaptureLost();
        return handle;
    }

    private void OnColumnHandlePressed(Panel handle, int columnIndex, PointerPressedEventArgs e)
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
        _resizeColumn = columnIndex;
        _resizeStartX = e.GetPosition(this).X;
        _resizeColumnSpace = ColumnScrollers.Sum(s => s.Bounds.Width);
        handle.Classes.Add("active");
        e.Pointer.Capture((IInputElement?)e.Source);
        e.Handled = true;
    }

    // Columns never go under the minimum width; when the window is too narrow for that, nothing moves.
    private void OnColumnHandleMoved(PointerEventArgs e)
    {
        if (_resizeColumn is not { } column || Layout is not { IsResizing: true } layout || _resizeColumnSpace <= 0)
        {
            return;
        }
        var dx = e.GetPosition(this).X - _resizeStartX;
        layout.PreviewColumnResize(column, dx / _resizeColumnSpace, LayoutLimits.MinimumColumnWidth / _resizeColumnSpace);
    }

    private void OnColumnHandleReleased(PointerReleasedEventArgs e)
    {
        if (_resizeColumn is not null)
        {
            OnColumnHandleMoved(e);
            Layout?.CommitResize();
        }
        EndResize();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnColumnHandleCaptureLost()
    {
        if (_resizeColumn is not null)
        {
            Layout?.CancelResize();
        }
        EndResize();
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
        _resizeColumn = null;
    }

    // Each widget's view is made once and kept (with its state) however the cards are grouped.
    private readonly Dictionary<string, WidgetView> _views = new();

    private WidgetView ViewOf(WidgetDefinition widget)
    {
        if (!_views.TryGetValue(widget.Id, out var view))
        {
            view = (WidgetView)Activator.CreateInstance(widget.ViewType)!;
            view.InstanceId = widget.Id;
            _views[widget.Id] = view;
        }
        return view;
    }

    // Lets go of the card and view of every instance (of a widget that allows several) that was deleted. An instance
    // removed during editing is kept until editing ends, so Cancel can bring it back with its view; widgets that
    // allow one instance are never released (they come back from the tray).
    private void ReleaseDeletedInstances()
    {
        if (Layout is null)
        {
            return;
        }
        var kept = Layout.Layout.WidgetIds.Concat(Layout.Snapshot?.WidgetIds ?? []).ToHashSet();
        foreach (var id in _frames.Keys.Where(id => InstanceIds.IsInstance(id) && !kept.Contains(id)).ToList())
        {
            _frames[id].Release();
            _frames.Remove(id);
        }
        foreach (var id in _views.Keys.Where(id => InstanceIds.IsInstance(id) && !kept.Contains(id)).ToList())
        {
            (_views[id] as IDisposable)?.Dispose();
            _views.Remove(id);
        }
    }

    private void SetMembers(WidgetFrame frame, WidgetSlot slot) =>
        frame.SetMembers(slot.Members.Select(m => (m, ViewOf(m))).ToList(), slot.ActiveIndex, slot.ViewMode, slot.PaneWeights);

    private WidgetFrame Frame(WidgetDefinition widget)
    {
        if (!_frames.TryGetValue(widget.Id, out var frame))
        {
            frame = new WidgetFrame(widget, ViewOf(widget)) { IsEditing = _isEditing };
            var created = frame;
            frame.EditBar.Cursor = GrabCursor;
            frame.EditBar.PointerPressed += (_, e) => OnBarPressed(created, e);
            frame.RemoveButton.Click += (_, _) => Layout?.RemoveWidget(created.WidgetId);
            frame.HeaderButton.Click += (_, _) => Layout?.ToggleHeader(created.WidgetId);
            frame.TabSelected += id => Layout?.SelectTab(id);
            frame.TabUnmerged += id => Layout?.Unmerge(id);
            frame.TabReordered += (id, index) => Layout?.ReorderTab(id, index);
            frame.ViewModeRequested += (mode, width) => Layout?.SetViewMode(created.WidgetId, mode, width);
            frame.PaneResizeBegan += () => Layout?.BeginPaneResize();
            frame.PaneResizeMoved += (id, dx, leftMin, rightMin, space) =>
                Layout?.PreviewPaneResize(id, dx / space, leftMin / space, rightMin / space);
            frame.PaneResizeEnded += commit =>
            {
                if (commit)
                {
                    Layout?.CommitPaneResize();
                }
                else
                {
                    Layout?.CancelPaneResize();
                }
            };
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
        var id = layout.DraggedWidgetId;
        if (_newColumnTarget is { } newColumn)
        {
            _droppedId = id;
            layout.DropWidgetInNewColumn(newColumn);
        }
        else if (_mergeTarget is { } merge)
        {
            _droppedId = merge.WidgetId;
            layout.DropOnWidget(merge.WidgetId, merge.InnerWidth);
        }
        else if (_target is { } target)
        {
            _droppedId = id;
            layout.DropWidget(target.Column, target.Index);
        }
        else
        {
            layout.CancelWidgetDrag();
        }
    }

    // The drop border: neutral to amber over DropBorderIn, then back over DropBorderOut. The only place these are set.
    public static readonly TimeSpan DropBorderIn = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan DropBorderOut = TimeSpan.FromMilliseconds(350);

    // The drop border's quiet and accent colors, read from the theme each time so a changed mode or accent shows.
    private Color NeutralBorder => ColorWithAlpha("EditHairlineBrush", 0x29);
    private Color AmberBorder => ColorWithAlpha("AccentBrush", 0xB0);

    private Color ColorWithAlpha(string key, byte alpha) =>
        Theming.ThemeService.Brush(this, key) is ISolidColorBrush b ? b.Color.WithAlpha(alpha) : default;

    private string? _droppedId;

    // The run of the latest drop border animation (for tests), and the card it runs on.
    public Task? DropBorderRun { get; private set; }
    public WidgetFrame? DropBorderFrame { get; private set; }

    // A KeyFrame animation, not a style transition: it starts on the card after the drop has attached it, so it
    // runs on whatever control the layout rebuild put in the slot.
    private void StartDropBorder(string widgetId)
    {
        if (!_frames.TryGetValue(widgetId, out var frame) || !frame.IsAttachedToVisualTree())
        {
            return;
        }
        KeyFrame Key(double cue, Color color) =>
            new() { Cue = new Cue(cue), Setters = { new Setter(Border.BorderBrushProperty, new SolidColorBrush(color)) } };
        var total = DropBorderIn + DropBorderOut;
        var animation = new Animation
        {
            Duration = total,
            Easing = new CubicEaseInOut(),
            Children = { Key(0, NeutralBorder), Key(DropBorderIn / total, AmberBorder), Key(1, NeutralBorder) },
        };
        DropBorderFrame = frame;
        DropBorderRun = animation.RunAsync(frame);
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
            StartDragVisuals(frame, CardTitle(frame));
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
        _origin = frame is null ? null : Layout?.Layout.Find(frame.WidgetId);
        _pickColumns = ColumnScrollers.Select(BoundsInHost).ToList();
        _pickFrames = Frames;
        _pickWidgets = Frames.Select(c => (IReadOnlyList<Rect>)c.Select(BoundsInHost).ToList()).ToList();
        FindNewColumnGaps(frame?.WidgetId);

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
        // Shown at rest first, then lifted: the style transitions scale and shadow over ~150 ms.
        _float.Classes.Set("lifted", false);
        _float.IsVisible = true;
        _float.UpdateLayout();
        _float.Classes.Set("lifted", true);
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
            var source = widgetId == id;
            if (source == frame.Classes.Contains("dragSource"))
            {
                continue;
            }
            frame.Classes.Set("dragSource", source);
        }
    }

    // The gaps a new column could go into. A widget alone in its column skips the two gaps beside its own
    // column (it would land where it is).
    private void FindNewColumnGaps(string? placedWidgetId)
    {
        _newColumnGaps = [];
        if (Layout is not { } layout || !layout.CanOfferNewColumn(Bounds.Width, Gap))
        {
            return;
        }
        var at = placedWidgetId is null ? null : layout.Layout.Find(placedWidgetId);
        var alone = at is { } a && layout.Layout.Columns[a.Column].Widgets.Count == 1;
        _newColumnGaps = Enumerable.Range(0, _pickColumns.Count + 1)
            .Where(i => !(alone && (i == at!.Value.Column || i == at.Value.Column + 1)))
            .ToList();
    }

    // Moves the floating widget with the pointer and marks where it would land: a phantom column in a new-column
    // zone, otherwise a line at the gap in the hovered column. Nothing on the board moves.
    private void UpdateDrag(Point point)
    {
        Canvas.SetLeft(_float, point.X - _grabOffset.X);
        Canvas.SetTop(_float, point.Y - _grabOffset.Y);

        var candidate = Classify(point);
        if (candidate != _zone && (_zone.Kind == ZoneKind.None || Distance(point, _zonePoint) >= ZoneHysteresis))
        {
            _zone = candidate;
            _zonePoint = point;
        }
        ApplyZone();
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // The zone a pointer position belongs to: a new column, merging into the middle of a card, or the gap near a card's
    // top or bottom edge.
    private DropZone Classify(Point point)
    {
        if (LayoutDropTargets.FindNewColumn(point, _pickColumns, Bounds.Width, Gap, EdgeZoneFraction, DividerZoneWidth, _newColumnGaps) is { } gap)
        {
            return new DropZone(ZoneKind.NewColumn, gap);
        }
        // Over the middle of another card: merge into it. Near its top or bottom edge: insert there.
        if (Layout is { } layout
            && LayoutDropTargets.FindMerge(point, _pickWidgets, at => layout.CanMergeInto(_pickFrames[at.Column][at.Index].WidgetId)) is { } merge)
        {
            return new DropZone(ZoneKind.Merge, merge.Column, merge.Index);
        }
        return LayoutDropTargets.Find(point, _pickColumns, _pickWidgets, Gap) is { } target
            ? new DropZone(ZoneKind.Gap, target.Column, target.Index)
            : default;
    }

    // Names a card for the drop caption: its title, or its members' titles for a group.
    private static string CardTitle(WidgetFrame frame) =>
        string.Join(" + ", frame.MemberTitles);

    // Shows the one preview for the current zone, and records where a drop would go.
    private void ApplyZone()
    {
        _newColumnTarget = _zone.Kind == ZoneKind.NewColumn ? _zone.A : null;
        _mergeTarget = _zone.Kind == ZoneKind.Merge ? _pickFrames[_zone.A][_zone.B] : null;
        _target = _zone.Kind == ZoneKind.Gap ? (_zone.A, _zone.B) : null;

        Rect? rect = null;
        string caption = "";
        switch (_zone.Kind)
        {
            case ZoneKind.NewColumn:
                rect = LayoutDropTargets.PhantomColumn(_zone.A, _pickColumns, Bounds.Width, Gap);
                caption = "New column";
                break;
            case ZoneKind.Merge:
                rect = _pickWidgets[_zone.A][_zone.B];
                caption = $"Tab with {CardTitle(_pickFrames[_zone.A][_zone.B])}";
                break;
            case ZoneKind.Gap when !LayoutDropTargets.IsHome((_zone.A, _zone.B), _origin):
                // A gap that would put the widget back where it was gets no preview: its slot already shows as the home placeholder.
                var column = _pickColumns[_zone.A];
                var cards = _pickFrames[_zone.A];
                var y = LayoutDropTargets.InsertionY(column, _pickWidgets[_zone.A], _zone.B, Gap);
                rect = new Rect(column.Left, y - GapPreviewHeight / 2, column.Width, GapPreviewHeight);
                caption = _zone.B < cards.Count ? $"Above {CardTitle(cards[_zone.B])}"
                    : cards.Count > 0 ? $"Below {CardTitle(cards[^1])}" : "Here";
                break;
        }

        SetActive(_preview, rect is not null);
        if (rect is { } r)
        {
            _preview.Width = r.Width;
            _preview.Height = r.Height;
            Canvas.SetLeft(_preview, r.X);
            Canvas.SetTop(_preview, r.Y);
            _previewCaption.Text = caption;
        }
    }

    // How tall the preview is for an insert between cards.
    private const double GapPreviewHeight = 32;

    // Shows a drop target at rest first, then activates it, so the style eases it in (~120 ms).
    private static void SetActive(Border target, bool active)
    {
        if (active && !target.IsVisible)
        {
            target.Classes.Set("active", false);
            target.IsVisible = true;
            target.UpdateLayout();
        }
        else if (!active)
        {
            target.IsVisible = false;
        }
        target.Classes.Set("active", active);
    }

    private void HideFloat()
    {
        _float.IsVisible = false;
        _float.Classes.Set("lifted", false);
        _float.Child = null;
    }

    private void EndDrag()
    {
        _pressed = null;
        _trayWidget = null;
        _dragging = false;
        _target = null;
        _mergeTarget = null;
        _zone = default;
        _origin = null;
        Cursor = null;
        _newColumnTarget = null;
        _newColumnGaps = [];
        SetActive(_preview, false);
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
        // The cards are laid out in their final places before the floating snapshot goes, so there is never a
        // frame with the slot empty and the overlay already gone.
        UpdateLayout();
        HideFloat();
        if (_droppedId is { } dropped)
        {
            _droppedId = null;
            StartDropBorder(dropped);
        }
        if (_pointer is { } pointer)
        {
            _pointer = null;
            if (pointer.Captured == this)
            {
                pointer.Capture(null);
            }
        }
    }

    private Rect BoundsInHost(Visual visual) =>
        new(visual.TranslatePoint(new Point(0, 0), this) ?? default, visual.Bounds.Size);

    // Lays the column handles over the gaps of the columns panel: the i-th handle fills the gap right of the
    // i-th column. It is arranged after the columns, so their final positions are known.
    private sealed class ColumnHandleLayer(Panel columns) : Panel
    {
        private readonly List<Rect> _arranged = new();

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            columns.LayoutUpdated += OnColumnsLayoutUpdated;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            columns.LayoutUpdated -= OnColumnsLayoutUpdated;
            base.OnDetachedFromVisualTree(e);
        }

        // The columns can move (weights changed, a widget moved) without this layer being invalidated, so
        // re-arrange the handles whenever a column's bounds differ from where they were last placed.
        private void OnColumnsLayoutUpdated(object? sender, EventArgs e)
        {
            var bounds = columns.Children.OfType<ScrollViewer>().Select(s => s.Bounds).ToList();
            if (!bounds.SequenceEqual(_arranged))
            {
                InvalidateArrange();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(Gap, availableSize.Height));
            }
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var scrollers = columns.Children.OfType<ScrollViewer>().ToList();
            _arranged.Clear();
            _arranged.AddRange(scrollers.Select(s => s.Bounds));
            for (var i = 0; i < Children.Count && i + 1 < scrollers.Count; i++)
            {
                var column = scrollers[i].Bounds;
                Children[i].Arrange(new Rect(column.Right, column.Top, Gap, column.Height));
            }
            return finalSize;
        }
    }
}
