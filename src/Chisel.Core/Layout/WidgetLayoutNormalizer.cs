namespace Chisel.Core.Layout;

// Makes a loaded layout safe to show. The default layout says which widgets exist (and their default heights):
// widgets not in it are dropped (and repeats of one widget), empty columns go, columns past
// LayoutLimits.MaximumColumns fold into the last one, and weights are kept sensible.
//
// A known widget missing from the layout is either hidden (the user removed it: it is in HiddenWidgetIds and
// stays off the dashboard) or new (neither placed nor hidden, e.g. a widget added in a later version): a new
// widget is added at the bottom of the first column with its default height. A layout with nothing placed
// becomes the default. A widget the default layout lists as hidden is never placed on its own: it waits in the tray.
public static class WidgetLayoutNormalizer
{
    public static WidgetLayout Normalize(WidgetLayout layout, WidgetLayout defaults)
    {
        var known = defaults.WidgetIds.Concat(defaults.HiddenWidgetIds).ToList();
        var seen = new HashSet<string>();

        var widths = new List<double>();
        var columns = new List<List<WidgetPlacement>>();
        foreach (var column in layout.Columns)
        {
            var widgets = column.Widgets
                .Select(w => Clean(w, known, seen))
                .OfType<WidgetPlacement>()
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

        var hidden = known.Where(id => !seen.Contains(id) && (layout.HiddenWidgetIds.Contains(id) || defaults.HiddenWidgetIds.Contains(id))).ToList();
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

    // One saved card made safe: unknown and repeated widgets go, a group keeps at most LayoutLimits.MaximumGroupMembers
    // members (the rest are treated as not placed), one left becomes a plain card with its own header choice back,
    // and the active tab stays in range. Null when nothing is left.
    private static WidgetPlacement? Clean(WidgetPlacement placement, List<string> known, HashSet<string> seen)
    {
        var weight = Weight(placement.HeightWeight);
        if (!placement.IsGroup)
        {
            return known.Contains(placement.WidgetId) && seen.Add(placement.WidgetId)
                ? placement with { HeightWeight = weight, Members = null, ActiveIndex = 0 }
                : null;
        }

        var activeId = placement.ActiveId;
        var members = new List<GroupMember>();
        foreach (var member in placement.Members!)
        {
            if (members.Count < LayoutLimits.MaximumGroupMembers && known.Contains(member.WidgetId) && seen.Add(member.WidgetId))
            {
                members.Add(member);
            }
        }
        if (members.Count == 0)
        {
            return null;
        }
        if (members.Count == 1)
        {
            return new WidgetPlacement(members[0].WidgetId, weight, members[0].ShowHeader);
        }
        var active = members.FindIndex(m => m.WidgetId == activeId);
        return placement with
        {
            WidgetId = members.Any(m => m.WidgetId == placement.WidgetId) ? placement.WidgetId : members[0].WidgetId,
            HeightWeight = weight,
            ShowHeader = null,
            Members = members,
            ActiveIndex = active >= 0 ? active : Math.Clamp(placement.ActiveIndex, 0, members.Count - 1),
        };
    }

    // NaN or infinite weights become 1; small or negative ones are raised to the minimum.
    private static double Weight(double weight) =>
        double.IsFinite(weight) ? Math.Max(weight, WidgetLayout.MinimumWeight) : 1;
}
