using System;
using Chisel.Core.Media;

namespace Chisel.App.ViewModels;

// What the media widget's volume row shows: the system output level and mute state. While the slider is dragged the
// level is written at most about 30 times a second (the final value always lands on CompleteDrag), and the service's
// echo of our own write is ignored so the thumb does not jitter. Changes made elsewhere update the row at once.
public sealed class VolumeViewModel : ViewModelBase
{
    private static readonly TimeSpan MinWriteGap = TimeSpan.FromMilliseconds(33);
    private const double DefaultAudible = 0.3;
    private const double WheelStep = 0.02;

    private readonly IVolumeService _service;
    private readonly TimeProvider _time;
    private DateTimeOffset? _lastWrite;
    private double? _pending;
    private double? _echo;
    private bool _dragging;
    private double _lastAudible = DefaultAudible;
    private double _level;
    private bool _muted;
    private bool _available;

    public VolumeViewModel(IVolumeService service, TimeProvider time)
    {
        _service = service;
        _time = time;
        Read(includeLevel: true);
        _service.Changed += OnServiceChanged;
    }

    public bool IsAvailable => _available;
    public double Level => _level;
    public bool IsMuted => _muted;
    public VolumeIcon Icon => VolumeMath.Icon(_level, _muted);
    public string Tip => !_available ? "No output device" : _muted ? "Muted" : VolumeMath.Percent(_level);

    // The slider moved: show it now, write it to the system (throttled).
    public void DragTo(double level)
    {
        if (!_available)
        {
            return;
        }
        level = VolumeMath.Clamp(level);
        _dragging = true;
        SetShown(level, false);
        var now = _time.GetUtcNow();
        if (_lastWrite is null || now - _lastWrite >= MinWriteGap)
        {
            Write(level, now);
        }
        else
        {
            _pending = level;
        }
    }

    // The slider was let go: whatever was held back is written now.
    public void CompleteDrag()
    {
        if (_pending is { } level)
        {
            Write(level, _time.GetUtcNow());
        }
        _dragging = false;
    }

    // Mouse-wheel volume: enabled by the "Scroll to change volume" setting, blocked while widgets are being edited.
    public bool ScrollEnabled { get; set; }
    public bool ScrollBlocked { get; set; }

    // Raised with the new level after a wheel change, so the widget can flash the percentage.
    public event EventHandler<double>? ScrollFeedback;

    // One wheel notch is 2 percent; fractional deltas (touchpads, smooth wheels) add up in the shown level.
    // Returns true when the wheel was used, so the caller marks the event handled. Held-back writes land on CompleteDrag.
    public bool ScrollBy(double notches)
    {
        if (!ScrollEnabled || ScrollBlocked || !_available || notches == 0 || double.IsNaN(notches))
        {
            return false;
        }
        DragTo(_level + notches * WheelStep);
        ScrollFeedback?.Invoke(this, _level);
        return true;
    }

    // Mute, or unmute back to the level it had. From silence (level 0) it goes back to the last audible level.
    public void ToggleMute()
    {
        if (!_available)
        {
            return;
        }
        if (_service.IsMuted || _level <= 0)
        {
            if (_level <= 0)
            {
                _echo = _lastAudible;
                _service.SetLevel(_lastAudible);
            }
            _service.SetMuted(false);
        }
        else
        {
            _service.SetMuted(true);
        }
        Read(includeLevel: true);
    }

    private void Write(double level, DateTimeOffset now)
    {
        _lastWrite = now;
        _pending = null;
        _echo = level;
        try
        {
            if (_service.IsMuted)
            {
                _service.SetMuted(false);
            }
            _service.SetLevel(level);
        }
        catch (Exception)
        {
        }
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        var includeLevel = !_dragging && !(_echo is { } echo && Math.Abs(_service.Level - echo) < 0.002);
        Read(includeLevel);
        if (includeLevel)
        {
            _echo = null;
        }
    }

    private void Read(bool includeLevel)
    {
        SetShown(includeLevel ? _service.Level : _level, _service.IsMuted, _service.IsAvailable);
    }

    private void SetShown(double level, bool muted, bool? available = null)
    {
        var levelChanged = level != _level;
        var mutedChanged = muted != _muted;
        var availableChanged = available is { } a && a != _available;
        _level = level;
        _muted = muted;
        if (level > 0)
        {
            _lastAudible = level;
        }
        if (available is { } v)
        {
            _available = v;
        }
        if (levelChanged)
        {
            OnPropertyChanged(nameof(Level));
        }
        if (mutedChanged)
        {
            OnPropertyChanged(nameof(IsMuted));
        }
        if (availableChanged)
        {
            OnPropertyChanged(nameof(IsAvailable));
        }
        if (levelChanged || mutedChanged || availableChanged)
        {
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(Tip));
        }
    }
}
