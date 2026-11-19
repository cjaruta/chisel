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
                GroupMembers = widget.IsGroup ? FormatMembers(widget.Members!) : null,
                ActiveTab = widget.IsGroup ? widget.ActiveIndex : 0,
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
                g.OrderBy(p => p.Position).Select(ToPlacement).ToList())),
            hiddenIds);
    }

    private static WidgetPlacement ToPlacement(WidgetPlacementEntity p)
    {
        var members = ParseMembers(p.GroupMembers);
        return members.Count > 1
            ? new WidgetPlacement(p.WidgetId, p.HeightWeight, null, members, p.ActiveTab)
            : new WidgetPlacement(p.WidgetId, p.HeightWeight, p.ShowHeader);
    }

    private static string FormatMembers(IEnumerable<GroupMember> members) =>
        string.Join(";", members.Select(m => $"{m.WidgetId}:{(m.ShowHeader is { } shown ? (shown ? 't' : 'f') : 'n')}"));

    private static List<GroupMember> ParseMembers(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : text.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var colon = part.LastIndexOf(':');
                    var id = colon < 0 ? part : part[..colon];
                    var flag = colon < 0 || colon == part.Length - 1 ? 'n' : part[colon + 1];
                    return new GroupMember(id, flag switch { 't' => true, 'f' => false, _ => null });
                })
                .ToList();
}
