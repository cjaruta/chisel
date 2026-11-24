using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// The one analog clock renderer, driven by a ClockStyle (the named looks are ClockPresets, which are data). The still parts
// (rim, markers, numerals) are drawn once into a cached bitmap; the hands are cached shapes that are only rotated, so a
// steady-state frame allocates nothing. The cache is rebuilt when the size, the style or a theme brush changes.
// The same renderer draws every face (the named looks are data): a solid dial, a minute track, luminous markers, a 24-hour dial,
// and a chronograph with two sub-dials and a tachymeter ring. Its dial pieces are shared with the analog stopwatch.
// Angles are degrees clockwise from 12. Colors come from the theme unless the style sets one for that part.
public sealed class ClockFace : Control
{
    public static readonly StyledProperty<double> HourAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(HourAngle));
    public static readonly StyledProperty<double> MinuteAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(MinuteAngle));
    public static readonly StyledProperty<double> SecondAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(SecondAngle));
    public static readonly StyledProperty<double> Hour24AngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(Hour24Angle));
    public static readonly StyledProperty<ClockStyle> StyleProperty = AvaloniaProperty.Register<ClockFace, ClockStyle>(nameof(Style), ClockStyle.Default);

    // Space kept free around the face.
    public const double EdgeGap = 8;
    private const double Pad = 6;
    internal const double RoundedCorner = 0.3;
    // The chronograph's two sub-dials: how far from the center (as a share of the radius) and how big.
    private const double SubDialOffset = 0.42;
    private const double SubDialRadius = 0.2;

    // One hand, drawn as a stroked line or a filled shape, pointing up from the face center.
    internal sealed record Hand(Geometry? Shape, Pen? Stroke, Point From, Point To, IBrush Brush);

    private sealed record BuiltFor(ClockStyle Style, double Radius, double Scale, IBrush Text, IBrush Dim, IBrush Grid, IBrush Accent);

    private BuiltFor? _built;
    private RenderTargetBitmap? _dial;
    private Hand? _hour, _minute, _second, _subSecond, _subDay;
    private IBrush _centerBrush = null!;

    static ClockFace() => AffectsRender<ClockFace>(HourAngleProperty, MinuteAngleProperty, SecondAngleProperty, Hour24AngleProperty, StyleProperty);

    public double HourAngle { get => GetValue(HourAngleProperty); set => SetValue(HourAngleProperty, value); }
    public double MinuteAngle { get => GetValue(MinuteAngleProperty); set => SetValue(MinuteAngleProperty, value); }
    public double SecondAngle { get => GetValue(SecondAngleProperty); set => SetValue(SecondAngleProperty, value); }
    // The hour hand of a 24-hour dial (once round a day) and the day sub-dial of a chronograph.
    public double Hour24Angle { get => GetValue(Hour24AngleProperty); set => SetValue(Hour24AngleProperty, value); }
    public ClockStyle Style { get => GetValue(StyleProperty); set => SetValue(StyleProperty, value); }

    // Hour, minute and second.
    public int HandCount => 3;

    private IBrush Brush(string key) => Theming.ThemeService.Brush(this, key);

    // Radius of the drawn face: the smaller of width and height, less the gap.
    public double FaceRadius => Math.Min(Bounds.Width, Bounds.Height) / 2 - EdgeGap;

    // The face sits left, centered or right in the widget, vertically centered.
    public Point FaceCenter
    {
        get
        {
            var r = FaceRadius;
            var x = Style.Alignment switch
            {
                ClockAlignment.Left => EdgeGap + r,
                ClockAlignment.Right => Bounds.Width - EdgeGap - r,
                _ => Bounds.Width / 2,
            };
            return new Point(x, Bounds.Height / 2);
        }
    }

    public override void Render(DrawingContext context) => Draw(context);

    public void Draw(DrawingContext context)
    {
        var radius = FaceRadius;
        if (radius <= 4)
        {
            return;
        }
        var style = Style;
        EnsureCache(style, radius);
        var center = FaceCenter;
        var side = 2 * (radius + Pad);
        context.DrawImage(_dial!, new Rect(center.X - radius - Pad, center.Y - radius - Pad, side, side));

        if (style.Kind == DialKind.Chronograph)
        {
            var offset = radius * SubDialOffset;
            if (style.ShowSeconds)
            {
                DrawHand(context, new Point(center.X - offset, center.Y), SecondAngle, _subSecond!);
            }
            DrawHand(context, new Point(center.X + offset, center.Y), Hour24Angle, _subDay!);
        }
        DrawHand(context, center, style.Kind == DialKind.TwentyFourHour ? Hour24Angle : HourAngle, _hour!);
        DrawHand(context, center, MinuteAngle, _minute!);
        if (style.ShowSeconds)
        {
            DrawHand(context, center, SecondAngle, _second!);
        }
        var cap = Math.Max(3, style.HourThickness * 0.75);
        context.DrawEllipse(_centerBrush, null, center, cap, cap);
    }

    internal static void DrawHand(DrawingContext context, Point center, double degrees, Hand hand)
    {
        using (context.PushTransform(Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            if (hand.Stroke is { } pen)
            {
                context.DrawLine(pen, hand.From, hand.To);
            }
            else
            {
                context.DrawGeometry(hand.Brush, null, hand.Shape!);
            }
        }
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
        var shades = style.Colors;
        _built = new BuiltFor(style, radius, scale, text, dim, grid, accent);
        BuildDial(style, radius, scale, Part(shades.Dial, grid), Part(shades.Markers, dim), Part(shades.Numerals, text),
            style.DialFill is { } fill ? Part(fill, text) : null);

        var hourBrush = Part(shades.Hour, text);
        var minuteBrush = Part(shades.Minute, text);
        var secondBrush = Part(shades.Second, accent);
        _hour = BuildHand(style.Hands, radius * 0.5, style.HourThickness, hourBrush);
        _minute = BuildHand(style.Hands, radius * 0.75, style.MinuteThickness, minuteBrush);
        _second = new Hand(null, new Pen(secondBrush, style.SecondThickness, lineCap: PenLineCap.Round),
            new Point(0, radius * 0.15), new Point(0, -radius * 0.85), secondBrush);
        _centerBrush = style.ShowSeconds ? secondBrush : hourBrush;
        var sub = radius * SubDialRadius;
        _subSecond = new Hand(null, new Pen(secondBrush, 1.2, lineCap: PenLineCap.Round), new Point(0, sub * 0.2), new Point(0, -sub * 0.85), secondBrush);
        _subDay = new Hand(null, new Pen(hourBrush, 1.8, lineCap: PenLineCap.Round), new Point(0, sub * 0.2), new Point(0, -sub * 0.85), hourBrush);
    }

    internal static IBrush Part(string? hex, IBrush themed) =>
        hex is not null && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : themed;

    internal static Hand BuildHand(HandStyle style, double length, double thickness, IBrush brush)
    {
        var tail = length * 0.18;
        if (style == HandStyle.Line)
        {
            return new Hand(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), new Point(0, tail), new Point(0, -length), brush);
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var half = thickness / 2;
            switch (style)
            {
                case HandStyle.Tapered:
                    g.BeginFigure(new Point(-half * 1.4, tail), true);
                    g.LineTo(new Point(half * 1.4, tail));
                    g.LineTo(new Point(half * 0.25, -length));
                    g.LineTo(new Point(-half * 0.25, -length));
                    break;
                case HandStyle.Baton:
                    g.BeginFigure(new Point(-half, tail), true);
                    g.LineTo(new Point(half, tail));
                    g.LineTo(new Point(half, -length));
                    g.LineTo(new Point(-half, -length));
                    break;
                case HandStyle.Sword:
                    g.BeginFigure(new Point(0, -length), true);
                    g.LineTo(new Point(half * 1.6, -length * 0.72));
                    g.LineTo(new Point(half * 1.1, tail));
                    g.LineTo(new Point(-half * 1.1, tail));
                    g.LineTo(new Point(-half * 1.6, -length * 0.72));
                    break;
                default: // Leaf
                    g.BeginFigure(new Point(0, tail), true);
                    g.QuadraticBezierTo(new Point(thickness * 1.7, -length * 0.4), new Point(0, -length));
                    g.QuadraticBezierTo(new Point(-thickness * 1.7, -length * 0.4), new Point(0, tail));
                    break;
            }
            g.EndFigure(true);
        }
        return new Hand(geometry, null, default, default, brush);
    }

    // Distance from the center to the dial's edge in a direction (degrees clockwise from 12).
    internal static double Edge(DialShape shape, double radius, double degrees)
    {
        if (shape == DialShape.Round)
        {
            return radius;
        }
        var radians = degrees * Math.PI / 180;
        var reach = radius / Math.Max(Math.Abs(Math.Sin(radians)), Math.Abs(Math.Cos(radians)));
        return shape == DialShape.Square ? reach : Math.Min(reach, radius * (Math.Sqrt(2) - (Math.Sqrt(2) - 1) * RoundedCorner));
    }

    internal static Point PointAt(Point center, double degrees, double distance)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Sin(radians) * distance, center.Y - Math.Cos(radians) * distance);
    }

    private void BuildDial(ClockStyle style, double radius, double scale, IBrush rimBrush, IBrush markerBrush, IBrush numeralBrush, IBrush? fillBrush)
    {
        var side = 2 * (radius + Pad);
        _dial?.Dispose();
        _dial = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(side * scale), (int)Math.Ceiling(side * scale)), new Vector(96 * scale, 96 * scale));
        using var context = _dial.CreateDrawingContext();
        var center = new Point(side / 2, side / 2);

        PaintShape(context, style, center, radius, fillBrush, style.BorderWidth > 0 ? new Pen(rimBrush, style.BorderWidth) : null);

        var inset = style.BorderWidth / 2 + 3;
        // The tachymeter ring takes a band at the edge; everything else sits inside it.
        var band = style.Tachymeter ? radius * 0.13 : 0;
        if (style.Tachymeter)
        {
            PaintTachymeter(context, style, center, radius, inset, band, markerBrush);
        }
        var markerEdge = inset + band;

        if (style.MinuteTrack)
        {
            var thin = new Pen(markerBrush, 1);
            for (var i = 0; i < 60; i++)
            {
                if (i % 5 == 0 && style.Markers != MarkerStyle.None)
                {
                    continue;
                }
                var angle = i * 6;
                var outer = Edge(style.Shape, radius, angle) - markerEdge;
                context.DrawLine(thin, PointAt(center, angle, outer - radius * 0.035), PointAt(center, angle, outer));
            }
        }

        if (style.Markers != MarkerStyle.None)
        {
            var count = style.Kind == DialKind.TwentyFourHour ? 24 : 12;
            var step = 360.0 / count;
            for (var i = 0; i < count; i++)
            {
                var angle = i * step;
                var quarter = style.EmphasizeQuarters && i % (count / 4) == 0;
                var outer = Edge(style.Shape, radius, angle) - markerEdge;
                switch (style.Markers)
                {
                    case MarkerStyle.Dots:
                        var dot = quarter ? 4.0 : 2.5;
                        context.DrawEllipse(markerBrush, null, PointAt(center, angle, outer - dot), dot, dot);
                        break;
                    case MarkerStyle.Bars:
                        var barLength = radius * (quarter ? 0.17 : 0.1);
                        context.DrawLine(new Pen(markerBrush, quarter ? 5 : 3), PointAt(center, angle, outer - barLength), PointAt(center, angle, outer));
                        break;
                    case MarkerStyle.Lume:
                        PaintLume(context, center, angle, outer, radius, quarter, i == 0, markerBrush);
                        break;
                    default:
                        var tickLength = radius * (quarter ? 0.15 : 0.08);
                        context.DrawLine(new Pen(markerBrush, quarter ? 3 : 2, lineCap: PenLineCap.Round), PointAt(center, angle, outer - tickLength), PointAt(center, angle, outer));
                        break;
                }
            }
        }

        if (style.Kind == DialKind.Chronograph)
        {
            var sub = radius * SubDialRadius;
            var ring = new Pen(markerBrush, 1);
            var tick = new Pen(markerBrush, 1);
            foreach (var (dx, ticks) in new[] { (-SubDialOffset, 12), (SubDialOffset, 8) })
            {
                var at = new Point(center.X + radius * dx, center.Y);
                context.DrawEllipse(null, ring, at, sub, sub);
                for (var i = 0; i < ticks; i++)
                {
                    var angle = i * 360.0 / ticks;
                    context.DrawLine(tick, PointAt(at, angle, sub * 0.8), PointAt(at, angle, sub * 0.95));
                }
            }
        }

        // Numerals crowd a small dial, so they go and leave the marks and the hands (see ClockDialLayout).
        if (style.Numerals != NumeralStyle.None && ClockDialLayout.ShowsNumerals(radius * 2))
        {
            var twentyFour = style.Kind == DialKind.TwentyFourHour;
            var big = style.Numerals is NumeralStyle.Twelve or NumeralStyle.Four;
            var fontSize = Math.Max(9, radius * (twentyFour ? 0.15 : big ? 0.24 : 0.17));
            var reach = (style.Markers == MarkerStyle.None ? 0.77 : 0.68) - (style.Tachymeter ? 0.08 : 0);
            var labels = twentyFour ? ClockLabels.TwentyFour() : ClockLabels.For(style.Numerals);
            foreach (var label in labels)
            {
                var angle = twentyFour ? ClockLabels.AngleOf24(label.Hour) : ClockLabels.AngleOf(label.Hour);
                var text = new FormattedText(label.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), fontSize, numeralBrush);
                var at = PointAt(center, angle, Edge(style.Shape, radius, angle) * reach);
                context.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
            }
        }
    }

    // The dial's outline and solid color.
    internal static void PaintShape(DrawingContext context, ClockStyle style, Point center, double radius, IBrush? fill, Pen? rim)
    {
        if (fill is null && rim is null)
        {
            return;
        }
        if (style.Shape == DialShape.Round)
        {
            context.DrawEllipse(fill, rim, center, radius, radius);
        }
        else
        {
            var corner = style.Shape == DialShape.Square ? 0 : radius * RoundedCorner;
            context.DrawRectangle(fill, rim, new RoundedRect(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius), corner));
        }
    }

    // A luminous marker: a filled block (a triangle at 12), drawn upright at the top of a turned drawing surface.
    private static void PaintLume(DrawingContext context, Point center, double degrees, double outer, double radius, bool quarter, bool top, IBrush brush)
    {
        using (context.PushTransform(Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var width = Math.Max(3, radius * (quarter ? 0.075 : 0.05));
            var length = radius * (quarter ? 0.15 : 0.1);
            if (top)
            {
                var triangle = new StreamGeometry();
                using (var g = triangle.Open())
                {
                    g.BeginFigure(new Point(-width * 1.1, -outer + length), true);
                    g.LineTo(new Point(width * 1.1, -outer + length));
                    g.LineTo(new Point(0, -outer));
                    g.EndFigure(true);
                }
                context.DrawGeometry(brush, null, triangle);
            }
            else
            {
                context.DrawRectangle(brush, null, new RoundedRect(new Rect(-width / 2, -outer, width, length), width * 0.25));
            }
        }
    }

    // The speed scale of a chronograph: a ring line, and a small number and tick at each speed's angle.
    private static void PaintTachymeter(DrawingContext context, ClockStyle style, Point center, double radius, double inset, double band, IBrush brush)
    {
        var thin = new Pen(brush, 1);
        var fontSize = Math.Max(7, radius * 0.075);
        foreach (var mark in ClockTachymeter.Marks)
        {
            var edge = Edge(style.Shape, radius, mark.Angle) - inset;
            context.DrawLine(thin, PointAt(center, mark.Angle, edge - band * 0.25), PointAt(center, mark.Angle, edge));
            var text = new FormattedText(mark.Speed.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal), fontSize, brush);
            var at = PointAt(center, mark.Angle, edge - band * 0.62);
            context.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
        }
        context.DrawEllipse(null, thin, center, Edge(DialShape.Round, radius, 0) - inset - band, Edge(DialShape.Round, radius, 0) - inset - band);
    }
}
