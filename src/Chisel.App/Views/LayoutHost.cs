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
// a ghost follows the pointer, an accent line (or a highlighted "Drop here" zone in an empty column)
// shows where it will land, and releasing drops it there. Nothing else moves until the drop.
public sealed class LayoutHost : Decorator
{
    public const double Gap = 16;

    // How far the pointer must move before a press on a title bar becomes a drag.
    public const double DragThreshold = 4;

    public static readonly StyledProperty<LayoutViewModel?> LayoutProperty =
        AvaloniaProperty.Register<LayoutHost, LayoutViewModel?>(nameof(Layout));

    private static readonly Cursor GrabCursor = new(StandardCursorType.Hand);
    private static readonly Cursor DraggingCursor = new(StandardCursorType.DragMove);

    private readonly Dictionary<string, WidgetFrame> _frames = new();
    private readonly WeightedStackPanel _columns = new() { Orientation = Orientation.Horizontal, Spacing = Gap };
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _indicator = new() { Name = "DropIndicator", IsVisible = false };
    private readonly Border _ghost;
    private readonly TextBlock _ghostTitle = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<Control> _dropZones = new();

    // The title bar press that may turn into a drag.
    private WidgetFrame? _pressed;
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

        _overlay.Children.Add(_indicator);
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
                Rebuild();
                break;
            case nameof(LayoutViewModel.IsEditing):
                IsEditing = Layout?.IsEditing == true;
                if (!IsEditing)
                {
                    EndDrag();
                }
                break;
            case nameof(LayoutViewModel.DraggedWidgetId) when Layout?.IsDraggingWidget != true:
                // Dropped, or cancelled (Esc): clear the drag visuals.
                EndDrag();
                break;
        }
    }

    // Puts every card (and the empty columns' drop zones) in or out of the editing look.
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
            foreach (var zone in _dropZones)
            {
                zone.IsVisible = value;
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
        _dropZones.Clear();

        if (Layout is null)
        {
            return;
        }

        for (var c = 0; c < Layout.Columns.Count; c++)
        {
            var column = Layout.Columns[c];
            var panel = new WeightedStackPanel { Orientation = Orientation.Vertical, Spacing = Gap };
            panel.Classes.Add("layoutColumn");
            foreach (var slot in column.Widgets)
            {
                var frame = Frame(slot.Widget);
                WeightedStackPanel.SetWeight(frame, slot.HeightWeight);
                panel.Children.Add(frame);
            }
            if (column.Widgets.Count == 0)
            {
                panel.Children.Add(DropZone(c));
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

    // A dashed "Drop here" area filling an empty column; shown only in edit mode.
    private Control DropZone(int column)
    {
        var outline = new Rectangle { RadiusX = 10, RadiusY = 10 };
        outline.Classes.Add("dropZoneOutline");
        var label = new TextBlock { Text = "Drop here", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("dropZoneLabel");
        var zone = new Panel { Name = $"DropZone{column}", IsVisible = _isEditing, Children = { outline, label } };
        zone.Classes.Add("dropZone");
        _dropZones.Add(zone);
        return zone;
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
            _frames[widget.Id] = frame;
        }
        return frame;
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
            StartDragVisuals(frame);
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
            if (_target is { } target)
            {
                layout.DropWidget(target.Column, target.Index);
            }
            else
            {
                layout.CancelWidgetDrag();
            }
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

    private void StartDragVisuals(WidgetFrame frame)
    {
        _dragging = true;
        frame.Classes.Add("dragSource");
        frame.EditBar.Cursor = DraggingCursor;
        _ghostTitle.Text = frame.Widget.Title;
        _ghost.Width = Math.Min(frame.Bounds.Width, 360);
        _grabOffset = new Point(Math.Min(_grabOffset.X, _ghost.Width - 24), _grabOffset.Y);
        _ghost.IsVisible = true;
    }

    // Moves the ghost with the pointer and shows where the widget would land.
    private void UpdateDrag(Point point)
    {
        Canvas.SetLeft(_ghost, point.X - _grabOffset.X);
        Canvas.SetTop(_ghost, point.Y - _grabOffset.Y);

        var scrollers = ColumnScrollers;
        var frames = Frames;
        var columnRects = scrollers.Select(BoundsInHost).ToList();
        var widgetRects = frames.Select(c => (IReadOnlyList<Rect>)c.Select(BoundsInHost).ToList()).ToList();
        var target = LayoutDropTargets.Find(point, columnRects, widgetRects, Gap);

        // Dropping where it already is would change nothing, so that is not shown as a target.
        if (target is { } t && Layout is { DraggedWidgetId: { } id } layout
            && ReferenceEquals(layout.Layout.Move(id, t.Column, t.Index), layout.Layout))
        {
            target = null;
        }
        _target = target;

        foreach (var zone in _dropZones)
        {
            zone.Classes.Set("active", false);
        }
        _indicator.IsVisible = false;
        if (target is not { } drop)
        {
            return;
        }

        var column = columnRects[drop.Column];
        var cards = widgetRects[drop.Column];
        if (cards.Count == 0)
        {
            _dropZones.FirstOrDefault(z => z.Name == $"DropZone{drop.Column}")?.Classes.Set("active", true);
            return;
        }

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
        foreach (var zone in _dropZones)
        {
            zone.Classes.Set("active", false);
        }
        _pressed = null;
        _dragging = false;
        _target = null;
        _indicator.IsVisible = false;
        _ghost.IsVisible = false;
    }

    private Rect BoundsInHost(Visual visual) =>
        new(visual.TranslatePoint(new Point(0, 0), this) ?? default, visual.Bounds.Size);
}
