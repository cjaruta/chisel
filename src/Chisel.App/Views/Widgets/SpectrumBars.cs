using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Chisel.Core.Media;

namespace Chisel.App.Views.Widgets;

// The little audio bars in the media widget. It owns the capture lifecycle: the source runs only while the bars are
// enabled, a session is playing, the control is on screen and visible, and the window is not minimized. While running it
// reads the levels and redraws about 30 times a second; nothing is allocated per frame. Stopped, the bars sit flat.
public sealed class SpectrumBars : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<SpectrumBars, IBrush?>(nameof(Fill));

    private const double MaxWidth = 120;
    private const double BarHeight = 22;
    private const double Gap = 2;
    private const double MinBar = 2;
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 30);

    private ISpectrumSource? _source;
    private float[] _levels = [];
    private DispatcherTimer? _timer;
    private Window? _window;
    private long _last;
    private bool _enabled = true;
    private bool _playing;
    private bool _attached;

    static SpectrumBars()
    {
        AffectsRender<SpectrumBars>(FillProperty);
    }

    public SpectrumBars()
    {
        IsHitTestVisible = false;
        this.Bind(FillProperty, this.GetResourceObservable("AccentBrush"));
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public ReadOnlySpan<float> Levels => _levels;

    public bool IsCapturing { get; private set; }

    public ISpectrumSource? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }
            Stop();
            _source = value;
            _levels = value is null ? [] : new float[value.BandCount];
            Update();
        }
    }

    // The setting: off hides the bars and never starts capture.
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            IsVisible = value;
            Update();
        }
    }

    // Whether the media session is playing.
    public bool Playing
    {
        get => _playing;
        set
        {
            _playing = value;
            Update();
        }
    }

    // Reads the newest levels from the source and redraws. Called by the timer; tests call it directly.
    public void Advance(double elapsedSeconds)
    {
        _source?.ReadLevels(_levels, elapsedSeconds);
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowChanged;
        }
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowChanged;
            _window = null;
        }
        Update();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == nameof(IsEffectivelyVisible))
        {
            Update();
        }
    }

    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            Update();
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(MaxWidth, double.IsInfinity(availableSize.Width) ? MaxWidth : availableSize.Width), BarHeight);

    public override void Render(DrawingContext context)
    {
        if (Fill is { } fill)
        {
            Draw(context, _levels, Bounds.Size, fill);
        }
    }

    // The single draw path (the control and the tests): one bar per level, bottom-aligned, never shorter than a thin line.
    public static void Draw(DrawingContext context, ReadOnlySpan<float> levels, Size size, IBrush fill)
    {
        var count = levels.Length;
        if (count == 0)
        {
            return;
        }
        var barWidth = (size.Width - Gap * (count - 1)) / count;
        if (barWidth < 1)
        {
            return;
        }
        var radius = Math.Min(1.5, barWidth / 2);
        for (var i = 0; i < count; i++)
        {
            var level = Math.Clamp(levels[i], 0f, 1f);
            var height = Math.Max(MinBar, level * size.Height);
            var rect = new Rect(i * (barWidth + Gap), size.Height - height, barWidth, height);
            context.DrawRectangle(fill, null, new RoundedRect(rect, radius));
        }
    }

    private void Update()
    {
        var want = _source is not null && _enabled && _playing && _attached && IsEffectivelyVisible
            && _window?.WindowState != WindowState.Minimized;
        if (want == IsCapturing)
        {
            return;
        }
        if (want)
        {
            IsCapturing = true;
            _source!.Start();
            _last = Stopwatch.GetTimestamp();
            _timer ??= new DispatcherTimer(Frame, DispatcherPriority.Render, (_, _) => Tick());
            _timer.Start();
        }
        else
        {
            Stop();
        }
    }

    private void Tick()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_last, now).TotalSeconds;
        _last = now;
        Advance(elapsed);
    }

    private void Stop()
    {
        if (!IsCapturing)
        {
            return;
        }
        IsCapturing = false;
        _timer?.Stop();
        _source?.Stop();
        Array.Clear(_levels);
        InvalidateVisual();
    }
}
