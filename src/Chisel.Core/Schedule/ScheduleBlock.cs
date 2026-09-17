namespace Chisel.Core.Schedule;

public class ScheduleBlock
{
    public ScheduleBlock(string title, TimeOnly start, TimeOnly end)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }
        if (start == end)
        {
            throw new ArgumentException("Start time cannot be equal to end time.", nameof(end));
        }
        Title = title.Trim();
        Start = start;
        End = end;
    }

    public string Title { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeSpan Duration => End - Start;

    public bool CrossesMidnight => End < Start;

    public bool Contains(TimeOnly time)
    {
        if (!CrossesMidnight)
        {
            return time >= Start && time < End;
        }

        return time >= Start || time < End;
    }
}