using System.Globalization;

namespace Chisel.Core.Layout;

// One member of a widget group. ShowHeader is the header choice the widget had before it joined the group
// (null = its own default), kept so it comes back when the widget leaves.
public sealed record GroupMember(string WidgetId, bool? ShowHeader = null);

// One card in a column. HeightWeight is its share of the column's height, relative to its neighbours.
// ShowHeader is the user's choice for the widget's title bar; null means use the widget's own default.
// A card is either one widget, or (Members has two or more) a group: tabs in the header, one active widget in the
// body. WidgetId is then the card's own id: it is always one of the members, and stays put when tabs are reordered.
// ShowHeader means nothing for a group (it always shows its header); each member remembers its own choice.
public sealed record WidgetPlacement(
    string WidgetId, double HeightWeight = 1, bool? ShowHeader = null,
    IReadOnlyList<GroupMember>? Members = null, int ActiveIndex = 0)
{
    public bool IsGroup => Members is { Count: > 1 };

    // The widgets on this card, in tab order.
    public IReadOnlyList<string> MemberIds => IsGroup ? Members!.Select(m => m.WidgetId).ToList() : new[] { WidgetId };

    // The widget whose body is shown.
    public string ActiveId => IsGroup ? Members![Math.Clamp(ActiveIndex, 0, Members.Count - 1)].WidgetId : WidgetId;

    public bool Contains(string widgetId) => IsGroup ? Members!.Any(m => m.WidgetId == widgetId) : WidgetId == widgetId;
}

public static class WidgetHeaders
{
    // Whether a widget shows its header: the user's choice if there is one, otherwise the widget's default.
    public static bool EffectiveShowHeader(WidgetPlacement placement, bool widgetDefault) => placement.ShowHeader ?? widgetDefault;
}

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

    // Every widget on the dashboard, group members included.
    public IEnumerable<string> WidgetIds => Columns.SelectMany(c => c.Widgets).SelectMany(w => w.MemberIds);

    // Where the card holding a widget is (for a group member, the group's card).
    public (int Column, int Index)? Find(string widgetId)
    {
        for (var c = 0; c < Columns.Count; c++)
        {
            for (var i = 0; i < Columns[c].Widgets.Count; i++)
            {
                if (Columns[c].Widgets[i].Contains(widgetId))
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

    // Sets (or, with null, clears) one widget's header choice. Everything else is unchanged.
    public WidgetLayout SetShowHeader(string widgetId, bool? showHeader)
    {
        if (Find(widgetId) is null)
        {
            throw new ArgumentException($"'{widgetId}' is not on the dashboard.", nameof(widgetId));
        }
        // A group member's choice is remembered, not changed: its group always shows the header.
        var (column, index) = Find(widgetId)!.Value;
        if (Columns[column].Widgets[index].IsGroup)
        {
            return this;
        }
        return With(Columns.Select(c => c with
        {
            Widgets = c.Widgets.Select(w => w.WidgetId == widgetId ? w with { ShowHeader = showHeader } : w).ToList(),
        }));
    }

    // Whether the dragged widget's card can be merged into the target's card: both are on the dashboard on
    // different cards, the result holds at most LayoutLimits.MaximumGroupMembers widgets, and no widget would appear twice.
    public bool CanCombine(string draggedId, string targetId)
    {
        if (Find(draggedId) is not { } from || Find(targetId) is not { } to || from == to)
        {
            return false;
        }
        var dragged = Columns[from.Column].Widgets[from.Index].MemberIds;
        var target = Columns[to.Column].Widgets[to.Index].MemberIds;
        return dragged.Count + target.Count <= LayoutLimits.MaximumGroupMembers && !dragged.Intersect(target).Any();
    }

    // Merges the dragged widget's card into the target's: its widgets become the last tabs, the target's active tab
    // stays active, and the target card keeps its place and height. The column the dragged card left is rescaled
    // (and kept even if empty; see RemoveEmptyColumns). Each widget remembers the header choice it had.
    public WidgetLayout Combine(string draggedId, string targetId)
    {
        if (!CanCombine(draggedId, targetId))
        {
            throw new InvalidOperationException($"'{draggedId}' cannot be combined with '{targetId}'.");
        }
        var from = Find(draggedId)!.Value;
        var to = Find(targetId)!.Value;
        var dragged = Columns[from.Column].Widgets[from.Index];
        var target = Columns[to.Column].Widgets[to.Index];

        var members = MembersOf(target).Concat(MembersOf(dragged)).ToList();
        var group = target with { Members = members, ActiveIndex = target.IsGroup ? target.ActiveIndex : 0, ShowHeader = null };

        var columns = Columns.Select(c => c.Widgets.ToList()).ToList();
        columns[to.Column][to.Index] = group;
        columns[from.Column].Remove(dragged);
        return With(Columns.Select((c, i) =>
        {
            var changed = c with { Widgets = columns[i] };
            return i == from.Column ? NormalizeHeights(changed) : changed;
        }));
    }

    private static IEnumerable<GroupMember> MembersOf(WidgetPlacement placement) =>
        placement.IsGroup ? placement.Members! : new[] { new GroupMember(placement.WidgetId, placement.ShowHeader) };

    // Takes one widget out of its group, as its own card right below the group (the nearest spot that is always
    // valid) with the given height weight and the header choice it had before it joined. When one member is left,
    // the group dissolves into a plain card. A widget that is not in a group changes nothing.
    public WidgetLayout Split(string memberId, double heightWeight = 1)
    {
        var (column, index) = Find(memberId) ?? throw new ArgumentException($"'{memberId}' is not on the dashboard.", nameof(memberId));
        var group = Columns[column].Widgets[index];
        if (!group.IsGroup)
        {
            return this;
        }

        var members = group.Members!.ToList();
        var at = members.FindIndex(m => m.WidgetId == memberId);
        var leaving = members[at];
        var activeId = group.ActiveId;
        members.RemoveAt(at);

        WidgetPlacement rest;
        if (members.Count == 1)
        {
            rest = new WidgetPlacement(members[0].WidgetId, group.HeightWeight, members[0].ShowHeader);
        }
        else
        {
            var active = members.FindIndex(m => m.WidgetId == activeId);
            rest = group with
            {
                WidgetId = members.Any(m => m.WidgetId == group.WidgetId) ? group.WidgetId : members[0].WidgetId,
                Members = members,
                ActiveIndex = active >= 0 ? active : Math.Min(at, members.Count - 1),
            };
        }
        var widgets = Columns[column].Widgets.ToList();
        widgets[index] = rest;
        widgets.Insert(index + 1, new WidgetPlacement(leaving.WidgetId, heightWeight, leaving.ShowHeader));
        return With(Columns.Select((c, i) => i == column ? NormalizeHeights(c with { Widgets = widgets }) : c));
    }

    // Moves a tab to a new place in its group (clamped); the active tab stays on the same widget.
    // Returns this same layout when nothing changes.
    public WidgetLayout ReorderMember(string memberId, int newIndex)
    {
        var (column, index) = Find(memberId) ?? throw new ArgumentException($"'{memberId}' is not on the dashboard.", nameof(memberId));
        var group = Columns[column].Widgets[index];
        if (!group.IsGroup)
        {
            return this;
        }
        var members = group.Members!.ToList();
        var from = members.FindIndex(m => m.WidgetId == memberId);
        var to = Math.Clamp(newIndex, 0, members.Count - 1);
        if (from == to)
        {
            return this;
        }
        var activeId = group.ActiveId;
        var moved = members[from];
        members.RemoveAt(from);
        members.Insert(to, moved);
        return Replace(column, index, group with { Members = members, ActiveIndex = members.FindIndex(m => m.WidgetId == activeId) });
    }

    // Makes a group member's tab the active one. Returns this same layout when it already is, or for a plain card.
    public WidgetLayout SetActive(string memberId)
    {
        var (column, index) = Find(memberId) ?? throw new ArgumentException($"'{memberId}' is not on the dashboard.", nameof(memberId));
        var group = Columns[column].Widgets[index];
        if (!group.IsGroup || group.ActiveId == memberId)
        {
            return this;
        }
        return Replace(column, index, group with { ActiveIndex = group.Members!.ToList().FindIndex(m => m.WidgetId == memberId) });
    }

    private WidgetLayout Replace(int column, int index, WidgetPlacement placement) =>
        With(Columns.Select((c, i) => i == column
            ? c with { Widgets = c.Widgets.Select((w, j) => j == index ? placement : w).ToList() }
            : c));

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
        return new WidgetLayout(columns, HiddenWidgetIds.Concat(Columns[column].Widgets[index].MemberIds));
    }

    // Whether a card can be removed: not when it is the only one left on the dashboard (a group is one card).
    public bool CanRemoveWidget => Columns.Sum(c => c.Widgets.Count) > 1;

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
            $"{Number(c.WidthWeight)}[{string.Join(", ", c.Widgets.Select(w => $"{(w.IsGroup ? string.Join("+", w.MemberIds) + "@" + w.ActiveIndex : w.WidgetId)}:{Number(w.HeightWeight)}"))}]"))
        + (HiddenWidgetIds.Count > 0 ? $" (hidden: {string.Join(", ", HiddenWidgetIds)})" : "");

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
