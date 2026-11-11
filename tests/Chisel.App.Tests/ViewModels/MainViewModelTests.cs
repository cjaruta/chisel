using Chisel.App.ViewModels;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class MainViewModelTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        private DaySchedule _schedule = new(Array.Empty<ScheduleBlock>());
        public DaySchedule Get() => _schedule;
        public void Save(DaySchedule schedule) => _schedule = schedule;
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> GetAll() => Array.Empty<Marker>();
        public void ReplaceAll(IEnumerable<Marker> markers) { }
    }

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public IReadOnlyList<TaskItem> GetAll() => Array.Empty<TaskItem>();
        public void Add(TaskItem task) { }
        public void Update(TaskItem task) { }
        public void Remove(Guid id) { }
    }

    private sealed class FakeNoteRepository : INoteRepository
    {
        public IReadOnlyList<Note> GetAll() => Array.Empty<Note>();
        public void Add(Note note) { }
        public void Update(Note note) { }
        public void Remove(Guid id) { }
    }

    private sealed class FakeQuickLinkRepository : IQuickLinkRepository
    {
        public IReadOnlyList<QuickLink> GetAll() => Array.Empty<QuickLink>();
        public void Add(QuickLink link) { }
        public void Remove(Guid id) { }
    }

    private static MainViewModel CreateViewModel(
        FakeTimeProvider time,
        FakeScheduleRepository scheduleRepository,
        FakeMarkerRepository? markerRepository = null)
    {
        markerRepository ??= new FakeMarkerRepository();
        return new MainViewModel(
            new ClockViewModel(time),
            new ScheduleViewModel(scheduleRepository, markerRepository, time),
            new TaskListViewModel(new FakeTaskRepository()),
            new NotesViewModel(new FakeNoteRepository(), time),
            new QuickLinksViewModel(new FakeQuickLinkRepository()),
            scheduleRepository,
            markerRepository);
    }

    [Fact]
    public void Constructor_ExposesEachChildViewModel()
    {
        var time = new FakeTimeProvider();
        var viewModel = CreateViewModel(time, new FakeScheduleRepository());

        Assert.NotNull(viewModel.Clock);
        Assert.NotNull(viewModel.Schedule);
        Assert.NotNull(viewModel.Tasks);
        Assert.NotNull(viewModel.Notes);
        Assert.NotNull(viewModel.QuickLinks);
    }

    [Fact]
    public void Refresh_UpdatesClockAndSchedule()
    {
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        var scheduleRepository = new FakeScheduleRepository();
        var viewModel = CreateViewModel(time, scheduleRepository);
        Assert.Equal("No block", viewModel.Schedule.CurrentBlockTitle);

        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero));

        viewModel.Refresh();

        Assert.Equal(time.GetLocalNow(), viewModel.Clock.Now);
        Assert.Equal("Focus", viewModel.Schedule.CurrentBlockTitle);
    }

    [Fact]
    public void OpenScheduleEditor_RaisesScheduleEditorRequestedWithAnEditorViewModel()
    {
        var time = new FakeTimeProvider();
        var viewModel = CreateViewModel(time, new FakeScheduleRepository());

        ScheduleEditorViewModel? requested = null;
        viewModel.ScheduleEditorRequested += editor => requested = editor;
        viewModel.OpenScheduleEditorCommand.Execute(null);

        Assert.NotNull(requested);
    }

    [Fact]
    public void OpenScheduleEditor_WhenEditorSaves_RefreshesSchedule()
    {
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        var scheduleRepository = new FakeScheduleRepository();
        var viewModel = CreateViewModel(time, scheduleRepository);

        ScheduleEditorViewModel? requested = null;
        viewModel.ScheduleEditorRequested += editor => requested = editor;
        viewModel.OpenScheduleEditorCommand.Execute(null);

        requested!.AddBlockCommand.Execute(null);
        requested.Blocks[0].Title = "Focus";
        requested.Blocks[0].Start = "08:00";
        requested.Blocks[0].End = "11:00";
        requested.SaveCommand.Execute(null);

        Assert.Equal("Focus", viewModel.Schedule.CurrentBlockTitle);
    }
}
