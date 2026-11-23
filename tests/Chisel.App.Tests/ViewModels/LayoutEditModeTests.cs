using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Chisel.Core.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// Layout edit mode: entering snapshots the layout and edits a working copy;
// Done saves it, Cancel (or Esc) restores the snapshot, Reset swaps in the default until Done.
public class LayoutEditModeTests
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
    }

    // A saved layout that differs from the default: three columns.
    private static readonly WidgetLayout Custom = new(new[]
    {
        new LayoutColumn(1, new[] { new WidgetPlacement("tasks"), new WidgetPlacement("links") }),
        new LayoutColumn(2, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(1, new[] { new WidgetPlacement("notes") }),
    }, new[] { "clock", "weather", "media", "calculator", "scripture" });

    private static readonly WidgetLayout Changed = new(new[]
    {
        new LayoutColumn(1, new[] { new WidgetPlacement("schedule"), new WidgetPlacement("tasks"), new WidgetPlacement("notes"), new WidgetPlacement("links") }),
    }, new[] { "clock", "weather", "media", "calculator", "scripture" });

    private readonly FakeLayoutRepository _repository = new() { Saved = Custom };

    private LayoutViewModel CreateLayout() => new(_repository);

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
            CreateLayout());
    }

    [Fact]
    public void StartsOutOfEditMode()
    {
        var layout = CreateLayout();

        Assert.False(layout.IsEditing);
        Assert.Null(layout.Snapshot);
        Assert.False(layout.DoneCommand.CanExecute(null));
        Assert.False(layout.CancelCommand.CanExecute(null));
        Assert.False(layout.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void Enter_TakesASnapshotOfTheCurrentLayout()
    {
        var layout = CreateLayout();

        layout.EnterEditCommand.Execute(null);

        Assert.True(layout.IsEditing);
        Assert.Equal(Custom.Describe(), layout.Snapshot!.Describe());
        Assert.Equal(Custom.Describe(), layout.Layout.Describe());
        Assert.True(layout.DoneCommand.CanExecute(null));
        Assert.False(layout.EnterEditCommand.CanExecute(null));
    }

    [Fact]
    public void ChangesWhileEditing_GoToTheWorkingCopyAndAreNotSavedYet()
    {
        var layout = CreateLayout();
        layout.EnterEditCommand.Execute(null);

        layout.Apply(Changed);

        Assert.Equal(Changed.Describe(), layout.Layout.Describe());
        Assert.Equal(Custom.Describe(), layout.Snapshot!.Describe());
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public void Done_SavesTheWorkingCopyAndExits()
    {
        var layout = CreateLayout();
        layout.EnterEditCommand.Execute(null);
        layout.Apply(Changed);

        layout.DoneCommand.Execute(null);

        Assert.False(layout.IsEditing);
        Assert.Null(layout.Snapshot);
        Assert.Equal(1, _repository.SaveCalls);
        Assert.Equal(Changed.Describe(), _repository.Saved!.Describe());
        Assert.Equal(Changed.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void Cancel_RestoresTheSnapshotAndExitsWithoutSaving()
    {
        var layout = CreateLayout();
        layout.EnterEditCommand.Execute(null);
        layout.Apply(Changed);

        layout.CancelCommand.Execute(null);

        Assert.False(layout.IsEditing);
        Assert.Equal(Custom.Describe(), layout.Layout.Describe());
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public void Reset_SwapsInTheDefaultLayoutButOnlySavesOnDone()
    {
        var layout = CreateLayout();
        layout.EnterEditCommand.Execute(null);

        layout.ResetCommand.Execute(null);

        Assert.True(layout.IsEditing);
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
        Assert.Equal(0, _repository.SaveCalls);

        layout.DoneCommand.Execute(null);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), _repository.Saved!.Describe());
    }

    [Fact]
    public void ResetThenCancel_KeepsTheOldLayout()
    {
        var layout = CreateLayout();
        layout.EnterEditCommand.Execute(null);
        layout.ResetCommand.Execute(null);

        layout.CancelCommand.Execute(null);

        Assert.Equal(Custom.Describe(), layout.Layout.Describe());
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public void Customize_EntersEditModeAndClosesSettingsAndScheduleEditors()
    {
        var main = CreateMain();
        main.Settings.IsOpen = true;
        main.Schedule.AddBlockCommand.Execute(null);
        main.Schedule.HoverAt(new TimeOnly(12, 0));

        main.CustomizeLayoutCommand.Execute(null);

        Assert.True(main.Layout.IsEditing);
        Assert.False(main.Settings.IsOpen);
        Assert.False(main.Schedule.BlockEditor.IsOpen);
        Assert.False(main.Schedule.MarkerEditor.IsOpen);
        Assert.False(main.Schedule.IsHoverVisible);
    }

    [Fact]
    public void Customize_CancelsADragInProgress()
    {
        var main = CreateMain();
        main.Schedule.BeginDrag(720, 0);
        main.Schedule.UpdateDrag(900, 0);

        main.CustomizeLayoutCommand.Execute(null);

        Assert.False(main.Schedule.IsDragActive);
        Assert.False(main.Schedule.IsPreviewVisible);
    }

    [Fact]
    public void SettingsGear_IsDisabledWhileEditing()
    {
        var main = CreateMain();
        var changed = new List<string?>();
        main.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.True(main.CanOpenSettings);

        main.CustomizeLayoutCommand.Execute(null);
        Assert.False(main.CanOpenSettings);
        Assert.Contains(nameof(MainViewModel.CanOpenSettings), changed);

        main.Layout.DoneCommand.Execute(null);
        Assert.True(main.CanOpenSettings);
    }

    [Fact]
    public void Escape_WhileEditing_CancelsEditMode()
    {
        var main = CreateMain();
        main.CustomizeLayoutCommand.Execute(null);
        main.Layout.Apply(Changed);

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Layout.IsEditing);
        Assert.Equal(Custom.Describe(), main.Layout.Layout.Describe());
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public void Escape_OutsideEditMode_StillClosesPanels()
    {
        var main = CreateMain();
        main.Settings.IsOpen = true;

        main.CloseOverlaysCommand.Execute(null);

        Assert.False(main.Settings.IsOpen);
        Assert.False(main.Layout.IsEditing);
    }
}
