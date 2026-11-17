using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// A plain click on empty grid space adds a marker: the marker editor opens at the snapped time
// and a pending marker line shows on the grid until Save. Dragging still adds a block.
// Positions are pixels from midnight with no scrolling (1 px per minute).
public class ScheduleClickMarkerTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Saved { get; set; } = new(Array.Empty<ScheduleBlock>());
        public int SaveCalls { get; private set; }
        public DaySchedule Get() => Saved;
        public void Save(DaySchedule schedule)
        {
            Saved = schedule;
            SaveCalls++;
        }
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> Saved { get; set; } = Array.Empty<Marker>();
        public int SaveCalls { get; private set; }
        public IReadOnlyList<Marker> GetAll() => Saved;
        public void ReplaceAll(IEnumerable<Marker> markers)
        {
            Saved = markers.ToList();
            SaveCalls++;
        }
    }

    private static readonly ScheduleBlock Workout = new("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
    private static readonly Marker Coffee = new("Last coffee", new TimeOnly(13, 0), MarkerKind.Checkpoint);

    private readonly FakeScheduleRepository _blocks = new();
    private readonly FakeMarkerRepository _markers = new();
    private readonly TimeDisplay _display = new();

    private ScheduleViewModel Create() =>
        new(_blocks, _markers,
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)), _display);

    private static void ClickAt(ScheduleViewModel schedule, double y)
    {
        schedule.BeginDrag(y, 0);
        schedule.UpdateDrag(y + 1, 0);
        schedule.EndDrag();
    }

    [Theory]
    [InlineData(727, "12:00", 720)]
    [InlineData(728, "12:15", 735)]
    public void Click_OpensTheMarkerEditorAtTheSnappedTimeWithAPendingMarker(double y, string time, double minute)
    {
        var schedule = Create();

        ClickAt(schedule, y);

        var editor = schedule.MarkerEditor;
        Assert.True(editor.IsOpen);
        Assert.False(editor.IsEditing);
        Assert.Equal("", editor.Label);
        Assert.Equal(time, editor.TimeText);
        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.True(schedule.IsMarkerPending);
        Assert.Equal(minute, schedule.PendingMarkerMinute);
        Assert.Equal(0, _markers.SaveCalls);
    }

    [Fact]
    public void Click_UsesTheSameKindAsTheAddMarkerButton()
    {
        var schedule = Create();
        schedule.AddMarkerCommand.Execute(null);
        var buttonKind = schedule.MarkerEditor.Kind;
        schedule.CloseEditorsCommand.Execute(null);

        ClickAt(schedule, 720);

        Assert.Equal(buttonKind, schedule.MarkerEditor.Kind);
    }

    [Fact]
    public void Click_InTwelveHourFormat_PrefillsTheTimeThatWay()
    {
        _display.Format = ClockFormat.TwelveHour;
        var schedule = Create();

        ClickAt(schedule, 15 * 60 + 15);

        Assert.Equal("3:15 PM", schedule.MarkerEditor.TimeText);
        Assert.Equal("3:15 PM", schedule.PendingMarkerTime);
    }

    [Fact]
    public void PendingMarker_FollowsTheLabelTimeAndKindTypedInTheEditor()
    {
        var schedule = Create();
        ClickAt(schedule, 720);
        Assert.Equal("New marker", schedule.PendingMarkerLabel);

        schedule.MarkerEditor.Label = "Stretch";
        schedule.MarkerEditor.TimeText = "12:45";
        schedule.MarkerEditor.Kind = MarkerKind.Checkpoint;

        Assert.Equal("Stretch", schedule.PendingMarkerLabel);
        Assert.Equal(765, schedule.PendingMarkerMinute);
        Assert.Equal("12:45", schedule.PendingMarkerTime);
        Assert.True(schedule.IsPendingMarkerCheckpoint);
    }

    [Fact]
    public void Escape_DiscardsThePendingMarkerWithNothingSaved()
    {
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.CloseEditorsCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.False(schedule.IsMarkerPending);
        Assert.Equal(0, _markers.SaveCalls);
    }

    [Fact]
    public void Cancel_DiscardsThePendingMarkerWithNothingSaved()
    {
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.MarkerEditor.CancelCommand.Execute(null);

        Assert.False(schedule.IsMarkerPending);
        Assert.Equal(0, _markers.SaveCalls);
    }

    [Fact]
    public void Save_StoresExactlyOneMarkerAndClearsThePending()
    {
        _markers.Saved = new[] { Coffee };
        var schedule = Create();
        ClickAt(schedule, 735);
        schedule.MarkerEditor.Label = "Stretch";

        schedule.MarkerEditor.SaveCommand.Execute(null);

        Assert.Equal(1, _markers.SaveCalls);
        Assert.Equal(2, _markers.Saved.Count);
        var added = _markers.Saved[1];
        Assert.Equal(("Stretch", new TimeOnly(12, 15), MarkerKind.Reminder), (added.Label, added.Time, added.Kind));
        Assert.False(schedule.IsMarkerPending);
        Assert.Equal(2, schedule.GridMarkers.Count);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void Save_WithoutALabel_KeepsThePendingMarker()
    {
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.MarkerEditor.SaveCommand.Execute(null);

        Assert.NotNull(schedule.MarkerEditor.ErrorMessage);
        Assert.True(schedule.IsMarkerPending);
        Assert.Equal(0, _markers.SaveCalls);
    }

    [Fact]
    public void RealDrag_StillOpensTheBlockEditorWithTheDraggedTimes()
    {
        var schedule = Create();

        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(915, 0);
        schedule.EndDrag();

        Assert.True(schedule.BlockEditor.IsOpen);
        Assert.Equal("12:00", schedule.BlockEditor.StartText);
        Assert.Equal("15:15", schedule.BlockEditor.EndText);
        Assert.True(schedule.IsPending);
        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.False(schedule.IsMarkerPending);
    }

    [Fact]
    public void RealDragAfterAClick_ReplacesThePendingMarkerWithAPendingBlock()
    {
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.BeginDrag(780, 0);
        schedule.UpdateDrag(900, 0);
        schedule.EndDrag();

        Assert.False(schedule.IsMarkerPending);
        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.True(schedule.IsPending);
    }

    [Fact]
    public void ClickAfterADrag_ReplacesThePendingBlockWithAPendingMarker()
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(915, 0);
        schedule.EndDrag();

        ClickAt(schedule, 1000);

        Assert.False(schedule.IsPending);
        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.True(schedule.IsMarkerPending);
    }

    [Fact]
    public void ClickOnAnExistingBlock_StillOpensItForEditing()
    {
        _blocks.Saved = new DaySchedule(new[] { Workout });
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.EditBlockCommand.Execute(schedule.GridBlocks[0]);

        Assert.True(schedule.BlockEditor.IsEditing);
        Assert.Equal("Workout", schedule.BlockEditor.Title);
        Assert.False(schedule.IsMarkerPending);
    }

    [Fact]
    public void ClickOnAnExistingMarker_StillOpensItForEditing()
    {
        _markers.Saved = new[] { Coffee };
        var schedule = Create();
        ClickAt(schedule, 720);

        schedule.EditMarkerCommand.Execute(schedule.GridMarkers[0]);

        Assert.True(schedule.MarkerEditor.IsEditing);
        Assert.Equal("Last coffee", schedule.MarkerEditor.Label);
        Assert.False(schedule.IsMarkerPending);
    }

    [Fact]
    public void AddMarkerButton_ShowsNoPendingMarker()
    {
        var schedule = Create();

        schedule.AddMarkerCommand.Execute(null);

        Assert.True(schedule.MarkerEditor.IsOpen);
        Assert.False(schedule.IsMarkerPending);
    }

    [Fact]
    public void AddButtons_ClearAPendingMarker()
    {
        var schedule = Create();
        ClickAt(schedule, 720);
        schedule.AddMarkerCommand.Execute(null);
        Assert.False(schedule.IsMarkerPending);

        ClickAt(schedule, 720);
        schedule.AddBlockCommand.Execute(null);
        Assert.False(schedule.IsMarkerPending);
    }
}
