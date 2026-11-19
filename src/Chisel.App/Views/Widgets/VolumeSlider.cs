using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Chisel.App.Views.Widgets;

// A thin slider drawn like the media progress line: a 3 px track, an accent fill and a small round thumb. Dragging
// raises Dragged with the new value (0..1) on every move; DragCompleted comes when the pointer is released.
public sealed class VolumeSlider : Control
{
    private const double TrackHeight = 3;
    private const double ThumbRadius = 5;

    private double _value;
    private bool _dragging;
    private IBrush? _track, _fill, _trackOverride;

    public VolumeSlider()
    {
        Height = 18;
        MinWidth = 40;
        Cursor = new Cursor(StandardCursorType.Hand);
        this.GetResourceObservable("GridLineBrush").Subscribe(new Watch(b => { _track = b as IBrush; InvalidateVisual(); }));
        this.GetResourceObservable("AccentBrush").Subscribe(new Watch(b => { _fill = b as IBrush; InvalidateVisual(); }));
    }

    // A brush for the track instead of the theme's (the media widget sets one that shows over its gradient); null goes back to the theme.
    public IBrush? TrackOverride
    {
        get => _trackOverride;
        set
        {
            _trackOverride = value;
            InvalidateVisual();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 1);
            if (value != _value)
            {
                _value = value;
                InvalidateVisual();
            }
        }
    }

    public event Action<double>? Dragged;
    public event Action? DragCompleted;

    private double Usable => Math.Max(1, Bounds.Width - 2 * ThumbRadius);

    public override void Render(DrawingContext context)
    {
        var middle = Bounds.Height / 2;
        var track = new Rect(ThumbRadius, middle - TrackHeight / 2, Usable, TrackHeight);
        context.DrawRectangle(_trackOverride ?? _track, null, track, 1.5, 1.5);
        var x = ThumbRadius + _value * Usable;
        context.DrawRectangle(_fill, null, new Rect(ThumbRadius, track.Y, x - ThumbRadius, TrackHeight), 1.5, 1.5);
        context.DrawEllipse(_fill, null, new Point(x, middle), ThumbRadius, ThumbRadius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        MoveTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            MoveTo(e.GetPosition(this).X);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        pointer?.Capture(null);
        DragCompleted?.Invoke();
    }

    private void MoveTo(double x)
    {
        Value = (x - ThumbRadius) / Usable;
        Dragged?.Invoke(_value);
    }

    private sealed class Watch(Action<object?> apply) : IObserver<object?>
    {
        public void OnNext(object? value) => apply(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
