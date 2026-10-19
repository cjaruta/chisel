using Chisel.Core.Layout;

namespace Chisel.Infrastructure.Data;

public class WidgetPlacementEntity
{
    public Guid Id { get; set; }
    public string WidgetId { get; set; } = string.Empty;
    public Column Column { get; set; }
    public int Position { get; set; }
}