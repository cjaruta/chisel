using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// Something drawn on the day grid, in minutes from midnight. EndMinute NaN means a point (a line or label).
public abstract class ScheduleGridItem : ObservableObject
{
    public double StartMinute { get; init; }
    public double EndMinute { get; init; } = double.NaN;
    public int Column { get; init; }
    public int ColumnCount { get; init; } = 1;
}

public sealed partial class ScheduleGridBlock : ScheduleGridItem
{
    // Blocks shorter than this show title and time on one line.
    public const double ShortMinutes = 45;

    // Position of the block in the saved schedule, so it can be edited later.
    public required int BlockIndex { get; init; }
    public required string Title { get; init; }
    public required string TimeLabel { get; init; }
    // BlockColor key, and the brushes it resolves to in the current palette.
    public string ColorKey { get; init; } = BlockColor.Default;
    public required BlockBrushes Brushes { get; init; }
    public bool IsShort => EndMinute - StartMinute < ShortMinutes;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsPast { get; set; }
}

public sealed partial class ScheduleGridMarker : ScheduleGridItem
{
    public required int MarkerIndex { get; init; }
    public required string Label { get; init; }
    public required string TimeLabel { get; init; }
    public required MarkerKind Kind { get; init; }
    public bool IsCheckpoint => Kind == MarkerKind.Checkpoint;

    [ObservableProperty]
    public partial bool IsPast { get; set; }
}

public sealed class ScheduleHourLabel : ScheduleGridItem
{
    public required string Text { get; init; }
}
