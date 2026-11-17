using System.Globalization;

namespace Chisel.Core.Layout;

// One widget in a column. HeightWeight is its share of the column's height, relative to its neighbours.
public sealed record WidgetPlacement(string WidgetId, double HeightWeight = 1);

// One column of the window. WidthWeight is its share of the window's width, relative to the other columns.
public sealed record LayoutColumn(double WidthWeight, IReadOnlyList<WidgetPlacement> Widgets);

// The window's arrangement: an ordered list of columns, each an ordered list of widgets, plus the widgets
// the user took off the dashboard (hidden; their data is untouched). Remembering hidden widgets is what tells
// a removed widget (stays hidden) apart from a brand-new one (gets placed; see WidgetLayoutNormalizer).
// Immutable: every change returns a new layout, so a changed layout is a new object.
public sealed class WidgetLayout
{
    // Smallest width or height weight a column or widget can have.
    public const double MinimumWeight = 0.1;

    public WidgetLayout(IEnumerable<LayoutColumn> columns, IEnumerable<string>? hiddenWidgetIds = null)
    {
        Columns = columns.ToList();
        HiddenWidgetIds = hiddenWidgetIds?.ToList() ?? new List<string>();
    }

    public IReadOnlyList<LayoutColumn> Columns { get; }

    // Widgets taken off the dashboard, in the order they should be listed.
    public IReadOnlyList<string> HiddenWidgetIds { get; }

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

        return With(Columns.Select((column, i) =>
        {
            var moved = column with { Widgets = columns[i] };
            return i == fromColumn || i == columnIndex ? NormalizeHeights(moved) : moved;
        }));
    }

    // Moves a widget into a new column placed at a gap between columns: 0 is left of the first column,
    // Count right of the last (counted as the layout is before the move). The widget fills the new column
    // (height weight 1), the new column gets the average of the current column widths, and the column it
    // left is rescaled (and kept, even if empty; see RemoveEmptyColumns).
    // A widget that is alone in its column moves its whole column instead (keeping its width), so nothing is
    // left empty; next to its own column that is a no-op and returns this same layout.
    // Rejects an unknown widget, a gap that does not exist, and going over LayoutLimits.MaximumColumns.
    public WidgetLayout MoveToNewColumn(string widgetId, int columnIndex)
    {
        var (fromColumn, fromIndex) = Find(widgetId)
            ?? throw new ArgumentException($"'{widgetId}' is not in the layout.", nameof(widgetId));
        if (columnIndex < 0 || columnIndex > Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "There is no such gap between columns.");
        }

        var columns = Columns.ToList();
        var source = Columns[fromColumn];
        if (source.Widgets.Count == 1)
        {
            if (columnIndex == fromColumn || columnIndex == fromColumn + 1)
            {
                return this;
            }
            columns.RemoveAt(fromColumn);
            columns.Insert(columnIndex > fromColumn ? columnIndex - 1 : columnIndex, source);
            return With(columns);
        }

        if (!CanMoveToNewColumn(widgetId))
        {
            throw new InvalidOperationException($"A layout can have at most {LayoutLimits.MaximumColumns} columns.");
        }

        var width = NewColumnWidth;
        var placement = source.Widgets[fromIndex] with { HeightWeight = 1 };
        columns[fromColumn] = NormalizeHeights(source with { Widgets = source.Widgets.Where((_, i) => i != fromIndex).ToList() });
        columns.Insert(columnIndex, new LayoutColumn(width, new[] { placement }));
        return With(columns);
    }

    // Whether the widget can go into a new column: always for a widget alone in its column (its column just
    // moves), otherwise only while there is room for another column.
    public bool CanMoveToNewColumn(string widgetId) =>
        Find(widgetId) is { } at && (Columns[at.Column].Widgets.Count == 1 || Columns.Count < LayoutLimits.MaximumColumns);

    // Drops columns with no widgets, keeping the others in order with their widths and their heights
    // rescaled to average 1. Returns this same layout when no column is empty.
    public WidgetLayout RemoveEmptyColumns()
    {
        if (Columns.All(c => c.Widgets.Count > 0))
        {
            return this;
        }
        return With(Columns.Where(c => c.Widgets.Count > 0).Select(NormalizeHeights));
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
        return With(Columns.Select((c, i) => i == column ? c with { Widgets = resized } : c));
    }

    // Makes a column wider (positive delta) or narrower (negative) by moving width weight between it and the
    // column right of it. delta and minimumFraction are fractions of the total width (the UI divides pixels by
    // the width the columns share). Neither column goes below minimumFraction; the move stops there. The total
    // width weight and every other column are unchanged. Returns this same layout when nothing changes.
    // The column must have a column to its right.
    public WidgetLayout ResizeColumns(int columnIndex, double delta, double minimumFraction)
    {
        if (columnIndex < 0 || columnIndex + 1 >= Columns.Count)
        {
            throw new ArgumentException($"Column {columnIndex} has no column to its right to resize against.", nameof(columnIndex));
        }

        var total = Columns.Sum(c => c.WidthWeight);
        var minimum = minimumFraction * total;
        var left = Columns[columnIndex];
        var right = Columns[columnIndex + 1];

        var most = right.WidthWeight - minimum;
        var least = minimum - left.WidthWeight;
        var change = least > most ? 0 : Math.Clamp(delta * total, Math.Min(least, 0), Math.Max(most, 0));
        if (Math.Abs(change) < 1e-12)
        {
            return this;
        }

        return With(Columns.Select((c, i) =>
            i == columnIndex ? c with { WidthWeight = left.WidthWeight + change }
            : i == columnIndex + 1 ? c with { WidthWeight = right.WidthWeight - change }
            : c));
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

    // Takes a widget off the dashboard: it joins the hidden widgets (its data is untouched), its column is
    // rescaled, and a column it leaves empty collapses. The last widget on the dashboard cannot be removed.
    public WidgetLayout RemoveWidget(string widgetId)
    {
        var (column, index) = Find(widgetId)
            ?? throw new ArgumentException($"'{widgetId}' is not on the dashboard.", nameof(widgetId));
        if (!CanRemoveWidget)
        {
            throw new InvalidOperationException("At least one widget has to stay on the dashboard.");
        }

        var left = Columns[column] with { Widgets = Columns[column].Widgets.Where((_, i) => i != index).ToList() };
        var columns = Columns.Select((c, i) => i == column ? NormalizeHeights(left) : c).Where(c => c.Widgets.Count > 0);
        return new WidgetLayout(columns, HiddenWidgetIds.Append(widgetId));
    }

    // Whether a widget can be removed: not when it is the only one left on the dashboard.
    public bool CanRemoveWidget => WidgetIds.Skip(1).Any();

    // Puts a hidden widget back on the dashboard in a column gap (0 = top, Count = bottom), with the given
    // height weight (its default); the column is then rescaled to average 1.
    public WidgetLayout AddWidget(string widgetId, int columnIndex, int index, double heightWeight)
    {
        RequireHidden(widgetId);
        if (columnIndex < 0 || columnIndex >= Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "There is no such column.");
        }

        var widgets = Columns[columnIndex].Widgets.ToList();
        widgets.Insert(Math.Clamp(index, 0, widgets.Count), new WidgetPlacement(widgetId, heightWeight));
        var columns = Columns.Select((c, i) => i == columnIndex ? NormalizeHeights(c with { Widgets = widgets }) : c);
        return new WidgetLayout(columns, HiddenWidgetIds.Where(id => id != widgetId));
    }

    // Puts a hidden widget back on the dashboard in a new column at a gap between columns (0 = left edge,
    // Count = right edge). Like MoveToNewColumn, it fills the column and the column gets the average width.
    public WidgetLayout AddWidgetInNewColumn(string widgetId, int columnIndex)
    {
        RequireHidden(widgetId);
        if (columnIndex < 0 || columnIndex > Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "There is no such gap between columns.");
        }
        if (!CanAddInNewColumn)
        {
            throw new InvalidOperationException($"A layout can have at most {LayoutLimits.MaximumColumns} columns.");
        }

        var columns = Columns.ToList();
        columns.Insert(columnIndex, new LayoutColumn(NewColumnWidth, new[] { new WidgetPlacement(widgetId) }));
        return new WidgetLayout(columns, HiddenWidgetIds.Where(id => id != widgetId));
    }

    // Whether there is room for another column.
    public bool CanAddInNewColumn => Columns.Count < LayoutLimits.MaximumColumns;

    private void RequireHidden(string widgetId)
    {
        if (Find(widgetId) is not null)
        {
            throw new ArgumentException($"'{widgetId}' is already on the dashboard.", nameof(widgetId));
        }
        if (!HiddenWidgetIds.Contains(widgetId))
        {
            throw new ArgumentException($"'{widgetId}' is not a hidden widget of this layout.", nameof(widgetId));
        }
    }

    // A new column's width: the average of the current ones (1 when there are none).
    private double NewColumnWidth => Columns.Count == 0 ? 1 : Math.Round(Columns.Average(c => c.WidthWeight), 3);

    // The same hidden widgets with different columns.
    private WidgetLayout With(IEnumerable<LayoutColumn> columns) => new(columns, HiddenWidgetIds);

    // A compact text form for tests and logs, like "1.3[schedule:1] | 1[tasks:1, notes:1.2] (hidden: links)".
    public string Describe() =>
        string.Join(" | ", Columns.Select(c =>
            $"{Number(c.WidthWeight)}[{string.Join(", ", c.Widgets.Select(w => $"{w.WidgetId}:{Number(w.HeightWeight)}"))}]"))
        + (HiddenWidgetIds.Count > 0 ? $" (hidden: {string.Join(", ", HiddenWidgetIds)})" : "");

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
