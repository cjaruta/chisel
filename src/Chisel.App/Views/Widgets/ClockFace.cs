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
// Angles are degrees clockwise from 12. Colors come from the theme unless the style sets one for that part.
public sealed class ClockFace : Control
{
    public static readonly StyledProperty<double> HourAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(HourAngle));
    public static readonly StyledProperty<double> MinuteAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(MinuteAngle));
    public static readonly StyledProperty<double> SecondAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(SecondAngle));
    public static readonly StyledProperty<ClockStyle> StyleProperty = AvaloniaProperty.Register<ClockFace, ClockStyle>(nameof(Style), ClockStyle.Default);

    // Space kept free around the face.
    public const double EdgeGap = 8;
    private const double Pad = 6;
    private const double RoundedCorner = 0.3;

    // One hand, drawn as a stroked line or a filled shape, pointing up from the face center.
    private sealed record Hand(Geometry? Shape, Pen? Stroke, Point From, Point To, IBrush Brush);

    private sealed record BuiltFor(ClockStyle Style, double Radius, double Scale, IBrush Text, IBrush Dim, IBrush Grid, IBrush Accent);

    private BuiltFor? _built;
    private RenderTargetBitmap? _dial;
    private Hand? _hour, _minute, _second;
    private IBrush _centerBrush = null!;

    static ClockFace() => AffectsRender<ClockFace>(HourAngleProperty, MinuteAngleProperty, SecondAngleProperty, StyleProperty);

    public double HourAngle { get => GetValue(HourAngleProperty); set => SetValue(HourAngleProperty, value); }
    public double MinuteAngle { get => GetValue(MinuteAngleProperty); set => SetValue(MinuteAngleProperty, value); }
    public double SecondAngle { get => GetValue(SecondAngleProperty); set => SetValue(SecondAngleProperty, value); }
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

        DrawHand(context, center, HourAngle, _hour!);
        DrawHand(context, center, MinuteAngle, _minute!);
        if (style.ShowSeconds)
        {
            DrawHand(context, center, SecondAngle, _second!);
        }
        var cap = Math.Max(3, style.HourThickness * 0.75);
        context.DrawEllipse(_centerBrush, null, center, cap, cap);
    }

    private static void DrawHand(DrawingContext context, Point center, double degrees, Hand hand)
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
        BuildDial(style, radius, scale, Part(shades.Dial, grid), Part(shades.Markers, dim), Part(shades.Numerals, text));

        var hourBrush = Part(shades.Hour, text);
        var minuteBrush = Part(shades.Minute, text);
        var secondBrush = Part(shades.Second, accent);
        _hour = BuildHand(style.Hands, radius * 0.5, style.HourThickness, hourBrush);
        _minute = BuildHand(style.Hands, radius * 0.75, style.MinuteThickness, minuteBrush);
        _second = new Hand(null, new Pen(secondBrush, style.SecondThickness, lineCap: PenLineCap.Round),
            new Point(0, radius * 0.15), new Point(0, -radius * 0.85), secondBrush);
        _centerBrush = style.ShowSeconds ? secondBrush : hourBrush;
    }

    private static IBrush Part(string? hex, IBrush themed) =>
        hex is not null && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : themed;

    private static Hand BuildHand(HandStyle style, double length, double thickness, IBrush brush)
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
    private static double Edge(DialShape shape, double radius, double degrees)
    {
        if (shape == DialShape.Round)
        {
            return radius;
        }
        var radians = degrees * Math.PI / 180;
        var reach = radius / Math.Max(Math.Abs(Math.Sin(radians)), Math.Abs(Math.Cos(radians)));
        return shape == DialShape.Square ? reach : Math.Min(reach, radius * (Math.Sqrt(2) - (Math.Sqrt(2) - 1) * RoundedCorner));
    }

    private static Point PointAt(Point center, double degrees, double distance)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Sin(radians) * distance, center.Y - Math.Cos(radians) * distance);
    }

    private void BuildDial(ClockStyle style, double radius, double scale, IBrush rimBrush, IBrush markerBrush, IBrush numeralBrush)
    {
        var side = 2 * (radius + Pad);
        _dial?.Dispose();
        _dial = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(side * scale), (int)Math.Ceiling(side * scale)), new Vector(96 * scale, 96 * scale));
        using var context = _dial.CreateDrawingContext();
        var center = new Point(side / 2, side / 2);

        if (style.BorderWidth > 0)
        {
            var rim = new Pen(rimBrush, style.BorderWidth);
            if (style.Shape == DialShape.Round)
            {
                context.DrawEllipse(null, rim, center, radius, radius);
            }
            else
            {
                var corner = style.Shape == DialShape.Square ? 0 : radius * RoundedCorner;
                context.DrawRectangle(null, rim, new RoundedRect(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius), corner));
            }
        }

        var inset = style.BorderWidth / 2 + 3;
        if (style.Markers != MarkerStyle.None)
        {
            for (var i = 0; i < 12; i++)
            {
                var angle = i * 30;
                var quarter = style.EmphasizeQuarters && i % 3 == 0;
                var outer = Edge(style.Shape, radius, angle) - inset;
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
                    default:
                        var tickLength = radius * (quarter ? 0.15 : 0.08);
                        context.DrawLine(new Pen(markerBrush, quarter ? 3 : 2, lineCap: PenLineCap.Round), PointAt(center, angle, outer - tickLength), PointAt(center, angle, outer));
                        break;
                }
            }
        }

        if (style.Numerals != NumeralStyle.None)
        {
            var big = style.Numerals is NumeralStyle.Twelve or NumeralStyle.Four;
            var fontSize = Math.Max(9, radius * (big ? 0.24 : 0.17));
            var reach = style.Markers == MarkerStyle.None ? 0.77 : 0.68;
            foreach (var label in ClockLabels.For(style.Numerals))
            {
                var angle = ClockLabels.AngleOf(label.Hour);
                var text = new FormattedText(label.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), fontSize, numeralBrush);
                var at = PointAt(center, angle, Edge(style.Shape, radius, angle) * reach);
                context.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
            }
        }
    }
}
