namespace Chisel.App.ViewModels;

public sealed class ScheduleTimelineEntry
{
    public required string Title { get; init; }
    public required string TimeLabel { get; init; }
    public required bool IsCurrent { get; init; }
    public required bool IsPast { get; init; }
}
