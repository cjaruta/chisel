namespace Chisel.Infrastructure.Data;

// One widget in the saved layout: which column (ordered by ColumnIndex), its place in that column,
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
}
