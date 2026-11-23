using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Chisel.Core.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// Resizing widgets in layout edit mode: a resize previews live from the weights it started with,
// commits into the working copy on release, and Esc puts the starting weights back.
public class LayoutResizeTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
        public int SaveCalls { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout)
        {
            Saved = layout;
            SaveCalls++;
        }
    }

    private sealed class FakeSettingsRepository : IUserSettingsRepository
    {
        public UserSettings? Get() => null;
        public void Save(UserSettings settings) { }
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Get() => new(Array.Empty<ScheduleBlock>());
        public void Save(DaySchedule schedule) { }
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
        public void Reorder(IReadOnlyList<Guid> orderedIds) { }
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
        public void Update(QuickLink link) { }
        public void Reorder(IReadOnlyList<Guid> orderedIds) { }
    }

    private readonly FakeLayoutRepository _repository = new();

    private static double[] RightHeights(LayoutViewModel layout) =>
        layout.Layout.Columns[1].Widgets.Select(w => w.HeightWeight).ToArray();

    private static readonly double[] DefaultRight =
        WidgetRegistry.DefaultLayout.Columns[1].Widgets.Select(w => w.HeightWeight).ToArray();

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    private MainViewModel CreateMain()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero));
        return new MainViewModel(
            new ClockViewModel(time),
            new ScheduleViewModel(new FakeScheduleRepository(), new FakeMarkerRepository(), time),
            new TaskListViewModel(new FakeTaskRepository()),
            new NotesViewModel(new FakeNoteRepository(), time),
            new QuickLinksViewModel(new FakeQuickLinkRepository()),
            new SettingsViewModel(new FakeSettingsRepository(), new TimeDisplay()),
            new LayoutViewModel(_repository));
    }

    [Fact]
    public void EveryWidget_UsesTheOneMinimumHeight()
    {
        Assert.Equal(110, WidgetRegistry.MinimumHeight);
        Assert.All(WidgetRegistry.All, w => Assert.Equal(WidgetRegistry.MinimumHeight, w.MinHeight));
    }

    [Fact]
    public void Resizing_OnlyInEditMode()
    {
        var layout = new LayoutViewModel(_repository);

        layout.BeginResize();

        Assert.False(layout.IsResizing);
    }

    [Fact]
    public void PreviewResize_ChangesTheShownLayoutLive_FromTheStartingWeights()
    {
        var layout = Editing();
        layout.BeginResize();

        layout.PreviewResize("tasks", 0.05, 0.1);
        layout.PreviewResize("tasks", 0.1, 0.1);

        var heights = RightHeights(layout);
        var total = DefaultRight.Sum();
        Assert.Equal(DefaultRight[0] + 0.1 * total, heights[0], 9);
        Assert.Equal(DefaultRight[1] - 0.1 * total, heights[1], 9);
        Assert.Equal(DefaultRight[2], heights[2], 9);
        Assert.True(layout.IsResizing);
    }

    [Fact]
    public void CommitResize_KeepsTheWeightsInTheWorkingCopyOnly()
    {
        var layout = Editing();
        var saves = _repository.SaveCalls;
        layout.BeginResize();
        layout.PreviewResize("notes", -0.1, 0.1);

        layout.CommitResize();

        Assert.False(layout.IsResizing);
        Assert.NotEqual(DefaultRight, RightHeights(layout));
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Snapshot!.Describe());
        Assert.Equal(saves, _repository.SaveCalls);
    }

    [Fact]
    public void Done_AfterAResize_PersistsTheWeights()
    {
        var layout = Editing();
        layout.BeginResize();
        layout.PreviewResize("tasks", 0.1, 0.1);
        layout.CommitResize();
        var resized = layout.Layout.Describe();

        layout.DoneCommand.Execute(null);

        Assert.Equal(resized, _repository.Saved!.Describe());
        Assert.Equal(resized, new LayoutViewModel(_repository).Layout.Describe());
    }

    [Fact]
    public void Cancel_AfterAResize_RestoresTheSnapshot()
    {
        var layout = Editing();
        layout.BeginResize();
        layout.PreviewResize("tasks", 0.1, 0.1);
        layout.CommitResize();

        layout.CancelCommand.Execute(null);

        Assert.Equal(DefaultRight, RightHeights(layout));
    }

    [Fact]
    public void Reset_AfterAResize_RestoresTheDefaultWeights()
    {
        var layout = Editing();
        layout.BeginResize();
        layout.PreviewResize("notes", 0.15, 0.1);
        layout.CommitResize();

        layout.ResetCommand.Execute(null);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void CancelResize_RestoresTheWeightsFromWhenItBegan()
    {
        var layout = Editing();
        layout.BeginResize();
        layout.PreviewResize("tasks", 0.1, 0.1);
        layout.CommitResize();
        var afterFirst = layout.Layout.Describe();

        layout.BeginResize();
        layout.PreviewResize("notes", 0.1, 0.1);
        layout.CancelResize();

        Assert.Equal(afterFirst, layout.Layout.Describe());
        Assert.False(layout.IsResizing);
        Assert.True(layout.IsEditing);
    }

    [Fact]
    public void LeavingEditMode_EndsAResize()
    {
        var layout = Editing();
        layout.BeginResize();
        layout.PreviewResize("tasks", 0.1, 0.1);

        layout.CancelCommand.Execute(null);

        Assert.False(layout.IsResizing);
    }

    [Fact]
    public void Escape_DuringAResize_RestoresTheStartingWeights_AndASecondEscapeCancelsEditMode()
    {
        var main = CreateMain();
        main.CustomizeLayoutCommand.Execute(null);
        main.Layout.BeginResize();
        main.Layout.PreviewResize("tasks", 0.1, 0.1);

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Layout.IsResizing);
        Assert.True(main.Layout.IsEditing);
        Assert.Equal(DefaultRight, RightHeights(main.Layout));

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Layout.IsEditing);
    }
}
