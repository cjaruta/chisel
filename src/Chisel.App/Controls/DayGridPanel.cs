using System;
using Avalonia;
using Avalonia.Controls;

namespace Chisel.App.Controls;

// Lays children out on a 24-hour vertical day, by minutes from midnight.
// A child with an EndMinute is a box from StartMinute to EndMinute in its Column of ColumnCount.
// A child without one (NaN) is a full-width point item centered on StartMinute, like a marker line,
// kept inside the day at the edges.
public class DayGridPanel : Panel
{
    public const double MinutesPerDay = 24 * 60;

    public static readonly StyledProperty<double> PixelsPerMinuteProperty =
        AvaloniaProperty.Register<DayGridPanel, double>(nameof(PixelsPerMinute), 1.0);

    public static readonly AttachedProperty<double> StartMinuteProperty =
        AvaloniaProperty.RegisterAttached<DayGridPanel, Control, double>("StartMinute");

    public static readonly AttachedProperty<double> EndMinuteProperty =
        AvaloniaProperty.RegisterAttached<DayGridPanel, Control, double>("EndMinute", double.NaN);

    public static readonly AttachedProperty<int> ColumnProperty =
        AvaloniaProperty.RegisterAttached<DayGridPanel, Control, int>("Column");

    public static readonly AttachedProperty<int> ColumnCountProperty =
        AvaloniaProperty.RegisterAttached<DayGridPanel, Control, int>("ColumnCount", 1);

    static DayGridPanel()
    {
        AffectsMeasure<DayGridPanel>(PixelsPerMinuteProperty);
        AffectsParentArrange<DayGridPanel>(StartMinuteProperty, EndMinuteProperty, ColumnProperty, ColumnCountProperty);
    }

    public double PixelsPerMinute
    {
        get => GetValue(PixelsPerMinuteProperty);
        set => SetValue(PixelsPerMinuteProperty, value);
    }

    public static double GetStartMinute(Control control) => control.GetValue(StartMinuteProperty);
    public static void SetStartMinute(Control control, double value) => control.SetValue(StartMinuteProperty, value);
    public static double GetEndMinute(Control control) => control.GetValue(EndMinuteProperty);
    public static void SetEndMinute(Control control, double value) => control.SetValue(EndMinuteProperty, value);
    public static int GetColumn(Control control) => control.GetValue(ColumnProperty);
    public static void SetColumn(Control control, int value) => control.SetValue(ColumnProperty, value);
    public static int GetColumnCount(Control control) => control.GetValue(ColumnCountProperty);
    public static void SetColumnCount(Control control, int value) => control.SetValue(ColumnCountProperty, value);

    public static double DayHeight(double pixelsPerMinute) => MinutesPerDay * pixelsPerMinute;

    public static Rect ItemRect(double startMinute, double endMinute, int column, int columnCount,
        double width, double desiredHeight, double pixelsPerMinute)
    {
        var top = startMinute * pixelsPerMinute;
        if (double.IsNaN(endMinute))
        {
            // Keep labels at midnight fully inside the day instead of half outside it.
            var pointTop = Math.Clamp(top - desiredHeight / 2, 0, Math.Max(0, DayHeight(pixelsPerMinute) - desiredHeight));
            return new Rect(0, pointTop, width, desiredHeight);
        }

        var count = Math.Max(1, columnCount);
        var columnWidth = width / count;
        return new Rect(column * columnWidth, top, columnWidth, Math.Max(0, endMinute - startMinute) * pixelsPerMinute);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, DayHeight(PixelsPerMinute));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(ItemRect(GetStartMinute(child), GetEndMinute(child), GetColumn(child), GetColumnCount(child),
                finalSize.Width, child.DesiredSize.Height, PixelsPerMinute));
        }

        return finalSize;
    }
}
