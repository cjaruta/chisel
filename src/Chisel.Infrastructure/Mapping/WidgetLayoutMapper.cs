using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class WidgetLayoutMapper
{
    public static List<WidgetPlacementEntity> ToEntities(this WidgetLayout layout)
    {
        var entities = new List<WidgetPlacementEntity>();
        entities.AddRange(ToColumnEntities(layout.Side, Column.Side));
        entities.AddRange(ToColumnEntities(layout.Main, Column.Main));
        return entities;
    }

    public static WidgetLayout ToLayout(this IEnumerable<WidgetPlacementEntity> entities)
    {
        var layout = new WidgetLayout();

        foreach (var placement in entities.OrderBy(e => e.Position))
        {
            var index = placement.Column == Column.Side ? layout.Side.Count : layout.Main.Count;
            layout.Move(placement.WidgetId, placement.Column, index);
        }

        return layout;
    }

    private static IEnumerable<WidgetPlacementEntity> ToColumnEntities(IReadOnlyList<string> widgetIds, Column column) =>
        widgetIds.Select((widgetId, position) => new WidgetPlacementEntity
        {
            Id = Guid.NewGuid(),
            WidgetId = widgetId,
            Column = column,
            Position = position
        });
}