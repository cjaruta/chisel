using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Chisel.App.Views.Widgets;

// A round clock face: tick marks (longer at 12, 3, 6, 9), hour, minute and second hands, a center dot.
// Fits the smaller of its width and height. Angles are degrees clockwise from 12. Colors come from the theme
// resources; the second hand uses the amber accent.
public sealed class ClockFace : Control
{
    public static readonly StyledProperty<double> HourAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(HourAngle));
    public static readonly StyledProperty<double> MinuteAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(MinuteAngle));
    public static readonly StyledProperty<double> SecondAngleProperty = AvaloniaProperty.Register<ClockFace, double>(nameof(SecondAngle));

    private const double Padding = 8;

    static ClockFace() => AffectsRender<ClockFace>(HourAngleProperty, MinuteAngleProperty, SecondAngleProperty);

    public double HourAngle { get => GetValue(HourAngleProperty); set => SetValue(HourAngleProperty, value); }
    public double MinuteAngle { get => GetValue(MinuteAngleProperty); set => SetValue(MinuteAngleProperty, value); }
    public double SecondAngle { get => GetValue(SecondAngleProperty); set => SetValue(SecondAngleProperty, value); }

    // Hour, minute and second.
    public int HandCount => 3;

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush ? brush : fallback;

    // Radius of the drawn circle: the smaller of width and height, less the padding.
    public double FaceRadius => Math.Min(Bounds.Width, Bounds.Height) / 2 - Padding;

    public override void Render(DrawingContext context)
    {
        var radius = FaceRadius;
        if (radius <= 4)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var tickBrush = Brush("DimTextBrush", Brushes.Gray);
        var handBrush = Brush("CurrentBlockOutlineBrush", Brushes.White);
        var secondBrush = Brush("EditAccentBrush", Brushes.Orange);
        var rim = new Pen(Brush("GridLineBrush", Brushes.DimGray), 1);

        context.DrawEllipse(null, rim, center, radius, radius);
        for (var i = 0; i < 60; i += 5)
        {
            var major = i % 15 == 0;
            var length = radius * (major ? 0.14 : 0.07);
            context.DrawLine(new Pen(tickBrush, major ? 1.5 : 1), PointAt(center, i * 6, radius - length), PointAt(center, i * 6, radius - 2));
        }

        context.DrawLine(new Pen(handBrush, 2.5, lineCap: PenLineCap.Round), center, PointAt(center, HourAngle, radius * 0.5));
        context.DrawLine(new Pen(handBrush, 1.5, lineCap: PenLineCap.Round), center, PointAt(center, MinuteAngle, radius * 0.75));
        context.DrawLine(new Pen(secondBrush, 1, lineCap: PenLineCap.Round), PointAt(center, SecondAngle + 180, radius * 0.15), PointAt(center, SecondAngle, radius * 0.85));
        context.DrawEllipse(secondBrush, null, center, 3, 3);
    }

    private static Point PointAt(Point center, double degrees, double distance)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Sin(radians) * distance, center.Y - Math.Cos(radians) * distance);
    }
}
