using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class ScheduleViewModelTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        private DaySchedule _schedule = new(Array.Empty<ScheduleBlock>());

        public DaySchedule Get() => _schedule;
        public void Save(DaySchedule schedule) => _schedule = schedule;
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        private IReadOnlyList<Marker> _markers = Array.Empty<Marker>();

        public IReadOnlyList<Marker> GetAll() => _markers;
        public void ReplaceAll(IEnumerable<Marker> markers) => _markers = markers.ToList();
    }

    [Fact]
    public void Constructor_WhenNowIsInABlock_SetsCurrentBlockTitle()
    {
        var repository = new FakeScheduleRepository();
        repository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

        var viewModel = new ScheduleViewModel(repository, new FakeMarkerRepository(), time);

        Assert.Equal("Focus", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Constructor_WhenNowIsNotInABlock_SetsNoBlockText()
    {
        var repository = new FakeScheduleRepository();
        repository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

        var viewModel = new ScheduleViewModel(repository, new FakeMarkerRepository(), time);

        Assert.Equal("No block", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Refresh_AfterScheduleChanges_UpdatesCurrentBlockTitle()
    {
        var repository = new FakeScheduleRepository();
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        var viewModel = new ScheduleViewModel(repository, new FakeMarkerRepository(), time);
        Assert.Equal("No block", viewModel.CurrentBlockTitle);

        repository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        viewModel.Refresh();

        Assert.Equal("Focus", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Timeline_MergesBlocksAndMarkersInTimeOrder()
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0))
        }));
        var markerRepository = new FakeMarkerRepository();
        markerRepository.ReplaceAll(new[]
        {
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder)
        });
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

        var viewModel = new ScheduleViewModel(scheduleRepository, markerRepository, time);

        Assert.Equal(
            new[] { "Focus", "Last coffee", "Workout" },
            viewModel.Timeline.Select(e => e.Title).ToArray());
    }

    [Fact]
    public void Timeline_FlagsCurrentBlockAndDimsPastEntries()
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0))
        }));
        var markerRepository = new FakeMarkerRepository();
        markerRepository.ReplaceAll(new[]
        {
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder)
        });
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 13, 30, 0, TimeSpan.Zero));

        var viewModel = new ScheduleViewModel(scheduleRepository, markerRepository, time);

        var focus = viewModel.Timeline.Single(e => e.Title == "Focus");
        var coffee = viewModel.Timeline.Single(e => e.Title == "Last coffee");
        var workout = viewModel.Timeline.Single(e => e.Title == "Workout");

        Assert.True(focus.IsPast);
        Assert.False(focus.IsCurrent);
        Assert.True(coffee.IsPast);
        Assert.False(workout.IsPast);
        Assert.False(workout.IsCurrent);
    }

    [Theory]
    [InlineData(22, 59, false, false)] // just before start
    [InlineData(23, 0, true, false)]   // start, before midnight
    [InlineData(23, 59, true, false)]  // just before midnight
    [InlineData(0, 0, true, false)]    // midnight
    [InlineData(0, 1, true, false)]    // just after midnight
    [InlineData(6, 29, true, false)]   // just before end
    [InlineData(6, 30, false, false)]  // the end itself, not current and not past
    [InlineData(12, 0, false, false)]  // midday: upcoming again tonight, never past
    public void Timeline_MidnightCrossingBlock_FlagsCurrentCorrectlyAndNeverPast(
        int hour, int minute, bool expectedCurrent, bool expectedPast)
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30))
        }));
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, hour, minute, 0, TimeSpan.Zero));

        var viewModel = new ScheduleViewModel(scheduleRepository, new FakeMarkerRepository(), time);

        var sleep = viewModel.Timeline.Single(e => e.Title == "Sleep");
        Assert.Equal(expectedCurrent, sleep.IsCurrent);
        Assert.Equal(expectedPast, sleep.IsPast);
    }

    [Fact]
    public void Timeline_MidnightCrossingBlock_SortsByStartTimeAmongOtherEntries()
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Read", new TimeOnly(22, 0), new TimeOnly(23, 0)),
            new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30))
        }));
        var time = new FakeTimeProvider();

        var viewModel = new ScheduleViewModel(scheduleRepository, new FakeMarkerRepository(), time);

        Assert.Equal(
            new[] { "Focus", "Read", "Sleep" },
            viewModel.Timeline.Select(e => e.Title).ToArray());
    }

    [Fact]
    public void Timeline_FormatsBlockAndMarkerTimeLabels()
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        var markerRepository = new FakeMarkerRepository();
        markerRepository.ReplaceAll(new[]
        {
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder)
        });
        var time = new FakeTimeProvider();

        var viewModel = new ScheduleViewModel(scheduleRepository, markerRepository, time);

        Assert.Equal("08:00-11:00", viewModel.Timeline.Single(e => e.Title == "Focus").TimeLabel);
        Assert.Equal("13:00", viewModel.Timeline.Single(e => e.Title == "Last coffee").TimeLabel);
    }
}
