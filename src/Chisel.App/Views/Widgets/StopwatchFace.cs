using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// The analog stopwatch: a round dial in the clock's look (a ClockStyle: fill, rim, markers, colors), a seconds hand that sweeps once
// a minute, a small sub-dial for the minutes (one turn per 30 minutes) and, under it in the tool, the digital readout.
//
// Cheap by design. The still parts (rim, 60 marks, numbers, the sub-dial) are drawn once into a cached bitmap and the two hands are
// cached shapes that are only rotated, so a frame allocates nothing. Frames are only asked for while the stopwatch is running and
// the face is on screen and its window is not minimized, and then at about 30 a second; a stopped or hidden face costs nothing.
public sealed class StopwatchFace : Control
{
    public static readonly StyledProperty<ClockStyle> StyleProperty = AvaloniaProperty.Register<StopwatchFace, ClockStyle>(nameof(Style), ClockStyle.Default);

    private const double Pad = 6;
    private static readonly Typeface NumberFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface SubFace = new(FontFamily.Default);
    private static readonly TimeSpan FrameGap = TimeSpan.FromMilliseconds(30);

    private sealed record BuiltFor(ClockStyle Style, double Radius, StopwatchMetrics Metrics, double Scale, IBrush Text, IBrush Dim, IBrush Grid, IBrush Accent);

    private readonly Action<TimeSpan> _frame;
    private BuiltFor? _built;
    private RenderTargetBitmap? _dial;
    private ClockFace.Hand? _seconds, _minutes;
    private IBrush _capBrush = null!;
    private bool _running;
    private bool _animating;
    private TimeSpan _lastFrame;
    private IDisposable? _windowState;

    static StopwatchFace() => AffectsRender<StopwatchFace>(StyleProperty);

    public StopwatchFace()
    {
        _frame = OnFrame; // made once: asking for the next frame allocates nothing
    }

    public ClockStyle Style { get => GetValue(StyleProperty); set => SetValue(StyleProperty, value); }

    // The elapsed time in seconds, read at every frame (the owner keeps the state; this holds none).
    public Func<double>? ElapsedSource { get; set; }

    // Whether frames are being asked for right now.
    public bool IsAnimating => _animating;

    // Whether the stopwatch is running: the owner sets this, and it is the only thing that starts frames.
    public bool IsRunning
    {
        get => _running;
        set
        {
            if (_running == value)
            {
                return;
            }
            _running = value;
            UpdateAnimation();
            InvalidateVisual();
        }
    }

    // The elapsed time, to read aloud.
    public static string Describe(TimeSpan elapsed) => $"Stopwatch, {ClockToolsFormat.Stopwatch(elapsed)}";

    // The face is the largest circle in the space, kept off the edges; the level of detail follows its diameter.
    public double FaceRadius => (Math.Min(Bounds.Width, Bounds.Height) - 2 * StopwatchLayout.EdgeGap) / 2;

    public StopwatchMetrics Metrics => _built?.Metrics ?? StopwatchLayout.For(2 * Math.Max(0, FaceRadius));

    private IBrush Brush(string key) => Theming.ThemeService.Brush(this, key);

    // Frames -----------------------------------------------------------------------------------------------------------

    private bool ShouldAnimate => _running && VisualRoot is not null && IsEffectivelyVisible
        && !(TopLevel.GetTopLevel(this) is Window { WindowState: WindowState.Minimized });

    private void UpdateAnimation()
    {
        var want = ShouldAnimate;
        if (want && !_animating)
        {
            _animating = true;
            if (TopLevel.GetTopLevel(this) is { } top)
            {
                top.RequestAnimationFrame(_frame);
            }
        }
        else if (!want)
        {
            _animating = false;
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_animating)
        {
            return;
        }
        if (!ShouldAnimate)
        {
            _animating = false;
            return;
        }
        if (time - _lastFrame >= FrameGap)
        {
            _lastFrame = time;
            InvalidateVisual();
        }
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.RequestAnimationFrame(_frame);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _windowState?.Dispose();
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _windowState = window.GetObservable(Window.WindowStateProperty).Subscribe(new StateObserver(this));
        }
        UpdateAnimation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animating = false;
        _windowState?.Dispose();
        _windowState = null;
        _dial?.Dispose();
        _dial = null;
        _built = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible))
        {
            UpdateAnimation();
        }
    }

    private sealed class StateObserver(StopwatchFace face) : IObserver<WindowState>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value) => face.UpdateAnimation();
    }

    // Drawing ----------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context) => Draw(context);

    public void Draw(DrawingContext context)
    {
        var radius = FaceRadius;
        if (radius * 2 < StopwatchLayout.MinDiameter)
        {
            return;
        }
        var style = Style;
        EnsureCache(style, radius);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var side = 2 * (radius + Pad);
        context.DrawImage(_dial!, new Rect(center.X - radius - Pad, center.Y - radius - Pad, side, side));

        var angles = StopwatchDial.Angles(ElapsedSource?.Invoke() ?? 0);
        var m = _built!.Metrics;
        if (m.SubDial)
        {
            ClockFace.DrawHand(context, new Point(center.X, center.Y + m.SubOffset), angles.Minutes, _minutes!);
        }
        ClockFace.DrawHand(context, center, angles.Seconds, _seconds!);
        var cap = Math.Max(2, m.Stroke(style.SecondThickness) * 2.2);
        context.DrawEllipse(_capBrush, null, center, cap, cap);
    }

    private void EnsureCache(ClockStyle style, double radius)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var text = Brush("TextBrush");
        var dim = Brush("DimTextBrush");
        var grid = Brush("GridLineBrush");
        var accent = Brush("AccentBrush");
        if (_built is { } b && _dial is not null && b.Style == style && b.Radius == radius && b.Scale == scale
            && ReferenceEquals(b.Text, text) && ReferenceEquals(b.Dim, dim) && ReferenceEquals(b.Grid, grid) && ReferenceEquals(b.Accent, accent))
        {
            return;
        }
        var m = StopwatchLayout.For(2 * radius);
        _built = new BuiltFor(style, radius, m, scale, text, dim, grid, accent);
        var shades = style.Colors;
        var secondBrush = ClockFace.Part(shades.Second, accent);
        var minuteBrush = ClockFace.Part(shades.Hour, text);
        BuildDial(style, radius, m, scale, ClockFace.Part(shades.Dial, grid), ClockFace.Part(shades.Markers, dim), ClockFace.Part(shades.Numerals, text),
            style.DialFill is { } fill ? ClockFace.Part(fill, text) : null);
        _seconds = new ClockFace.Hand(null, new Pen(secondBrush, m.Stroke(Math.Max(1.2, style.SecondThickness)), lineCap: PenLineCap.Round),
            new Point(0, m.SecondTail), new Point(0, -m.SecondLength), secondBrush);
        _minutes = new ClockFace.Hand(null, new Pen(minuteBrush, m.Stroke(Math.Max(1.6, style.MinuteThickness * 0.6)), lineCap: PenLineCap.Round),
            new Point(0, m.MinuteTail), new Point(0, -m.MinuteLength), minuteBrush);
        _capBrush = secondBrush;
    }

    private void BuildDial(ClockStyle style, double radius, StopwatchMetrics m, double scale, IBrush rimBrush, IBrush markerBrush, IBrush numeralBrush, IBrush? fillBrush)
    {
        var side = 2 * (radius + Pad);
        _dial?.Dispose();
        _dial = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(side * scale), (int)Math.Ceiling(side * scale)), new Vector(96 * scale, 96 * scale));
        using var context = _dial.CreateDrawingContext();
        var center = new Point(side / 2, side / 2);
        ClockFace.PaintShape(context, style, center, radius, fillBrush, style.BorderWidth > 0 ? new Pen(rimBrush, m.Stroke(style.BorderWidth)) : null);

        // The marks: a longer one every five, and the minor ones only when there is room for them.
        var inset = m.Stroke(style.BorderWidth) / 2 + m.TickInset;
        var thin = new Pen(markerBrush, m.MinorTickWidth, lineCap: PenLineCap.Round);
        var thick = new Pen(markerBrush, m.MajorTickWidth * (style.Markers == MarkerStyle.Bars ? 1.6 : 1), lineCap: PenLineCap.Round);
        for (var i = 0; i < 60; i++)
        {
            var five = i % 5 == 0;
            if (!five && !m.MinorTicks)
            {
                continue;
            }
            var angle = i * 6;
            var outer = ClockFace.Edge(style.Shape, radius, angle) - inset;
            if (style.Markers == MarkerStyle.Dots)
            {
                var dot = five ? m.MajorTickLength * 0.4 + 0.6 : m.MinorTickLength * 0.4 + 0.6;
                context.DrawEllipse(markerBrush, null, ClockFace.PointAt(center, angle, outer - dot), dot, dot);
            }
            else
            {
                var length = five ? m.MajorTickLength : m.MinorTickLength;
                context.DrawLine(five ? thick : thin, ClockFace.PointAt(center, angle, outer - length), ClockFace.PointAt(center, angle, outer));
            }
        }

        // The seconds: the four main positions (60, 15, 30, 45) when medium, every five when large; none when smaller.
        if (m.Numbers != StopwatchNumbers.None)
        {
            var step = m.Numbers == StopwatchNumbers.All ? 5 : 15;
            for (var s = step; s <= 60; s += step)
            {
                var angle = s % 60 * 6;
                var label = new FormattedText(s.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    NumberFace, m.NumberFont, numeralBrush);
                var at = ClockFace.PointAt(center, angle, ClockFace.Edge(style.Shape, radius, angle) * StopwatchLayout.NumberRadiusFactor);
                context.DrawText(label, new Point(at.X - label.Width / 2, at.Y - label.Height / 2));
            }
        }

        // The minutes sub-dial below the middle (large only): 30 marks (one turn is half an hour), numbered 10, 20 and 30.
        if (m.SubDial)
        {
            var sub = m.SubRadius;
            var subCenter = new Point(center.X, center.Y + m.SubOffset);
            context.DrawEllipse(null, thin, subCenter, sub, sub);
            for (var k = 0; k < 30; k++)
            {
                var angle = k * 12;
                var longMark = k % 5 == 0;
                context.DrawLine(thin, ClockFace.PointAt(subCenter, angle, sub * (longMark ? 0.74 : 0.86)), ClockFace.PointAt(subCenter, angle, sub * 0.95));
            }
            foreach (var k in new[] { 10, 20, 30 })
            {
                var label = new FormattedText(k.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    SubFace, m.SubFont, numeralBrush);
                var at = ClockFace.PointAt(subCenter, k % 30 * 12, sub * 0.52);
                context.DrawText(label, new Point(at.X - label.Width / 2, at.Y - label.Height / 2));
            }
        }
    }
}
