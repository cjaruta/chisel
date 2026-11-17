using Chisel.Core.Schedule;

namespace Chisel.Infrastructure.Data;

public class ScheduleBlockEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public int Position { get; set; }

    // A BlockColor palette key.
    public string Color { get; set; } = BlockColor.Default;
}