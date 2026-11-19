namespace Chisel.Infrastructure.Data;

// One widget card in the saved layout: which column (ordered by ColumnIndex), its place in that column,
// and its share of the column's height.
public class WidgetPlacementEntity
{
    public Guid Id { get; set; }
    public string WidgetId { get; set; } = string.Empty;
    public int ColumnIndex { get; set; }
    public int Position { get; set; }
    public double HeightWeight { get; set; } = 1;

    // The user's header choice; null means the widget's default.
    public bool? ShowHeader { get; set; }

    // For a tabbed group: its members in tab order as "id:h;id:h" (h is t, f, or n for no choice),
    // and the active tab. Null for a card that is one widget.
    // Each member may carry a third part, its width weight in split view ("id:h:w"); older rows have two parts.
    public string? GroupMembers { get; set; }
    public int ActiveTab { get; set; }

    // How a group shows its widgets; Tabs (0) for plain cards and for groups saved before split view.
    public Chisel.Core.Layout.GroupViewMode ViewMode { get; set; }
}
