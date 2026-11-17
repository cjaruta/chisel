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

    // Places the widget at index in the given column (indexes as in this layout). A column index past the
    // last column starts a new column. A column left empty is dropped. An unplaced widget is added.
    public WidgetLayout Move(string widgetId, int columnIndex, int index)
    {
        var columns = Columns.Select(c => c.Widgets.ToList()).ToList();
        var widths = Columns.Select(c => c.WidthWeight).ToList();
        var from = Find(widgetId);
        var placement = from is { } f ? columns[f.Column][f.Index] : new WidgetPlacement(widgetId);

        if (from is { } same && same.Column == columnIndex)
        {
            columns[same.Column].RemoveAt(same.Index);
            columns[columnIndex].Insert(Math.Clamp(index, 0, columns[columnIndex].Count), placement);
        }
        else
        {
            if (columnIndex >= columns.Count || columnIndex < 0)
            {
                columns.Add(new List<WidgetPlacement> { placement });
                widths.Add(1);
            }
            else
            {
                columns[columnIndex].Insert(Math.Clamp(index, 0, columns[columnIndex].Count), placement);
            }
            if (from is { } old)
            {
                columns[old.Column].RemoveAt(old.Index);
            }
        }

        return Build(widths, columns);
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
