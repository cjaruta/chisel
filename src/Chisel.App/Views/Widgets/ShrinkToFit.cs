using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Chisel.App.Views.Widgets;

// Shows its child at its natural size, and only when the child is taller (or wider) than the room it is given scales it down
// evenly until it fits, so every button stays on screen and clickable instead of being cut off. The child is laid out at the
// width it will really have once scaled, so wrapping rows stay as they would be. At sizes that fit, nothing changes.
public sealed class ShrinkToFit : Decorator
{
    private double _scale = 1;

    public double Scale => _scale;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        var scale = 1.0;
        var desired = default(Size);
        // A few passes: scaling down gives the child more (unscaled) width, which can unwrap a row and need less scaling.
        for (var pass = 0; pass < 3; pass++)
        {
            var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / scale;
            child.Measure(new Size(width, double.PositiveInfinity));
            desired = child.DesiredSize;
            var next = Math.Min(1, Math.Min(Ratio(availableSize.Height, desired.Height), Ratio(availableSize.Width, desired.Width)));
            if (Math.Abs(next - scale) < 0.005)
            {
                break;
            }
            scale = next;
        }
        _scale = Math.Max(0.05, scale);
        return new Size(desired.Width * _scale, desired.Height * _scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        // Laid out in the unscaled room, then drawn (and hit tested) at the scale.
        child.Arrange(new Rect(0, 0, finalSize.Width / _scale, finalSize.Height / _scale));
        child.RenderTransformOrigin = RelativePoint.TopLeft;
        child.RenderTransform = _scale < 1 ? new ScaleTransform(_scale, _scale) : null;
        return finalSize;
    }

    private static double Ratio(double available, double desired) =>
        double.IsInfinity(available) || desired <= 0 ? 1 : available / desired;
}
