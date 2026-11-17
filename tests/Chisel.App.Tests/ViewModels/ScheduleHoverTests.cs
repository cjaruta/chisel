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
}
