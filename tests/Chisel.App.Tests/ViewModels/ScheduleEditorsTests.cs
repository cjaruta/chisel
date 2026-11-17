using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// "+ Block" and "+ Marker" on the schedule grid, and the in-window editors they open.
public class ScheduleEditorsTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Saved { get; private set; } = new(Array.Empty<ScheduleBlock>());
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
        public IReadOnlyList<Marker> Saved { get; private set; } = Array.Empty<Marker>();
        public int SaveCalls { get; private set; }
        public IReadOnlyList<Marker> GetAll() => Saved;
        public void ReplaceAll(IEnumerable<Marker> markers)
        {
            Saved = markers.ToList();
            SaveCalls++;
        }
    }

    private readonly FakeScheduleRepository _blocks = new();
    private readonly FakeMarkerRepository _markers = new();
    private readonly TimeDisplay _display = new();

    private ScheduleViewModel CreateAt(int hour, int minute)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, hour, minute, 0, TimeSpan.Zero));
        return new ScheduleViewModel(_blocks, _markers, time, _display);
    }

    [Fact]
    public void Editors_StartClosed()
    {
        var schedule = CreateAt(10, 30);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.False(schedule.MarkerEditor.IsOpen);
    }

    [Theory]
    [InlineData(10, 30, "11:00", "12:00")]
    [InlineData(10, 0, "11:00", "12:00")]
    [InlineData(23, 10, "00:00", "01:00")]
    public void AddBlock_OpensAnEmptyEditorStartingAtTheNextHour(int hour, int minute, string start, string end)
    {
        var schedule = CreateAt(hour, minute);

        schedule.AddBlockCommand.Execute(null);

        var editor = schedule.BlockEditor;
        Assert.True(editor.IsOpen);
        Assert.Equal("", editor.Title);
        Assert.Equal(start, editor.StartText);
        Assert.Equal(end, editor.EndText);
        Assert.Null(editor.ErrorMessage);
    }

    [Fact]
    public void AddBlock_PrefillsTimesInTheTwelveHourFormat()
    {
        _display.Format = ClockFormat.TwelveHour;
        var schedule = CreateAt(13, 15);

        schedule.AddBlockCommand.Execute(null);

        Assert.Equal("2:00 PM", schedule.BlockEditor.StartText);
        Assert.Equal("3:00 PM", schedule.BlockEditor.EndText);
    }

    [Fact]
    public void SaveBlock_AddsTheBlockAndShowsItOnTheGridRightAway()
    {
        _blocks.Save(new DaySchedule(new[] { new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)) }));
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);

        schedule.BlockEditor.Title = "  Workout ";
        schedule.BlockEditor.StartText = "2:00 PM";
        schedule.BlockEditor.EndText = "15:00";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.Equal(new[] { "Focus", "Workout" }, _blocks.Saved.Blocks.Select(b => b.Title));
        var workout = _blocks.Saved.Blocks[1];
        Assert.Equal((new TimeOnly(14, 0), new TimeOnly(15, 0)), (workout.Start, workout.End));
        Assert.Contains(schedule.GridBlocks, b => b.Title == "Workout" && b.StartMinute == 840);
    }

    [Fact]
    public void SaveBlock_OvernightIsAllowed()
    {
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);

        schedule.BlockEditor.Title = "Sleep";
        schedule.BlockEditor.StartText = "23:00";
        schedule.BlockEditor.EndText = "06:30";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Null(schedule.BlockEditor.ErrorMessage);
        Assert.True(Assert.Single(_blocks.Saved.Blocks).CrossesMidnight);
        Assert.Equal(2, schedule.GridBlocks.Count);
    }

    [Fact]
    public void SaveBlock_OverlappingIsAllowed()
    {
        _blocks.Save(new DaySchedule(new[] { new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)) }));
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);

        schedule.BlockEditor.Title = "Call";
        schedule.BlockEditor.StartText = "09:00";
        schedule.BlockEditor.EndText = "10:00";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Null(schedule.BlockEditor.ErrorMessage);
        Assert.Equal(2, _blocks.Saved.Blocks.Count);
    }

    [Theory]
    [InlineData("", "09:00", "10:00", "Enter a title.")]
    [InlineData("   ", "09:00", "10:00", "Enter a title.")]
    [InlineData("Focus", "nine", "10:00", "Enter the start time like 14:30 or 2:30 PM.")]
    [InlineData("Focus", "09:00", "25:00", "Enter the end time like 14:30 or 2:30 PM.")]
    [InlineData("Focus", "09:00", "9:00 AM", "End must be different from start. Overnight blocks are fine.")]
    public void SaveBlock_InvalidInput_ShowsAMessageAndKeepsTheEditorOpen(string title, string start, string end, string message)
    {
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);
        var calls = _blocks.SaveCalls;

        schedule.BlockEditor.Title = title;
        schedule.BlockEditor.StartText = start;
        schedule.BlockEditor.EndText = end;
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal(message, schedule.BlockEditor.ErrorMessage);
        Assert.True(schedule.BlockEditor.IsOpen);
        Assert.Equal(calls, _blocks.SaveCalls);
    }

    [Fact]
    public void CancelBlock_ClosesWithoutSaving()
    {
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);
        schedule.BlockEditor.Title = "Focus";
        var calls = _blocks.SaveCalls;

        schedule.BlockEditor.CancelCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.Equal(calls, _blocks.SaveCalls);
    }

    [Fact]
    public void AddBlock_AfterAFailedSave_StartsFresh()
    {
        var schedule = CreateAt(10, 30);
        schedule.AddBlockCommand.Execute(null);
        schedule.BlockEditor.SaveCommand.Execute(null);
        Assert.NotNull(schedule.BlockEditor.ErrorMessage);
        schedule.BlockEditor.CancelCommand.Execute(null);

        schedule.AddBlockCommand.Execute(null);

        Assert.Null(schedule.BlockEditor.ErrorMessage);
    }

    [Fact]
    public void AddMarker_OpensAnEmptyReminderEditorAtTheNextHour()
    {
        var schedule = CreateAt(10, 30);

        schedule.AddMarkerCommand.Execute(null);

        var editor = schedule.MarkerEditor;
        Assert.True(editor.IsOpen);
        Assert.Equal("", editor.Label);
        Assert.Equal("11:00", editor.TimeText);
        Assert.Equal(MarkerKind.Reminder, editor.Kind);
        Assert.Null(editor.ErrorMessage);
    }

    [Fact]
    public void SaveMarker_AddsTheMarkerAndShowsItOnTheGridRightAway()
    {
        _markers.ReplaceAll(new[] { new Marker("Midday", new TimeOnly(12, 0), MarkerKind.Checkpoint) });
        var schedule = CreateAt(10, 30);
        schedule.AddMarkerCommand.Execute(null);

        schedule.MarkerEditor.Label = " Last coffee ";
        schedule.MarkerEditor.TimeText = "1:00 PM";
        schedule.MarkerEditor.Kind = MarkerKind.Checkpoint;
        schedule.MarkerEditor.SaveCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.Equal(new[] { "Midday", "Last coffee" }, _markers.Saved.Select(m => m.Label));
        Assert.Equal((new TimeOnly(13, 0), MarkerKind.Checkpoint), (_markers.Saved[1].Time, _markers.Saved[1].Kind));
        Assert.Contains(schedule.GridMarkers, m => m.Label == "Last coffee" && m.StartMinute == 780);
    }

    [Theory]
    [InlineData("", "13:00", "Enter a label.")]
    [InlineData("Last coffee", "1 o'clock", "Enter the time like 14:30 or 2:30 PM.")]
    public void SaveMarker_InvalidInput_ShowsAMessageAndKeepsTheEditorOpen(string label, string time, string message)
    {
        var schedule = CreateAt(10, 30);
        schedule.AddMarkerCommand.Execute(null);
        var calls = _markers.SaveCalls;

        schedule.MarkerEditor.Label = label;
        schedule.MarkerEditor.TimeText = time;
        schedule.MarkerEditor.SaveCommand.Execute(null);

        Assert.Equal(message, schedule.MarkerEditor.ErrorMessage);
        Assert.True(schedule.MarkerEditor.IsOpen);
        Assert.Equal(calls, _markers.SaveCalls);
    }

    [Fact]
    public void CancelMarker_ClosesWithoutSaving()
    {
        var schedule = CreateAt(10, 30);
        schedule.AddMarkerCommand.Execute(null);
        var calls = _markers.SaveCalls;

        schedule.MarkerEditor.CancelCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.Equal(calls, _markers.SaveCalls);
    }

    [Fact]
    public void OpeningOneEditor_ClosesTheOther()
    {
        var schedule = CreateAt(10, 30);

        schedule.AddBlockCommand.Execute(null);
        schedule.AddMarkerCommand.Execute(null);
        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.True(schedule.MarkerEditor.IsOpen);

        schedule.AddBlockCommand.Execute(null);
        Assert.True(schedule.BlockEditor.IsOpen);
        Assert.False(schedule.MarkerEditor.IsOpen);
    }

    [Fact]
    public void CloseEditors_ClosesWhicheverIsOpen()
    {
        var schedule = CreateAt(10, 30);
        schedule.AddMarkerCommand.Execute(null);

        schedule.CloseEditorsCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.False(schedule.BlockEditor.IsOpen);
    }

    // --- Editing and deleting what's already on the grid ---

    private ScheduleViewModel CreateWithItems()
    {
        _blocks.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0)),
        }));
        _markers.ReplaceAll(new[]
        {
            new Marker("Midday", new TimeOnly(12, 0), MarkerKind.Checkpoint),
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder),
        });
        return CreateAt(10, 30);
    }

    private static ScheduleGridBlock GridBlock(ScheduleViewModel schedule, string title) =>
        schedule.GridBlocks.First(b => b.Title == title);

    private static ScheduleGridMarker GridMarker(ScheduleViewModel schedule, string label) =>
        schedule.GridMarkers.Single(m => m.Label == label);

    [Fact]
    public void NewEditors_AreNotInEditMode()
    {
        var schedule = CreateAt(10, 30);

        schedule.AddBlockCommand.Execute(null);
        schedule.AddMarkerCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsEditing);
        Assert.Equal("New block", schedule.BlockEditor.Heading);
        Assert.False(schedule.MarkerEditor.IsEditing);
        Assert.Equal("New marker", schedule.MarkerEditor.Heading);
    }

    [Fact]
    public void EditBlock_OpensTheEditorWithItsValuesFilledIn()
    {
        var schedule = CreateWithItems();

        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Workout"));

        var editor = schedule.BlockEditor;
        Assert.True(editor.IsOpen);
        Assert.True(editor.IsEditing);
        Assert.Equal("Edit block", editor.Heading);
        Assert.Equal(("Workout", "14:00", "15:00"), (editor.Title, editor.StartText, editor.EndText));
        Assert.Null(editor.ErrorMessage);
    }

    [Fact]
    public void EditBlock_FillsTimesInTheTwelveHourFormat()
    {
        _display.Format = ClockFormat.TwelveHour;
        var schedule = CreateWithItems();

        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Sleep"));

        Assert.Equal(("11:00 PM", "6:30 AM"), (schedule.BlockEditor.StartText, schedule.BlockEditor.EndText));
    }

    [Fact]
    public void EditBlock_EitherPieceOfAnOvernightBlockEditsTheSameBlock()
    {
        var schedule = CreateWithItems();
        var pieces = schedule.GridBlocks.Where(b => b.Title == "Sleep").ToList();
        Assert.Equal(2, pieces.Count);

        foreach (var piece in pieces)
        {
            schedule.EditBlockCommand.Execute(piece);
            schedule.BlockEditor.Title = "Rest";
            schedule.BlockEditor.SaveCommand.Execute(null);
        }

        Assert.Equal(new[] { "Focus", "Rest", "Workout" }, _blocks.Saved.Blocks.Select(b => b.Title));
    }

    [Fact]
    public void SaveEditedBlock_ReplacesItInPlaceAndUpdatesTheGrid()
    {
        var schedule = CreateWithItems();
        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Focus"));

        schedule.BlockEditor.Title = "Deep work";
        schedule.BlockEditor.StartText = "7:30 AM";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.Equal(new[] { "Deep work", "Sleep", "Workout" }, _blocks.Saved.Blocks.Select(b => b.Title));
        Assert.Equal(new TimeOnly(7, 30), _blocks.Saved.Blocks[0].Start);
        Assert.DoesNotContain(schedule.GridBlocks, b => b.Title == "Focus");
        Assert.Contains(schedule.GridBlocks, b => b.Title == "Deep work" && b.StartMinute == 450);
    }

    [Fact]
    public void SaveEditedBlock_InvalidInput_KeepsTheOriginal()
    {
        var schedule = CreateWithItems();
        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Focus"));
        var calls = _blocks.SaveCalls;

        schedule.BlockEditor.EndText = "08:00";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal("End must be different from start. Overnight blocks are fine.", schedule.BlockEditor.ErrorMessage);
        Assert.True(schedule.BlockEditor.IsOpen);
        Assert.Equal(calls, _blocks.SaveCalls);
    }

    [Fact]
    public void DeleteBlock_RemovesItAndUpdatesTheGrid()
    {
        var schedule = CreateWithItems();
        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Sleep"));

        schedule.BlockEditor.DeleteCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.Equal(new[] { "Focus", "Workout" }, _blocks.Saved.Blocks.Select(b => b.Title));
        Assert.DoesNotContain(schedule.GridBlocks, b => b.Title == "Sleep");
    }

    [Fact]
    public void DeleteBlock_DoesNothingForANewBlock()
    {
        var schedule = CreateWithItems();
        schedule.AddBlockCommand.Execute(null);
        var calls = _blocks.SaveCalls;

        schedule.BlockEditor.DeleteCommand.Execute(null);

        Assert.Equal(calls, _blocks.SaveCalls);
        Assert.Equal(3, _blocks.Saved.Blocks.Count);
    }

    [Fact]
    public void AddBlock_AfterEditing_AddsInsteadOfReplacing()
    {
        var schedule = CreateWithItems();
        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Focus"));
        schedule.BlockEditor.CancelCommand.Execute(null);

        schedule.AddBlockCommand.Execute(null);
        schedule.BlockEditor.Title = "Lunch";
        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal(new[] { "Focus", "Sleep", "Workout", "Lunch" }, _blocks.Saved.Blocks.Select(b => b.Title));
    }

    [Fact]
    public void EditMarker_OpensTheEditorWithItsValuesFilledIn()
    {
        var schedule = CreateWithItems();

        schedule.EditMarkerCommand.Execute(GridMarker(schedule, "Midday"));

        var editor = schedule.MarkerEditor;
        Assert.True(editor.IsOpen);
        Assert.True(editor.IsEditing);
        Assert.Equal("Edit marker", editor.Heading);
        Assert.Equal(("Midday", "12:00", MarkerKind.Checkpoint), (editor.Label, editor.TimeText, editor.Kind));
    }

    [Fact]
    public void SaveEditedMarker_ReplacesItInPlaceAndUpdatesTheGrid()
    {
        var schedule = CreateWithItems();
        schedule.EditMarkerCommand.Execute(GridMarker(schedule, "Last coffee"));

        schedule.MarkerEditor.Label = "Tea";
        schedule.MarkerEditor.TimeText = "3:00 PM";
        schedule.MarkerEditor.Kind = MarkerKind.Checkpoint;
        schedule.MarkerEditor.SaveCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.Equal(new[] { "Midday", "Tea" }, _markers.Saved.Select(m => m.Label));
        Assert.Equal((new TimeOnly(15, 0), MarkerKind.Checkpoint), (_markers.Saved[1].Time, _markers.Saved[1].Kind));
        Assert.Contains(schedule.GridMarkers, m => m.Label == "Tea" && m.StartMinute == 900);
    }

    [Fact]
    public void DeleteMarker_RemovesItAndUpdatesTheGrid()
    {
        var schedule = CreateWithItems();
        schedule.EditMarkerCommand.Execute(GridMarker(schedule, "Midday"));

        schedule.MarkerEditor.DeleteCommand.Execute(null);

        Assert.False(schedule.MarkerEditor.IsOpen);
        Assert.Equal(new[] { "Last coffee" }, _markers.Saved.Select(m => m.Label));
        Assert.DoesNotContain(schedule.GridMarkers, m => m.Label == "Midday");
    }

    [Fact]
    public void EditingOneKind_ClosesTheOtherEditor()
    {
        var schedule = CreateWithItems();

        schedule.EditMarkerCommand.Execute(GridMarker(schedule, "Midday"));
        schedule.EditBlockCommand.Execute(GridBlock(schedule, "Focus"));
        Assert.False(schedule.MarkerEditor.IsOpen);

        schedule.EditMarkerCommand.Execute(GridMarker(schedule, "Midday"));
        Assert.False(schedule.BlockEditor.IsOpen);
    }
}
