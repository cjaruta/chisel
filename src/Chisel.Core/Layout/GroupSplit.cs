namespace Chisel.Core.Layout;

// How a group's card shows its widgets: one at a time behind tabs, or side by side.
public enum GroupViewMode
{
    Tabs,
    Split,
}

// Whether a split is allowed, and if not, why (a sentence the UI can show as is).
public readonly record struct SplitCheck(bool Allowed, string? Reason);

// The rules for the side-by-side view of a group.
public static class GroupSplit
{
    public const int MinimumPanes = 2;
    public const int MaximumPanes = 3;

    // Space between two panes, in pixels.
    public const double Gap = 16;

    // The least width a split card needs: every pane's minimum plus the gaps between them.
    public static double MinimumWidth(IEnumerable<double> paneMinimums)
    {
        var list = paneMinimums.ToList();
        return list.Sum() + Gap * Math.Max(0, list.Count - 1);
    }

    // Split needs 2 or 3 widgets and a card wide enough for every pane's minimum width.
    public static SplitCheck Check(IReadOnlyList<double> paneMinimums, double cardWidth)
    {
        if (paneMinimums.Count < MinimumPanes)
        {
            return new SplitCheck(false, "Split view needs at least two widgets.");
        }
        if (paneMinimums.Count > MaximumPanes)
        {
            return new SplitCheck(false, $"Split view holds at most {MaximumPanes} widgets.");
        }
        return MinimumWidth(paneMinimums) > cardWidth
            ? new SplitCheck(false, "This card is too narrow for split view. Make the column wider.")
            : new SplitCheck(true, null);
    }
}
