namespace Chisel.Core.Schedule;

public class DaySchedule
{
    public DaySchedule(IEnumerable<ScheduleBlock> blocks)
    {
        Blocks = blocks.ToList();
    }

    public IReadOnlyList<ScheduleBlock> Blocks { get; }

    public ScheduleBlock? CurrentBlock(TimeOnly now)
    {
        return Blocks.FirstOrDefault(block => block.Contains(now));
    }
}