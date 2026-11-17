namespace Chisel.Core.Layout;

// Makes a loaded layout safe to show. The default layout says which widgets exist and where they belong:
// widgets not in it are dropped (and repeats of one widget), empty columns go, columns past
// LayoutLimits.MaximumColumns fold into the last one, and weights are kept sensible.
// A widget missing from the loaded layout comes back next to its default neighbours (the widgets of its
// default column), right after the nearest one before it; with none of them left it gets its own column
// on its default side.
public static class WidgetLayoutNormalizer
{
    public static WidgetLayout Normalize(WidgetLayout layout, WidgetLayout defaults)
    {
        var known = defaults.WidgetIds.ToHashSet();
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

        for (var c = 0; c < defaults.Columns.Count; c++)
        {
            var defaultColumn = defaults.Columns[c];
            for (var i = 0; i < defaultColumn.Widgets.Count; i++)
            {
                var placement = defaultColumn.Widgets[i];
                if (!seen.Add(placement.WidgetId))
                {
                    continue;
                }

                var siblings = defaultColumn.Widgets.Select(w => w.WidgetId).ToList();
                var home = columns.FirstOrDefault(col => col.Any(w => siblings.Contains(w.WidgetId)));
                if (home is null)
                {
                    var at = Math.Min(c, columns.Count);
                    widths.Insert(at, defaultColumn.WidthWeight);
                    columns.Insert(at, new List<WidgetPlacement> { placement });
                    continue;
                }

                var before = siblings.Take(i).ToHashSet();
                var after = home.FindLastIndex(w => before.Contains(w.WidgetId));
                home.Insert(after + 1, placement);
            }
        }

        return new WidgetLayout(columns.Select((widgets, i) => new LayoutColumn(widths[i], widgets)));
    }

    // NaN or infinite weights become 1; small or negative ones are raised to the minimum.
    private static double Weight(double weight) =>
        double.IsFinite(weight) ? Math.Max(weight, WidgetLayout.MinimumWeight) : 1;
}
