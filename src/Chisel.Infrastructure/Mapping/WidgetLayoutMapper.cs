using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class WidgetLayoutMapper
{
    public static (List<LayoutColumnEntity> Columns, List<WidgetPlacementEntity> Placements) ToEntities(this WidgetLayout layout)
    {
        var columns = layout.Columns
            .Select((column, index) => new LayoutColumnEntity { Position = index, WidthWeight = column.WidthWeight })
            .ToList();
        var placements = layout.Columns
            .SelectMany((column, columnIndex) => column.Widgets.Select((widget, position) => new WidgetPlacementEntity
            {
                Id = Guid.NewGuid(),
                WidgetId = widget.WidgetId,
                ColumnIndex = columnIndex,
                Position = position,
                HeightWeight = widget.HeightWeight,
                ShowHeader = widget.ShowHeader,
            }))
            .ToList();
        return (columns, placements);
    }

    public static List<HiddenWidgetEntity> ToHiddenEntities(this WidgetLayout layout) =>
        layout.HiddenWidgetIds.Select((id, position) => new HiddenWidgetEntity { WidgetId = id, Position = position }).ToList();

    // Column indexes only order the columns, so gaps are fine. A column with no width row gets width 1.
    public static WidgetLayout ToLayout(IEnumerable<LayoutColumnEntity> columns, IEnumerable<WidgetPlacementEntity> placements,
        IEnumerable<HiddenWidgetEntity>? hidden = null)
    {
        var widths = columns.ToDictionary(c => c.Position, c => c.WidthWeight);
        var hiddenIds = (hidden ?? Enumerable.Empty<HiddenWidgetEntity>()).OrderBy(h => h.Position).Select(h => h.WidgetId);
        return new WidgetLayout(placements
            .GroupBy(p => p.ColumnIndex)
            .OrderBy(g => g.Key)
            .Select(g => new LayoutColumn(
                widths.GetValueOrDefault(g.Key, 1),
                g.OrderBy(p => p.Position).Select(p => new WidgetPlacement(p.WidgetId, p.HeightWeight, p.ShowHeader)).ToList())),
            hiddenIds);
    }
}
