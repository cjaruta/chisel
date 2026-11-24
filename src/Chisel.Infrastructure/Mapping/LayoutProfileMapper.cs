using System.Text.Json;
using Chisel.Core.Layout;

namespace Chisel.Infrastructure.Mapping;

// A whole layout as one JSON text, so a profile needs one row and no copy of the layout tables.
public static class LayoutProfileMapper
{
    private sealed record MemberDto(string Id, bool? ShowHeader, double Width);

    private sealed record WidgetDto(string Id, double Height, bool? ShowHeader, List<MemberDto>? Members, int Active, GroupViewMode View);

    private sealed record ColumnDto(double Width, List<WidgetDto> Widgets);

    private sealed record LayoutDto(List<ColumnDto> Columns, List<string> Hidden);

    public static string ToJson(this WidgetLayout layout) => JsonSerializer.Serialize(new LayoutDto(
        layout.Columns.Select(c => new ColumnDto(c.WidthWeight, c.Widgets.Select(w => new WidgetDto(
            w.WidgetId, w.HeightWeight, w.ShowHeader,
            w.IsGroup ? w.Members!.Select(m => new MemberDto(m.WidgetId, m.ShowHeader, m.WidthWeight)).ToList() : null,
            w.ActiveIndex, w.ViewMode)).ToList())).ToList(),
        layout.HiddenWidgetIds.ToList()));

    // Null when the text is not a layout (a damaged row is skipped rather than breaking the app).
    public static WidgetLayout? ToLayout(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<LayoutDto>(json);
            if (dto?.Columns is null || dto.Columns.Count == 0)
            {
                return null;
            }
            return new WidgetLayout(
                dto.Columns.Select(c => new LayoutColumn(c.Width, (c.Widgets ?? []).Select(w => new WidgetPlacement(
                    w.Id, w.Height, w.ShowHeader,
                    w.Members?.Select(m => new GroupMember(m.Id, m.ShowHeader, m.Width)).ToList(),
                    w.Active, w.View)).ToList())),
                dto.Hidden ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
