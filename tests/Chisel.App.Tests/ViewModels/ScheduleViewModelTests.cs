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

        var viewModel = new ScheduleViewModel(repository, time);

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

        var viewModel = new ScheduleViewModel(repository, time);

        Assert.Equal("No block", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Refresh_AfterScheduleChanges_UpdatesCurrentBlockTitle()
    {
        var repository = new FakeScheduleRepository();
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        var viewModel = new ScheduleViewModel(repository, time);
        Assert.Equal("No block", viewModel.CurrentBlockTitle);

        repository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        viewModel.Refresh();

        Assert.Equal("Focus", viewModel.CurrentBlockTitle);
    }
}
