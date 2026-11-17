using Avalonia;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Chisel.Core.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// Dragging widgets in layout edit mode: the drag state, dropping into the working copy,
// and finding the drop gap under the pointer.
public class LayoutDragTests
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

    private readonly FakeLayoutRepository _repository = new();

    private static string Order(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))));

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
    public void BeginDrag_OnlyInEditModeAndOnlyForAPlacedWidget()
    {
        var layout = new LayoutViewModel(_repository);
        layout.BeginWidgetDrag("tasks");
        Assert.False(layout.IsDraggingWidget);

        layout.EnterEditCommand.Execute(null);
        layout.BeginWidgetDrag("weather");
        Assert.False(layout.IsDraggingWidget);

        layout.BeginWidgetDrag("tasks");
        Assert.True(layout.IsDraggingWidget);
        Assert.Equal("tasks", layout.DraggedWidgetId);
    }

    [Fact]
    public void Drop_MovesTheWidgetInTheWorkingCopyOnly()
    {
        var layout = Editing();
        _repository.Saved = null;
        var saves = _repository.SaveCalls;
        layout.BeginWidgetDrag("links");

        Assert.True(layout.DropWidget(0, 1));

        Assert.False(layout.IsDraggingWidget);
        Assert.Equal("schedule,links | tasks,notes", Order(layout.Layout));
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Snapshot!.Describe());
        Assert.Equal(saves, _repository.SaveCalls);
    }

    [Fact]
    public void Drop_KeepsAnEmptiedColumnInTheWorkingCopy()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("schedule");

        layout.DropWidget(1, 0);

        Assert.Equal(" | schedule,tasks,notes,links", Order(layout.Layout));
        Assert.Empty(layout.Columns[0].Widgets);
    }

    [Fact]
    public void Drop_OntoItsOwnPosition_ChangesNothing()
    {
        var layout = Editing();
        var before = layout.Layout;
        layout.BeginWidgetDrag("notes");

        Assert.False(layout.DropWidget(1, 2));

        Assert.Same(before, layout.Layout);
        Assert.False(layout.IsDraggingWidget);
    }

    [Fact]
    public void Drop_WithoutADrag_DoesNothing()
    {
        var layout = Editing();

        Assert.False(layout.DropWidget(0, 0));

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void Done_AfterAMove_PersistsIt()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("links");
        layout.DropWidget(1, 0);

        layout.DoneCommand.Execute(null);

        Assert.Equal("schedule | links,tasks,notes", Order(_repository.Saved!));
        Assert.Equal("schedule | links,tasks,notes", Order(new LayoutViewModel(_repository).Layout));
    }

    [Fact]
    public void Cancel_AfterAMove_RestoresTheSnapshot()
    {
        var layout = Editing();
        var saves = _repository.SaveCalls;
        layout.BeginWidgetDrag("links");
        layout.DropWidget(1, 0);

        layout.CancelCommand.Execute(null);

        Assert.Equal("schedule | tasks,notes,links", Order(layout.Layout));
        Assert.Equal(saves, _repository.SaveCalls);
    }

    [Fact]
    public void Reset_AfterAMove_RestoresTheDefault()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("schedule");
        layout.DropWidget(1, 4);

        layout.ResetCommand.Execute(null);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void LeavingEditMode_EndsAnyDrag()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("tasks");

        layout.DoneCommand.Execute(null);

        Assert.False(layout.IsDraggingWidget);
        Assert.Null(layout.DraggedWidgetId);
    }

    [Fact]
    public void Escape_DuringADrag_CancelsOnlyTheDrag_AndASecondEscapeCancelsEditMode()
    {
        var main = CreateMain();
        main.CustomizeLayoutCommand.Execute(null);
        main.Layout.BeginWidgetDrag("tasks");

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Layout.IsDraggingWidget);
        Assert.True(main.Layout.IsEditing);
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), main.Layout.Layout.Describe());

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Layout.IsEditing);
    }

    // Two columns side by side with a 16px gap; the right one holds three 100px widgets with 16px gaps.
    private static readonly Rect[] Columns = { new(0, 0, 400, 600), new(416, 0, 300, 600) };
    private static readonly Rect[][] Widgets =
    {
        new[] { new Rect(0, 0, 400, 600) },
        new[] { new Rect(416, 0, 300, 100), new Rect(416, 116, 300, 100), new Rect(416, 232, 300, 100) },
    };

    [Theory]
    [InlineData(500, 10, 1, 0)]     // top half of the first widget
    [InlineData(500, 60, 1, 1)]     // bottom half of the first widget
    [InlineData(500, 108, 1, 1)]    // in the gap below it
    [InlineData(500, 200, 1, 2)]    // bottom half of the second
    [InlineData(500, 500, 1, 3)]    // empty space under the last
    [InlineData(100, 100, 0, 0)]    // top half of the schedule
    [InlineData(100, 500, 0, 1)]    // bottom half of the schedule
    [InlineData(409, 50, 1, 0)]     // just right of the middle of the gap between columns
    [InlineData(407, 50, 0, 0)]     // just left of it
    public void DropTarget_IsTheGapNearestThePointer(double x, double y, int column, int index)
    {
        Assert.Equal((column, index), LayoutDropTargets.Find(new Point(x, y), Columns, Widgets, gap: 16));
    }

    [Theory]
    [InlineData(-30, 100)]
    [InlineData(760, 100)]
    [InlineData(100, -40)]
    [InlineData(100, 700)]
    public void DropTarget_OutsideTheColumns_IsNone(double x, double y)
    {
        Assert.Null(LayoutDropTargets.Find(new Point(x, y), Columns, Widgets, gap: 16));
    }

    [Fact]
    public void DropTarget_InAnEmptyColumn_IsItsOnlyGap()
    {
        var widgets = new[] { Widgets[0], Array.Empty<Rect>() };

        Assert.Equal((1, 0), LayoutDropTargets.Find(new Point(500, 300), Columns, widgets, gap: 16));
    }
}
