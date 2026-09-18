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
    public ScheduleBlock? NextBlock(TimeOnly now)
    {
        return Blocks
            .Where(block => block.Start > now)
            .OrderBy(block => block.Start)
            .FirstOrDefault();
    }
        public IEnumerable<(TimeOnly Start, TimeOnly End)> Gaps(TimeOnly dayStart, TimeOnly dayEnd)
    {
        var cursor = dayStart;
        foreach (var block in Blocks)
        {
            if (block.Start > cursor)
            {
                yield return (cursor, block.Start);
            }
            cursor = block.End;
        }
        if (cursor < dayEnd)
        {
            yield return (cursor, dayEnd);
        }
    }
}