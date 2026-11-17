using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// The color swatches in the block editor, and how the chosen color reaches the grid and the saved block.
public class BlockColorEditorTests
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
        public IReadOnlyList<Marker> GetAll() => Array.Empty<Marker>();
        public void ReplaceAll(IEnumerable<Marker> markers) { }
    }

    private static readonly ScheduleBlock Workout = new("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0), "teal");

    private readonly FakeScheduleRepository _blocks = new();

    private ScheduleViewModel Create() =>
        new(_blocks, new FakeMarkerRepository(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)), new TimeDisplay());

    private static void DragNoonToThree(ScheduleViewModel schedule)
    {
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(900, 0);
        schedule.EndDrag();
    }

    private static string[] Selected(BlockEditorViewModel editor) =>
        editor.Swatches.Where(s => s.IsSelected).Select(s => s.Key).ToArray();

    [Fact]
    public void Swatches_AreThePaletteInOrder()
    {
        var editor = Create().BlockEditor;

        Assert.Equal(BlockColor.Keys, editor.Swatches.Select(s => s.Key));
    }

    [Fact]
    public void NewBlock_StartsWithTheDefaultColorSelected()
    {
        var schedule = Create();

        schedule.AddBlockCommand.Execute(null);

        Assert.Equal(BlockColor.Default, schedule.BlockEditor.ColorKey);
        Assert.Equal(new[] { BlockColor.Default }, Selected(schedule.BlockEditor));
    }

    [Fact]
    public void SelectColor_UpdatesTheKeyAndMovesTheRing()
    {
        var schedule = Create();
        schedule.AddBlockCommand.Execute(null);

        schedule.BlockEditor.SelectColorCommand.Execute("sage");

        Assert.Equal("sage", schedule.BlockEditor.ColorKey);
        Assert.Equal(new[] { "sage" }, Selected(schedule.BlockEditor));
    }

    [Fact]
    public void SelectColor_UnknownKey_FallsBackToTheDefault()
    {
        var schedule = Create();
        schedule.AddBlockCommand.Execute(null);

        schedule.BlockEditor.SelectColorCommand.Execute("neon");

        Assert.Equal(BlockColor.Default, schedule.BlockEditor.ColorKey);
    }

    [Fact]
    public void Save_AppliesTheChosenColor()
    {
        var schedule = Create();
        schedule.AddBlockCommand.Execute(null);
        schedule.BlockEditor.Title = "Read";
        schedule.BlockEditor.SelectColorCommand.Execute("lavender");

        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal("lavender", Assert.Single(_blocks.Saved.Blocks).Color);
        Assert.Equal("lavender", Assert.Single(schedule.GridBlocks).ColorKey);
    }

    [Fact]
    public void EditingABlock_ShowsItsCurrentColorSelected()
    {
        _blocks.Saved = new DaySchedule(new[] { Workout });
        var schedule = Create();

        schedule.EditBlockCommand.Execute(schedule.GridBlocks[0]);

        Assert.Equal("teal", schedule.BlockEditor.ColorKey);
        Assert.Equal(new[] { "teal" }, Selected(schedule.BlockEditor));
    }

    [Fact]
    public void EditingABlock_SaveKeepsOrChangesItsColor()
    {
        _blocks.Saved = new DaySchedule(new[] { Workout });
        var schedule = Create();
        schedule.EditBlockCommand.Execute(schedule.GridBlocks[0]);

        schedule.BlockEditor.SelectColorCommand.Execute("amber");
        schedule.BlockEditor.SaveCommand.Execute(null);

        var block = Assert.Single(_blocks.Saved.Blocks);
        Assert.Equal(("Workout", "amber"), (block.Title, block.Color));
    }

    [Fact]
    public void GridBlocks_CarryTheirColorKey()
    {
        _blocks.Saved = new DaySchedule(new[] { Workout, new ScheduleBlock("Read", new TimeOnly(22, 0), new TimeOnly(23, 0)) });

        var schedule = Create();

        Assert.Equal(new[] { "teal", BlockColor.Default }, schedule.GridBlocks.Select(b => b.ColorKey));
    }

    [Fact]
    public void DragPreview_HasNoBlockColorUntilItIsPending()
    {
        var schedule = Create();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(900, 0);

        Assert.Null(schedule.PreviewColorKey);

        schedule.EndDrag();

        Assert.Equal(BlockColor.Default, schedule.PreviewColorKey);
    }

    [Fact]
    public void PendingBlock_TakesTheSwatchColorRightAway()
    {
        var schedule = Create();
        DragNoonToThree(schedule);

        schedule.BlockEditor.SelectColorCommand.Execute("rose");

        Assert.Equal("rose", schedule.PreviewColorKey);
        Assert.Equal(0, _blocks.SaveCalls);
    }

    [Fact]
    public void PendingBlock_SaveStoresTheChosenColor()
    {
        var schedule = Create();
        DragNoonToThree(schedule);
        schedule.BlockEditor.Title = "Deep work";
        schedule.BlockEditor.SelectColorCommand.Execute("terracotta");

        schedule.BlockEditor.SaveCommand.Execute(null);

        Assert.Equal("terracotta", Assert.Single(_blocks.Saved.Blocks).Color);
    }

    [Fact]
    public void PendingBlock_CancelSavesNothing()
    {
        var schedule = Create();
        DragNoonToThree(schedule);
        schedule.BlockEditor.SelectColorCommand.Execute("terracotta");

        schedule.BlockEditor.CancelCommand.Execute(null);

        Assert.Equal(0, _blocks.SaveCalls);
        Assert.False(schedule.IsPreviewVisible);
    }
}
