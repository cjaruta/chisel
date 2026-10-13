using Chisel.Core.Schedule;
namespace Chisel.Infrastructure.Data;


public class MarkerEntity
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public TimeOnly Time { get; set; }
    public MarkerKind Kind { get; set; }
}