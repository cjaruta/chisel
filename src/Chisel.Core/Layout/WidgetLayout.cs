using System.Globalization;

namespace Chisel.Core.Layout;

// One widget in a column. HeightWeight is its share of the column's height, relative to its neighbours.
public sealed record WidgetPlacement(string WidgetId, double HeightWeight = 1);

// One column of the window. WidthWeight is its share of the window's width, relative to the other columns.
public sealed record LayoutColumn(double WidthWeight, IReadOnlyList<WidgetPlacement> Widgets);

// The window's arrangement: an ordered list of columns, each an ordered list of widgets.
// Immutable: Move and Remove return a new layout, so a changed layout is a new object.
public sealed class WidgetLayout
{
    // Smallest width or height weight a column or widget can have.
    public const double MinimumWeight = 0.1;

    public WidgetLayout(IEnumerable<LayoutColumn> columns)
    {
        Columns = columns.ToList();
    }

    public IReadOnlyList<LayoutColumn> Columns { get; }

    public IEnumerable<string> WidgetIds => Columns.SelectMany(c => c.Widgets).Select(w => w.WidgetId);

    public (int Column, int Index)? Find(string widgetId)
    {
        for (var c = 0; c < Columns.Count; c++)
        {
            for (var i = 0; i < Columns[c].Widgets.Count; i++)
            {
                if (Columns[c].Widgets[i].WidgetId == widgetId)
                {
                    return (c, i);
                }
            }
        }
        return null;
    }

    // Moves a widget into the gap at index of a column: 0 is above the first widget, Count below the last,
    // counted as the column is before the move (moving down within a column adjusts for the widget's own gap).
    // Columns are never added or removed, so a column can be left empty. The widget keeps its height weight,
    // and the columns it leaves and joins are rescaled to average 1 (NormalizeHeights).
    // Moving a widget onto its own position returns this same layout.
    public WidgetLayout Move(string widgetId, int columnIndex, int index)
    {
        var (fromColumn, fromIndex) = Find(widgetId)
            ?? throw new ArgumentException($"'{widgetId}' is not in the layout.", nameof(widgetId));
        if (columnIndex < 0 || columnIndex >= Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "There is no such column.");
        }

        var target = Math.Clamp(index, 0, Columns[columnIndex].Widgets.Count);
        if (columnIndex == fromColumn)
        {
            if (target == fromIndex || target == fromIndex + 1)
            {
                return this;
            }
            if (target > fromIndex)
            {
                target--;
            }
        }

        var columns = Columns.Select(c => c.Widgets.ToList()).ToList();
        var placement = columns[fromColumn][fromIndex];
        columns[fromColumn].RemoveAt(fromIndex);
        columns[columnIndex].Insert(target, placement);

        return new WidgetLayout(Columns.Select((column, i) =>
        {
            var moved = column with { Widgets = columns[i] };
            return i == fromColumn || i == columnIndex ? NormalizeHeights(moved) : moved;
        }));
    }

    // Makes a widget taller (positive delta) or shorter (negative) by moving height weight between it and the
    // widget right below it in its column. delta and minimumFraction are fractions of the column's height
    // (the UI divides pixels by the column's height). Neither widget goes below minimumFraction; the move stops
    // there rather than pushing anything else. The column's weight total and every other widget are unchanged.
    // Returns this same layout when nothing changes. The widget must have a widget below it.
    public WidgetLayout Resize(string widgetId, double delta, double minimumFraction)
    {
        var (column, index) = Find(widgetId)
            ?? throw new ArgumentException($"'{widgetId}' is not in the layout.", nameof(widgetId));
        var widgets = Columns[column].Widgets;
        if (index + 1 >= widgets.Count)
        {
            throw new ArgumentException($"'{widgetId}' has no widget below it to resize against.", nameof(widgetId));
        }

        var total = widgets.Sum(w => w.HeightWeight);
        var minimum = minimumFraction * total;
        var above = widgets[index];
        var below = widgets[index + 1];

        // How far the weight can move: down to the minimum on either side.
        var most = below.HeightWeight - minimum;
        var least = minimum - above.HeightWeight;
        var change = least > most ? 0 : Math.Clamp(delta * total, Math.Min(least, 0), Math.Max(most, 0));
        if (Math.Abs(change) < 1e-12)
        {
            return this;
        }

        var resized = widgets.ToList();
        resized[index] = above with { HeightWeight = above.HeightWeight + change };
        resized[index + 1] = below with { HeightWeight = below.HeightWeight - change };
        return new WidgetLayout(Columns.Select((c, i) => i == column ? c with { Widgets = resized } : c));
    }

    // Rescales a column's height weights so they average 1, keeping their proportions
    // (rounded to 3 decimals, never below MinimumWeight).
    public static LayoutColumn NormalizeHeights(LayoutColumn column)
    {
        var total = column.Widgets.Sum(w => w.HeightWeight);
        if (column.Widgets.Count == 0 || total <= 0)
        {
            return column;
        }
        var scale = column.Widgets.Count / total;
        return column with
        {
            Widgets = column.Widgets
                .Select(w => w with { HeightWeight = Math.Max(MinimumWeight, Math.Round(w.HeightWeight * scale, 3)) })
                .ToList(),
        };
    }

    public WidgetLayout Remove(string widgetId)
    {
        var columns = Columns.Select(c => c.Widgets.Where(w => w.WidgetId != widgetId).ToList()).ToList();
        return Build(Columns.Select(c => c.WidthWeight).ToList(), columns);
    }

    // A compact text form for tests and logs, like "1.3[schedule:1] | 1[tasks:1, notes:1.2]".
    public string Describe() =>
        string.Join(" | ", Columns.Select(c =>
            $"{Number(c.WidthWeight)}[{string.Join(", ", c.Widgets.Select(w => $"{w.WidgetId}:{Number(w.HeightWeight)}"))}]"));

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static WidgetLayout Build(IReadOnlyList<double> widths, IReadOnlyList<List<WidgetPlacement>> columns) =>
        new(columns.Select((widgets, i) => new LayoutColumn(widths[i], widgets)).Where(c => c.Widgets.Count > 0));
}
