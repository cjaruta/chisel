namespace Chisel.Core.Layout;

// Makes a loaded layout safe to show. The default layout says which widgets exist (and their default heights):
// widgets not in it are dropped (and repeats of one widget), empty columns go, columns past
// LayoutLimits.MaximumColumns fold into the last one, and weights are kept sensible.
//
// A known widget missing from the layout is either hidden (the user removed it: it is in HiddenWidgetIds and
// stays off the dashboard) or new (neither placed nor hidden, e.g. a widget added in a later version): a new
// widget is added at the bottom of the first column with its default height. A layout with nothing placed
// becomes the default.
public static class WidgetLayoutNormalizer
{
    public static WidgetLayout Normalize(WidgetLayout layout, WidgetLayout defaults)
    {
        var known = defaults.WidgetIds.ToList();
        var seen = new HashSet<string>();

        var widths = new List<double>();
        var columns = new List<List<WidgetPlacement>>();
        foreach (var column in layout.Columns)
        {
            var widgets = column.Widgets
                .Where(w => known.Contains(w.WidgetId) && seen.Add(w.WidgetId))
                .Select(w => w with { HeightWeight = Weight(w.HeightWeight) })
                .ToList();
            if (widgets.Count == 0)
            {
                continue;
            }
            if (columns.Count == LayoutLimits.MaximumColumns)
            {
                // Too many columns: the extra widgets go to the bottom of the last allowed column.
                columns[^1].AddRange(widgets);
                continue;
            }
            widths.Add(Weight(column.WidthWeight));
            columns.Add(widgets);
        }

        if (columns.Count == 0)
        {
            return defaults;
        }

        var hidden = known.Where(id => !seen.Contains(id) && layout.HiddenWidgetIds.Contains(id)).ToList();
        var brandNew = known.Where(id => !seen.Contains(id) && !hidden.Contains(id)).ToList();
        foreach (var id in brandNew)
        {
            var defaultHeight = defaults.Columns.SelectMany(c => c.Widgets).First(w => w.WidgetId == id).HeightWeight;
            columns[0].Add(new WidgetPlacement(id, defaultHeight));
        }

        var result = columns.Select((widgets, i) => new LayoutColumn(widths[i], widgets)).ToList();
        if (brandNew.Count > 0)
        {
            result[0] = WidgetLayout.NormalizeHeights(result[0]);
        }
        return new WidgetLayout(result, hidden);
    }

    // NaN or infinite weights become 1; small or negative ones are raised to the minimum.
    private static double Weight(double weight) =>
        double.IsFinite(weight) ? Math.Max(weight, WidgetLayout.MinimumWeight) : 1;
}
