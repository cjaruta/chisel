using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Chisel.Core.Focus;

namespace Chisel.App.Views.Widgets;

// The focus timer's dial: a ring that drains as the phase runs out, in the phase's color over a faint track (vector geometry, as
// crisp as the analog clock faces), with the phase name, the time left and the session dots inside. With the digital face, or when
// too small for a ring, only the text. Sizes come from FocusLayout.
//
// Cheap by design, like the stopwatch face: Show builds what is drawn, and only what changed (the arc and the text when the shown
// second moves, the pens when the theme or size changes). The text and dots are drawn once into a small cached bitmap, since
// drawing text allocates; Draw only replays the ring, the arc and that bitmap, so a frame allocates nothing. The owner calls Show
// when the shown second changes, so there are no animation frames at all.
public sealed class FocusDial : Control
{
    private static readonly Typeface TimeFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface LabelFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

    private FocusLayout _layout = FocusLayout.For(0, 0, FocusFace.Ring, false, 1);
    private double _fraction = -1;
    private string _time = "";
    private string _label = "";
    private int _done = -1;
    private int _total = -1;
    private Color _phaseColor;
    private IBrush? _text, _dim, _grid;
    private double _scale;

    private Pen? _trackPen, _arcPen;
    private StreamGeometry? _arc;
    private RenderTargetBitmap? _textImage;
    private Size _textSize;

    public FocusLayout Layout => _layout;

    // The time the dial shows (as drawn into its cached text).
    public string TimeText => _time;

    public void Show(FocusLayout layout, double fraction, string label, string time, int done, int total, Color phaseColor)
    {
        var text = Theming.ThemeService.Brush(this, "TextBrush");
        var dim = Theming.ThemeService.Brush(this, "DimTextBrush");
        var grid = Theming.ThemeService.Brush(this, "GridLineBrush");
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var look = !ReferenceEquals(text, _text) || !ReferenceEquals(dim, _dim) || !ReferenceEquals(grid, _grid)
            || layout != _layout || phaseColor != _phaseColor || scale != _scale;
        var phaseBrush = new ImmutableSolidColorBrush(phaseColor);
        if (look)
        {
            (_text, _dim, _grid, _layout, _phaseColor, _scale) = (text, dim, grid, layout, phaseColor, scale);
            _trackPen = layout.ShowRing ? new Pen(grid, layout.RingThickness) : null;
            _arcPen = layout.ShowRing ? new Pen(phaseBrush, layout.RingThickness, lineCap: PenLineCap.Round) : null;
        }
        if (look || fraction != _fraction)
        {
            _fraction = fraction;
            _arc = layout.ShowRing ? Arc((layout.Diameter - layout.RingThickness) / 2, fraction) : null;
        }
        if (look || time != _time || label != _label || done != _done || total != _total)
        {
            (_time, _label, _done, _total) = (time, label, done, total);
            BuildText(text, dim, phaseBrush);
        }
        InvalidateVisual();
    }

    // The part of the ring still to go: clockwise from the top, as a path around (0, 0). Null when nothing is left.
    private static StreamGeometry? Arc(double radius, double fraction)
    {
        if (fraction <= 0 || radius <= 0)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        var start = new Point(0, -radius);
        g.BeginFigure(start, false);
        if (fraction >= 0.9999)
        {
            // A whole circle as two half arcs (one arc cannot end where it starts).
            g.ArcTo(new Point(0, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            g.ArcTo(start, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        }
        else
        {
            var end = ClockFace.PointAt(new Point(0, 0), fraction * 360, radius);
            g.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise);
        }
        g.EndFigure(false);
        return geometry;
    }

    // The phase name, the time and the session dots, stacked and centered, drawn into the cached bitmap.
    private void BuildText(IBrush text, IBrush dim, IBrush phase)
    {
        _textImage?.Dispose();
        _textImage = null;
        var l = _layout;
        if (l.TimeFont <= 0)
        {
            return;
        }
        var culture = CultureInfo.CurrentUICulture;
        var timeText = new FormattedText(_time, culture, FlowDirection.LeftToRight, TimeFace, l.TimeFont, text);
        var labelText = l.ShowLabel ? new FormattedText(_label.ToUpperInvariant(), culture, FlowDirection.LeftToRight, LabelFace, l.LabelFont, phase) : null;
        var dots = l.ShowDots && _total > 0;
        var labelHeight = labelText is null ? 0 : l.LabelFont * FocusLayout.LineHeight;
        var timeHeight = l.TimeFont * FocusLayout.LineHeight;
        var dotsHeight = dots ? l.DotSize * 2 : 0;
        var dotsWidth = dots ? _total * l.DotSize + (_total - 1) * l.DotGap : 0;
        var width = Math.Ceiling(Math.Max(Math.Max(timeText.Width, labelText?.Width ?? 0), dotsWidth) + 2);
        var height = Math.Ceiling(labelHeight + timeHeight + dotsHeight);
        _textSize = new Size(width, height);
        _textImage = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)Math.Ceiling(width * _scale)), Math.Max(1, (int)Math.Ceiling(height * _scale))),
            new Vector(96 * _scale, 96 * _scale));
        using var context = _textImage.CreateDrawingContext();
        // Grayscale smoothing: subpixel text on a transparent bitmap leaves colored fringes.
        using var options = context.PushRenderOptions(new RenderOptions { TextRenderingMode = TextRenderingMode.Antialias });
        var mid = width / 2;
        if (labelText is not null)
        {
            context.DrawText(labelText, new Point(mid - labelText.Width / 2, (labelHeight - labelText.Height) / 2));
        }
        context.DrawText(timeText, new Point(mid - timeText.Width / 2, labelHeight + (timeHeight - timeText.Height) / 2));
        if (dots)
        {
            var d = l.DotSize;
            var ring = new Pen(dim, Math.Max(1, d * 0.18));
            var y = labelHeight + timeHeight + d;
            var x = mid - dotsWidth / 2 + d / 2;
            for (var i = 0; i < _total; i++)
            {
                var at = new Point(x + i * (d + l.DotGap), y);
                if (i < _done)
                {
                    context.DrawEllipse(phase, null, at, d / 2, d / 2);
                }
                else
                {
                    context.DrawEllipse(null, ring, at, d / 2 - 0.5, d / 2 - 0.5);
                }
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _textImage?.Dispose();
        _textImage = null;
        _text = null; // rebuilt on the next Show
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context) => Draw(context);

    public void Draw(DrawingContext context)
    {
        var l = _layout;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (l.ShowRing && _trackPen is not null)
        {
            var r = (l.Diameter - l.RingThickness) / 2;
            context.DrawEllipse(null, _trackPen, center, r, r);
            if (_arc is not null)
            {
                using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawGeometry(null, _arcPen, _arc);
                }
            }
        }
        if (_textImage is not null)
        {
            context.DrawImage(_textImage, new Rect(center.X - _textSize.Width / 2, center.Y - _textSize.Height / 2, _textSize.Width, _textSize.Height));
        }
    }
}
