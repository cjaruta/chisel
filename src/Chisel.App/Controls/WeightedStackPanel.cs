using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Chisel.App.Controls;

// Stacks children and shares the panel's length (height when vertical, width when horizontal) between them
// by their Weight, never giving a child less than its MinHeight (or MinWidth). Used for the layout's
// columns and for the widgets inside each column.
public class WeightedStackPanel : Panel
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<WeightedStackPanel, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<WeightedStackPanel, double>(nameof(Spacing));

    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<WeightedStackPanel, Control, double>("Weight", 1.0);

    static WeightedStackPanel()
    {
        AffectsMeasure<WeightedStackPanel>(OrientationProperty, SpacingProperty);
        AffectsParentMeasure<WeightedStackPanel>(WeightProperty);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public static double GetWeight(Control control) => control.GetValue(WeightProperty);
    public static void SetWeight(Control control, double value) => control.SetValue(WeightProperty, value);

    // Lengths for items sharing `available` by weight after taking out the spacing between them.
    // An item whose share would be under its minimum gets the minimum and the others share the rest.
    // If even the minimums don't fit, every item gets its minimum.
    public static double[] Distribute(double available, double spacing, IReadOnlyList<(double Weight, double Min)> items)
    {
        var lengths = new double[items.Count];
        if (items.Count == 0)
        {
            return lengths;
        }

        var space = Math.Max(0, available - spacing * (items.Count - 1));
        var fixedAtMin = new bool[items.Count];
        while (true)
        {
            var free = Enumerable.Range(0, items.Count).Where(i => !fixedAtMin[i]).ToList();
            var left = space - Enumerable.Range(0, items.Count).Where(i => fixedAtMin[i]).Sum(i => items[i].Min);
            var totalWeight = free.Sum(i => Math.Max(items[i].Weight, 0));
            var newlyFixed = false;
            foreach (var i in free)
            {
                var share = totalWeight > 0 ? Math.Max(left, 0) * Math.Max(items[i].Weight, 0) / totalWeight : 0;
                if (share < items[i].Min)
                {
                    fixedAtMin[i] = true;
                    newlyFixed = true;
                }
                lengths[i] = share;
            }
            if (!newlyFixed)
            {
                break;
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (fixedAtMin[i])
            {
                lengths[i] = items[i].Min;
            }
        }
        return lengths;
    }

    private bool Vertical => Orientation == Orientation.Vertical;

    private double[] Lengths(double available) =>
        Distribute(available, Spacing, Children.Select(c => (GetWeight(c), Vertical ? c.MinHeight : c.MinWidth)).ToList());

    protected override Size MeasureOverride(Size availableSize)
    {
        var along = Vertical ? availableSize.Height : availableSize.Width;
        var across = Vertical ? availableSize.Width : availableSize.Height;
        var spacing = Spacing * Math.Max(0, Children.Count - 1);

        if (double.IsInfinity(along))
        {
            // Unbounded: every child takes what it wants.
            double total = spacing, widest = 0;
            foreach (var child in Children)
            {
                child.Measure(Vertical ? new Size(across, double.PositiveInfinity) : new Size(double.PositiveInfinity, across));
                total += Vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
                widest = Math.Max(widest, Vertical ? child.DesiredSize.Width : child.DesiredSize.Height);
            }
            return Vertical ? new Size(widest, total) : new Size(total, widest);
        }

        var lengths = Lengths(along);
        double maxAcross = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(Vertical ? new Size(across, lengths[i]) : new Size(lengths[i], across));
            maxAcross = Math.Max(maxAcross, Vertical ? Children[i].DesiredSize.Width : Children[i].DesiredSize.Height);
        }
        var used = lengths.Sum() + spacing;
        if (double.IsInfinity(across))
        {
            across = maxAcross;
        }
        return Vertical ? new Size(across, used) : new Size(used, across);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var lengths = Lengths(Vertical ? finalSize.Height : finalSize.Width);
        double offset = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(Vertical
                ? new Rect(0, offset, finalSize.Width, lengths[i])
                : new Rect(offset, 0, lengths[i], finalSize.Height));
            offset += lengths[i] + Spacing;
        }
        return finalSize;
    }
}
