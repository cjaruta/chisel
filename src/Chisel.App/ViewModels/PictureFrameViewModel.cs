using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Chisel.Core.Pictures;

namespace Chisel.App.ViewModels;

// One picture frame's slideshow: which picture is shown, when the next comes (Cycle mode), and what a click or an
// arrow key does. It never touches a bitmap; the view loads the picture it names. It only runs while the frame can be
// seen: the view says so (IsVisible), and the window and edit mode can stop it too.
public sealed class PictureFrameViewModel
{
    // How long after a step in Manual mode before the position is saved (a burst of clicks saves once).
    public static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    private readonly PictureFramesViewModel _owner;
    private int[] _order = [];
    private int _position;
    private IReadOnlyList<FramePicture> _pictures = [];
    private SlideshowOrder _orderMode;
    private DateTimeOffset _shownAt;
    private DateTimeOffset? _saveDue;
    private bool _wasRunning;
    private bool _visible;

    public PictureFrameViewModel(string frameId, PictureFramesViewModel owner)
    {
        FrameId = frameId;
        _owner = owner;
        var content = owner.Content(frameId);
        _pictures = content.Pictures;
        _orderMode = content.Options.Order;
        _order = Slideshow.PlayOrder(_pictures.Count, _orderMode, owner.ShuffleSeed);
        _position = Math.Max(0, Array.IndexOf(_order, Math.Clamp(content.Options.CurrentIndex, 0, Math.Max(0, _pictures.Count - 1))));
        _shownAt = owner.Now;
    }

    public string FrameId { get; }

    public FrameContent Content => _owner.Content(FrameId);

    public int Count => _pictures.Count;

    // The picture shown, as its place in the frame's list (0 when there are none).
    public int CurrentIndex => _order.Length == 0 ? 0 : _order[_position];

    public FramePicture? Current => _pictures.Count == 0 ? null : _pictures[CurrentIndex];

    // The picture that comes next (what the view keeps decoded besides the current one); null with fewer than two.
    public FramePicture? Next => _pictures.Count < 2 ? null : _pictures[_order[Slideshow.Next(_position, _order.Length)]];

    // Raised when the picture to show changed (a step, or the list changed under it).
    public event Action? CurrentChanged;

    // Raised when anything about the frame changed that is only a redraw (options, a crop).
    public event Action? Changed;

    // Whether the frame can be seen: its view is attached, shown, and not in a hidden tab. Set by the view.
    public bool IsVisible
    {
        get => _visible;
        set
        {
            _visible = value;
            UpdateRunning();
        }
    }

    // Raised when IsRunning changed (the view starts or stops animated pictures).
    public event Action? RunningChanged;

    private bool _lastRunning;

    internal void UpdateRunning()
    {
        if (IsRunning != _lastRunning)
        {
            _lastRunning = IsRunning;
            RunningChanged?.Invoke();
        }
    }

    // Visible, the window is not minimized, and the layout is not being edited. The timer only runs then.
    public bool IsRunning => _visible && !_owner.WindowMinimized && !_owner.IsEditing;

    // Whether animated pictures play: the same rule as the timer.
    public bool ShouldAnimate => IsRunning;

    // A load in progress for this frame (a dropped picture on its way) and the last message about it.
    private int _pending;

    public int Pending
    {
        get => _pending;
        internal set
        {
            _pending = value;
            Changed?.Invoke();
        }
    }

    // Moves by one picture (wrapping) and restarts the timer. Does nothing with fewer than two pictures.
    public bool Step(int direction)
    {
        if (_pictures.Count < 2)
        {
            return false;
        }
        _position = direction >= 0 ? Slideshow.Next(_position, _order.Length) : Slideshow.Previous(_position, _order.Length);
        _shownAt = _owner.Now;
        if (Content.Options.Mode == SlideshowMode.Manual)
        {
            _saveDue = _owner.Now + SaveDelay;
        }
        CurrentChanged?.Invoke();
        return true;
    }

    // A press and release on the frame: x across a frame of the given width, and how far the pointer moved between them.
    // The right half goes forward, the left back. A drag, edit mode and a lone picture do nothing.
    public bool HandleClick(double x, double width, double dragX, double dragY)
    {
        if (_owner.IsEditing || IsAdjusting || !Slideshow.IsClick(dragX, dragY))
        {
            return false;
        }
        return Step(Slideshow.StepForClick(x, width));
    }

    public bool HandleKey(Key key)
    {
        if (_owner.IsEditing || key is not (Key.Left or Key.Right))
        {
            return false;
        }
        return Step(key == Key.Right ? 1 : -1);
    }

    // ----- adjusting the shown picture (edit mode): drag to pan, wheel to zoom, Reset, Done. Saved per picture.

    public bool IsAdjusting => _owner.AdjustingFrameId == FrameId;

    // Whether the picture shown can be adjusted: a still picture, and a layout being edited.
    public bool CanAdjust =>
        _owner.IsEditing && Current is { } picture && _owner.AssetOf(picture.AssetId) is { IsGif: false };

    // Raised when the frame entered or left the adjust state.
    public event Action? AdjustStateChanged;

    internal void AdjustingChanged() => AdjustStateChanged?.Invoke();

    public bool BeginAdjust() => CanAdjust && _owner.BeginAdjust(FrameId);

    // Drags the picture by (dragX, dragY) pixels in a view of the given size.
    public void AdjustPan(double viewWidth, double viewHeight, double dragX, double dragY)
    {
        if (!IsAdjusting || Current is not { } picture || _owner.AssetOf(picture.AssetId) is not { } asset)
        {
            return;
        }
        var (x, y) = CropMath.Pan(asset.Width, asset.Height, viewWidth, viewHeight, picture.FocalX, picture.FocalY, picture.Zoom, dragX, dragY);
        _owner.SetCrop(FrameId, CurrentIndex, x, y, picture.Zoom);
    }

    // Zooms by wheel steps (positive in): each step is 10 percent.
    public void AdjustZoom(double steps)
    {
        if (IsAdjusting && Current is { } picture)
        {
            _owner.SetCrop(FrameId, CurrentIndex, picture.FocalX, picture.FocalY, picture.Zoom * Math.Pow(1.1, steps));
        }
    }

    public void ResetCrop()
    {
        if (IsAdjusting)
        {
            _owner.SetCrop(FrameId, CurrentIndex, 0.5, 0.5, 1);
        }
    }

    // Called about every 200 ms by the window's tick.
    public void Tick()
    {
        var now = _owner.Now;
        if (_saveDue is { } due && now >= due)
        {
            _saveDue = null;
            _owner.SaveCurrentIndex(FrameId, CurrentIndex);
        }

        if (!IsRunning)
        {
            _wasRunning = false;
            return;
        }
        if (!_wasRunning)
        {
            // Just became visible again: count the interval from now, so it never jumps the moment it returns.
            _wasRunning = true;
            _shownAt = now;
            return;
        }
        var options = Content.Options;
        if (options.Mode == SlideshowMode.Cycle && _pictures.Count > 1
            && now - _shownAt >= TimeSpan.FromSeconds(SlideshowInterval.Clamp(options.IntervalSeconds)))
        {
            Step(1);
        }
    }

    // The frame's list or options changed: keep showing the same picture when it is still there.
    internal void ContentChanged()
    {
        var content = Content;
        var before = Current;
        var shown = before?.AssetId;
        var oldIndex = CurrentIndex;
        var sameList = content.Pictures.Select(p => p.AssetId).SequenceEqual(_pictures.Select(p => p.AssetId));
        var sameOrder = content.Options.Order == _orderMode;
        _pictures = content.Pictures;
        if (sameList && sameOrder)
        {
            // Only options or a crop changed: the picture shown stays, and the view may redraw it.
            Changed?.Invoke();
            return;
        }

        _orderMode = content.Options.Order;
        _order = Slideshow.PlayOrder(_pictures.Count, _orderMode, _owner.ShuffleSeed);
        var at = shown is { } id ? _pictures.ToList().FindIndex(p => p.AssetId == id) : -1;
        var list = at >= 0 ? at : Math.Clamp(oldIndex, 0, Math.Max(0, _pictures.Count - 1));
        _position = Math.Max(0, Array.IndexOf(_order, list));
        _shownAt = _owner.Now;
        CurrentChanged?.Invoke();
        Changed?.Invoke();
    }
}
