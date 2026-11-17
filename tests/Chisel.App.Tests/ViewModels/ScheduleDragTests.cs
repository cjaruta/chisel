using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// Dragging on empty grid space to create a block: live preview, then a pending block in the editor.
// Positions are pixels from midnight with no scrolling (1 px per minute).
public class ScheduleDragTests
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
        public IReadOnlyList<Marker> Saved { get; private set; } = Array.Empty<Marker>();
        public IReadOnlyList<Marker> GetAll() => Saved;
        public void ReplaceAll(IEnumerable<Marker> markers) => Saved = markers.ToList();
    }

    private readonly FakeScheduleRepository _blocks = new();
    private readonly TimeDisplay _display = new();

    private ScheduleViewModel Create() =>
        new(_blocks, new FakeMarkerRepository(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)), _display);

    // Press at noon, drag to 15:15, release.
    private static void DragNoonToQuarterPastThree(ScheduleViewModel schedule)
    {
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(800, 0);
        schedule.UpdateDrag(915, 0);
        schedule.EndDrag();
    }

    [Fact]
    public void BeginDrag_TracksThePressButShowsNoPreviewYet()
    {
        var schedule = Create();

        schedule.BeginDrag(720, 0);

        Assert.True(schedule.IsDragActive);
        Assert.False(schedule.IsPreviewVisible);
    }

    [Fact]
    public void UpdateDrag_ShowsAPreviewOverTheDraggedRange()
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);

        schedule.UpdateDrag(915, 0);

        Assert.True(schedule.IsPreviewVisible);
        Assert.False(schedule.IsPending);
        Assert.Equal(720, schedule.PreviewStartMinute);
        Assert.Equal(915, schedule.PreviewEndMinute);
        Assert.Equal("12:00-15:15", schedule.PreviewLabel);
    }

    [Fact]
    public void UpdateDrag_Upward_PutsTheEarlierTimeFirst()
    {
        var schedule = Create();
        schedule.BeginDrag(915, 0);

        schedule.UpdateDrag(720, 0);

        Assert.Equal(720, schedule.PreviewStartMinute);
        Assert.Equal(915, schedule.PreviewEndMinute);
    }

    [Fact]
    public void PreviewLabel_FollowsTheClockFormat()
    {
        _display.Format = ClockFormat.TwelveHour;
        var schedule = Create();
        schedule.BeginDrag(720, 0);

        schedule.UpdateDrag(915, 0);

        Assert.Equal(_display.Range(new TimeOnly(12, 0), new TimeOnly(15, 15)), schedule.PreviewLabel);
        Assert.Contains("PM", schedule.PreviewLabel);
    }

    [Fact]
    public void UpdateDrag_FollowsScrollingDuringTheDrag()
    {
        var schedule = Create();
        schedule.BeginDrag(100, 620);

        // The pointer stays put while the grid auto-scrolls 195 px further down.
        schedule.UpdateDrag(100, 815);

        Assert.Equal(720, schedule.PreviewStartMinute);
        Assert.Equal(915, schedule.PreviewEndMinute);
    }

    [Fact]
    public void RealDrag_HidesTheGuideLineAndIgnoresHover()
    {
        var schedule = Create();
        schedule.HoverAt(new TimeOnly(12, 0));
        schedule.BeginDrag(720, 0);

        schedule.UpdateDrag(915, 0);
        schedule.HoverAt(new TimeOnly(15, 15));

        Assert.False(schedule.IsHoverVisible);
    }

    [Fact]
    public void EndDrag_OpensTheEditorWithAPendingBlockAndSavesNothing()
    {
        var schedule = Create();

        DragNoonToQuarterPastThree(schedule);

        var editor = schedule.BlockEditor;
        Assert.True(editor.IsOpen);
        Assert.False(editor.IsEditing);
        Assert.Equal("", editor.Title);
        Assert.Equal("12:00", editor.StartText);
        Assert.Equal("15:15", editor.EndText);
        Assert.False(schedule.IsDragActive);
        Assert.True(schedule.IsPending);
        Assert.True(schedule.IsPreviewVisible);
        Assert.Equal((720.0, 915.0), (schedule.PreviewStartMinute, schedule.PreviewEndMinute));
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void EndDrag_InTwelveHourFormat_PrefillsNoonToQuarterPastThreePm()
    {
        _display.Format = ClockFormat.TwelveHour;
        var schedule = Create();

        DragNoonToQuarterPastThree(schedule);

        Assert.Equal(_display.Time(new TimeOnly(12, 0)), schedule.BlockEditor.StartText);
        Assert.Equal("3:15 PM", schedule.BlockEditor.EndText);
    }

    [Fact]
    public void EndDrag_ClosesTheMarkerEditor()
    {
        var schedule = Create();
        schedule.AddMarkerCommand.Execute(null);

        DragNoonToQuarterPastThree(schedule);

        Assert.False(schedule.MarkerEditor.IsOpen);
    }

    [Fact]
    public void Save_StoresExactlyOneBlockAndClearsThePending()
    {
        _blocks.Saved = new DaySchedule(new[] { new ScheduleBlock("Lunch", new TimeOnly(12, 0), new TimeOnly(13, 0)) });
        var schedule = Create();
        DragNoonToQuarterPastThree(schedule);
        schedule.BlockEditor.Title = "Deep work";

        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal(1, _blocks.SaveCalls);
        Assert.Equal(2, _blocks.Saved.Blocks.Count);
        var added = _blocks.Saved.Blocks[1];
        Assert.Equal(("Deep work", new TimeOnly(12, 0), new TimeOnly(15, 15)), (added.Title, added.Start, added.End));
        Assert.False(schedule.IsPending);
        Assert.False(schedule.IsPreviewVisible);
    }

    [Fact]
    public void Save_WithAnInvalidEditor_KeepsThePendingBlock()
    {
        var schedule = Create();
        DragNoonToQuarterPastThree(schedule);

        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.NotNull(schedule.BlockEditor.ErrorMessage);
        Assert.True(schedule.IsPending);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void CancelInTheEditor_DiscardsThePendingBlockAndSavesNothing()
    {
        var schedule = Create();
        DragNoonToQuarterPastThree(schedule);

        schedule.BlockEditor.CancelCommand.Execute(null);

        Assert.False(schedule.IsPending);
        Assert.False(schedule.IsPreviewVisible);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void EscapeAfterTheDrag_DiscardsThePendingBlock()
    {
        var schedule = Create();
        DragNoonToQuarterPastThree(schedule);

        schedule.CloseEditorsCommand.Execute(null);

        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.False(schedule.IsPending);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void EscapeDuringTheDrag_CancelsItWithNothingCreated()
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(915, 0);

        schedule.CloseEditorsCommand.Execute(null);
        schedule.UpdateDrag(1000, 0);
        schedule.EndDrag();

        Assert.False(schedule.IsDragActive);
        Assert.False(schedule.IsPreviewVisible);
        Assert.False(schedule.BlockEditor.IsOpen);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void EscapeDuringTheDrag_LeavesAnOpenEditorAlone()
    {
        var schedule = Create();
        schedule.AddMarkerCommand.Execute(null);
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(915, 0);

        schedule.CloseEditorsCommand.Execute(null);

        Assert.True(schedule.MarkerEditor.IsOpen);
    }

    [Theory]
    [InlineData(722)] // under the pixel threshold
    [InlineData(726)] // past the threshold but still the same snapped time
    public void SmallMovement_IsAClickThatOpensANewBlockAtThePress(double releaseY)
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(releaseY, 0);

        Assert.False(schedule.IsPreviewVisible);
        schedule.EndDrag();

        Assert.True(schedule.BlockEditor.IsOpen);
        Assert.Equal("12:00", schedule.BlockEditor.StartText);
        Assert.Equal("13:00", schedule.BlockEditor.EndText);
        Assert.False(schedule.IsPending);
    }

    [Fact]
    public void DragOnce_StaysADragWhenThePointerComesBack()
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(800, 0);

        schedule.UpdateDrag(721, 0);
        schedule.EndDrag();

        Assert.True(schedule.IsPending);
        Assert.Equal("12:15", schedule.BlockEditor.EndText);
    }

    [Fact]
    public void DragPastTheBottom_EndsAtMidnightStoredAsZeroHundred()
    {
        var schedule = Create();
        schedule.BeginDrag(1380, 0);
        schedule.UpdateDrag(1600, 0);
        schedule.EndDrag();
        schedule.BlockEditor.Title = "Wind down";

        Assert.Equal(1440, schedule.PreviewEndMinute);
        Assert.Equal("00:00", schedule.BlockEditor.EndText);
        schedule.BlockEditor.SaveCommand.Execute(null);

        var block = Assert.Single(_blocks.Saved.Blocks);
        Assert.Equal((new TimeOnly(23, 0), new TimeOnly(0, 0)), (block.Start, block.End));
    }

    [Fact]
    public void PendingBlock_FollowsTimesAndTitleTypedInTheEditor()
    {
        var schedule = Create();
        DragNoonToQuarterPastThree(schedule);

        schedule.BlockEditor.Title = "Deep work";
        schedule.BlockEditor.EndText = "16:00";

        Assert.Equal("Deep work", schedule.PreviewTitle);
        Assert.Equal(960, schedule.PreviewEndMinute);
        Assert.Equal("12:00-16:00", schedule.PreviewLabel);
    }

    [Fact]
    public void PendingBlock_WithoutATitle_IsCalledNewBlock()
    {
        var schedule = Create();

        DragNoonToQuarterPastThree(schedule);

        Assert.Equal("New block", schedule.PreviewTitle);
    }
}
