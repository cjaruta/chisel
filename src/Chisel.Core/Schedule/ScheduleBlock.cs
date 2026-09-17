namespace Chisel.Core.Schedule;

public class ScheduleBlock
{
    public ScheduleBlock(string title, TimeOnly start, TimeOnly end)
    {
        Title = title;
        Start = start;
        End = end;
    }

    public string Title { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeSpan Duration => End - Start;
}