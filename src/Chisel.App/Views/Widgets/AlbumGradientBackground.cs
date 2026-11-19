using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Chisel.App.Theming;
using Chisel.Core.Theme;

namespace Chisel.App.Views.Widgets;

// The soft three-color gradient behind the media widget. It paints a rounded rectangle and takes no pointer input.
//
// Drift: while Animate is on (and the control is on screen, visible and the window is not minimized) the gradient's end
// points and middle stop move a few percent and back over LoopSeconds, eased and looping. Gradient stops cannot be animated
// in Avalonia, and every gradient draw builds a new shader, so each palette is rendered once into one small bitmap per step
// of the loop and a frame just stretches the right one: nothing is allocated while drawing. The timer ticks at 20 per second
// at most and only redraws when the step changes (twice a second) or while a cross-fade runs.
// Cross-fade: a new palette is drawn over the old one with rising opacity for FadeSeconds.
public sealed class AlbumGradientBackground : Control
{
    public const double LoopSeconds = 24;
    public const double FadeSeconds = 0.5;
    private const int Steps = 48;
    private const int FrameWidth = 64;
    private const int FrameHeight = 32;
    private const double Reach = 0.10;
    private const double MiddleSwing = 0.06;
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(50);
    private static readonly Rect Source = new(0, 0, FrameWidth, FrameHeight);

    // One palette, pre-rendered: one bitmap per step of the drift loop.
    private sealed class Layer : IDisposable
    {
        public readonly RenderTargetBitmap[] Frames = new RenderTargetBitmap[Steps];
        public readonly Rgb[] Stops;

        public Layer(IReadOnlyList<Rgb> colors)
        {
            Stops = Normalize(colors);
            var area = new Rect(0, 0, FrameWidth, FrameHeight);
            for (var i = 0; i < Steps; i++)
            {
                var e = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Steps);
                ImmutableGradientStop[] stops =
                [
                    new(0, Stops[0].ToColor()),
                    new(0.5 + MiddleSwing * (2 * e - 1), Stops[1].ToColor()),
                    new(1, Stops[2].ToColor()),
                ];
                var brush = new ImmutableLinearGradientBrush(stops,
                    startPoint: new RelativePoint(Reach * e, 0, RelativeUnit.Relative),
                    endPoint: new RelativePoint(1 - Reach * e, 1, RelativeUnit.Relative));
                Frames[i] = new RenderTargetBitmap(new PixelSize(FrameWidth, FrameHeight));
                using var context = Frames[i].CreateDrawingContext();
                context.DrawRectangle(brush, null, area);
            }
        }

        public static Rgb[] Normalize(IReadOnlyList<Rgb> colors) => colors.Count switch
        {
            0 => [default, default, default],
            1 => [colors[0], colors[0], colors[0]],
            2 => [colors[0], colors[0].Mix(colors[1], 0.5), colors[1]],
            _ => [colors[0], colors[1], colors[2]],
        };

        public void Dispose()
        {
            foreach (var frame in Frames)
            {
                frame.Dispose();
            }
        }
    }

    private Layer? _to;
    private Layer? _from;
    private double _fade = 1;
    private double _clock;
    private int _step;
    private bool _animate;
    private bool _attached;
    private long _last;
    private DispatcherTimer? _timer;
    private Window? _window;
    private CornerRadius _radius;

    public AlbumGradientBackground()
    {
        IsHitTestVisible = false;
    }

    public CornerRadius CornerRadius
    {
        get => _radius;
        set
        {
            _radius = value;
            InvalidateVisual();
        }
    }

    // Whether the drift may run (the view sets it from the playing state). Off, the gradient holds where it is.
    public bool Animate
    {
        get => _animate;
        set
        {
            _animate = value;
            Update();
        }
    }

    public bool IsRunning => _timer?.IsEnabled == true;

    public bool IsFading => _from is not null;

    // The three stops being shown (or fading in).
    public IReadOnlyList<Rgb> Colors => _to?.Stops ?? [];

    // Drops the rendered bitmaps (when the widget leaves the window); SetColors builds them again.
    public void Clear()
    {
        _to?.Dispose();
        _from?.Dispose();
        _to = _from = null;
        _fade = 1;
        Update();
    }

    // The step of the drift loop being drawn (0 to 47).
    public int Phase => _step;

    // Shows a palette; with fade, from the one shown now (when there is one).
    public void SetColors(IReadOnlyList<Rgb> colors, bool fade)
    {
        var colorsNow = Layer.Normalize(colors);
        if (_to is not null && _to.Stops.SequenceEqual(colorsNow))
        {
            return;
        }
        var layer = new Layer(colorsNow);
        _from?.Dispose();
        if (fade && _to is not null)
        {
            _from = _to;
            _fade = 0;
        }
        else
        {
            _to?.Dispose();
            _from = null;
            _fade = 1;
        }
        _to = layer;
        InvalidateVisual();
        Update();
    }

    // Moves the drift and the cross-fade on by the given time; the timer calls it, and tests call it directly.
    public void Advance(double seconds)
    {
        var changed = false;
        if (_from is not null)
        {
            _fade += seconds / FadeSeconds;
            if (_fade >= 1)
            {
                _fade = 1;
                _from.Dispose();
                _from = null;
            }
            changed = true;
        }
        if (_animate)
        {
            _clock = (_clock + seconds) % LoopSeconds;
            var step = (int)(_clock / LoopSeconds * Steps) % Steps;
            if (step != _step)
            {
                _step = step;
                changed = true;
            }
        }
        if (changed)
        {
            InvalidateVisual();
        }
        Update();
    }

    public override void Render(DrawingContext context) => DrawTo(context, Bounds.Size);

    // The single draw path (the control and the tests).
    public void DrawTo(DrawingContext context, Size size)
    {
        if (_to is null || size.Width < 1 || size.Height < 1)
        {
            return;
        }
        var area = new Rect(size);
        if (_radius == default)
        {
            Paint(context, area);
            return;
        }
        using (context.PushClip(new RoundedRect(area, _radius.TopLeft, _radius.TopRight, _radius.BottomRight, _radius.BottomLeft)))
        {
            Paint(context, area);
        }
    }

    private void Paint(DrawingContext context, Rect area)
    {
        if (_from is null)
        {
            context.DrawImage(_to!.Frames[_step], Source, area);
            return;
        }
        context.DrawImage(_from.Frames[_step], Source, area);
        var eased = _fade * _fade * (3 - 2 * _fade);
        using (context.PushOpacity(eased))
        {
            context.DrawImage(_to!.Frames[_step], Source, area);
        }
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
        if (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible))
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

    // The timer runs only while there is something to move: the drift while animating, or a cross-fade in progress.
    // Re-checks whether it is on screen; for when an ancestor was hidden or shown (that raises no event here).
    public void RefreshVisibility() => Update();

    private void Update()
    {
        var onScreen = _attached && IsEffectivelyVisible && _window?.WindowState != WindowState.Minimized;
        var want = onScreen && (_animate || _from is not null);
        if (want == IsRunning)
        {
            return;
        }
        if (want)
        {
            _last = Stopwatch.GetTimestamp();
            _timer ??= new DispatcherTimer(Frame, DispatcherPriority.Render, (_, _) => Tick());
            _timer.Start();
        }
        else
        {
            _timer?.Stop();
        }
    }

    private void Tick()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Math.Min(Stopwatch.GetElapsedTime(_last, now).TotalSeconds, 0.25);
        _last = now;
        Advance(elapsed);
    }
}
