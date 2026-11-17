using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// The hover guide line on the day grid, and clicking empty grid space to add a block there.
public class ScheduleHoverTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Saved { get; set; } = new(Array.Empty<ScheduleBlock>());
        public DaySchedule Get() => Saved;
        public void Save(DaySchedule schedule) => Saved = schedule;
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> Saved { get; private set; } = Array.Empty<Marker>();
        public IReadOnlyList<Marker> GetAll() => Saved;
        public void ReplaceAll(IEnumerable<Marker> markers) => Saved = markers.ToList();
    }

    private readonly FakeScheduleRepository _blocks = new();
    private readonly TimeDisplay _display = new();

    private ScheduleViewModel Create() =>
        new(_blocks, new FakeMarkerRepository(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)), _display);

    [Fact]
    public void HoverLine_StartsHidden()
    {
        Assert.False(Create().IsHoverVisible);
    }

    [Fact]
    public void HoverAt_ShowsTheLineAtThatTime()
    {
        var schedule = Create();

        schedule.HoverAt(new TimeOnly(12, 15));

        Assert.True(schedule.IsHoverVisible);
        Assert.Equal(735, schedule.HoverMinute);
        Assert.Equal("12:15", schedule.HoverLabel);
    }

    [Fact]
    public void HoverLabel_FollowsTheClockFormat()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(14, 45));

        _display.Format = ClockFormat.TwelveHour;

        Assert.Equal("2:45 PM", schedule.HoverLabel);
    }

    [Fact]
    public void HoverAt_OverABlockOrMarker_HidesTheLine()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(12, 15));

        schedule.HoverAt(new TimeOnly(14, 30), overItem: true);

        Assert.False(schedule.IsHoverVisible);
    }

    [Fact]
    public void HideHover_WhenThePointerLeavesTheGrid_HidesTheLine()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(12, 15));

        schedule.HideHover();

        Assert.False(schedule.IsHoverVisible);
    }

    [Theory]
    [InlineData(12, 15, "12:15", "13:15")]
    [InlineData(0, 0, "00:00", "01:00")]
    [InlineData(22, 45, "22:45", "23:45")]
    [InlineData(23, 0, "23:00", "00:00")]
    [InlineData(23, 45, "23:45", "00:00")]
    public void AddBlockAtHover_OpensTheEditorAtTheLineForAnHourButNotPastMidnight(int hour, int minute, string start, string end)
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(hour, minute));

        schedule.AddBlockAtHoverCommand.Execute(null);

        var editor = schedule.BlockEditor;
        Assert.True(editor.IsOpen);
        Assert.False(editor.IsEditing);
        Assert.Equal("", editor.Title);
        Assert.Equal(start, editor.StartText);
        Assert.Equal(end, editor.EndText);
    }

    [Fact]
    public void AddBlockAtHover_ClosesTheMarkerEditor()
    {
        var schedule = Create();
        schedule.AddMarkerCommand.Execute(null);
        schedule.HoverAt(new TimeOnly(12, 0));

        schedule.AddBlockAtHoverCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.True(schedule.BlockEditor.IsOpen);
    }

    [Fact]
    public void AddBlockAtHover_WithNoLineShowing_DoesNothing()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(12, 0));
        schedule.HideHover();

        schedule.AddBlockAtHoverCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
    }

    [Fact]
    public void AddBlockAtHover_SavedBlockUsesThePrefilledTimes()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(16, 30));
        schedule.AddBlockAtHoverCommand.Execute(null);
        schedule.BlockEditor.Title = "Read";

        schedule.BlockEditor.SaveCommand.Execute(null);

        var block = Assert.Single(_blocks.Saved.Blocks);
        Assert.Equal(("Read", new TimeOnly(16, 30), new TimeOnly(17, 30)), (block.Title, block.Start, block.End));
    }
}
