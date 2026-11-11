using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class ScheduleEditorViewModelTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Saved = new(Array.Empty<ScheduleBlock>());
        private DaySchedule _schedule = new(Array.Empty<ScheduleBlock>());

        public DaySchedule Get() => _schedule;

        public void Save(DaySchedule schedule)
        {
            _schedule = schedule;
            Saved = schedule;
        }
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> Saved = Array.Empty<Marker>();
        private IReadOnlyList<Marker> _markers = Array.Empty<Marker>();

        public IReadOnlyList<Marker> GetAll() => _markers;

        public void ReplaceAll(IEnumerable<Marker> markers)
        {
            _markers = markers.ToList();
            Saved = _markers;
        }
    }

    [Fact]
    public void Constructor_LoadsExistingBlocksAndMarkersAsEditableRows()
    {
        var scheduleRepository = new FakeScheduleRepository();
        scheduleRepository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0))
        }));
        var markerRepository = new FakeMarkerRepository();
        markerRepository.ReplaceAll(new[] { new Marker("Coffee", new TimeOnly(13, 0), MarkerKind.Reminder) });

        var viewModel = new ScheduleEditorViewModel(scheduleRepository, markerRepository);

        var block = Assert.Single(viewModel.Blocks);
        Assert.Equal("Focus", block.Title);
        Assert.Equal("08:00", block.Start);
        Assert.Equal("11:00", block.End);

        var marker = Assert.Single(viewModel.Markers);
        Assert.Equal("Coffee", marker.Label);
        Assert.Equal("13:00", marker.Time);
        Assert.Equal(MarkerKind.Reminder, marker.Kind);
    }

    [Fact]
    public void AddBlock_AppendsANewEditableRow()
    {
        var viewModel = new ScheduleEditorViewModel(new FakeScheduleRepository(), new FakeMarkerRepository());

        viewModel.AddBlockCommand.Execute(null);

        Assert.Single(viewModel.Blocks);
    }

    [Fact]
    public void RemoveBlock_RemovesTheGivenRow()
    {
        var viewModel = new ScheduleEditorViewModel(new FakeScheduleRepository(), new FakeMarkerRepository());
        viewModel.AddBlockCommand.Execute(null);
        var row = viewModel.Blocks[0];

        viewModel.RemoveBlockCommand.Execute(row);

        Assert.Empty(viewModel.Blocks);
    }

    [Fact]
    public void AddMarker_AppendsANewEditableRow()
    {
        var viewModel = new ScheduleEditorViewModel(new FakeScheduleRepository(), new FakeMarkerRepository());

        viewModel.AddMarkerCommand.Execute(null);

        Assert.Single(viewModel.Markers);
    }

    [Fact]
    public void RemoveMarker_RemovesTheGivenRow()
    {
        var viewModel = new ScheduleEditorViewModel(new FakeScheduleRepository(), new FakeMarkerRepository());
        viewModel.AddMarkerCommand.Execute(null);
        var row = viewModel.Markers[0];

        viewModel.RemoveMarkerCommand.Execute(row);

        Assert.Empty(viewModel.Markers);
    }

    [Fact]
    public void Save_WithValidRows_WritesToBothRepositoriesAndRaisesSaved()
    {
        var scheduleRepository = new FakeScheduleRepository();
        var markerRepository = new FakeMarkerRepository();
        var viewModel = new ScheduleEditorViewModel(scheduleRepository, markerRepository);
        viewModel.AddBlockCommand.Execute(null);
        viewModel.Blocks[0].Title = "Focus";
        viewModel.Blocks[0].Start = "08:00";
        viewModel.Blocks[0].End = "11:00";
        viewModel.AddMarkerCommand.Execute(null);
        viewModel.Markers[0].Label = "Coffee";
        viewModel.Markers[0].Time = "13:00";

        var raised = false;
        viewModel.Saved += () => raised = true;
        viewModel.SaveCommand.Execute(null);

        Assert.True(raised);
        var savedBlock = Assert.Single(scheduleRepository.Saved.Blocks);
        Assert.Equal("Focus", savedBlock.Title);
        Assert.Equal(new TimeOnly(8, 0), savedBlock.Start);
        Assert.Equal(new TimeOnly(11, 0), savedBlock.End);
        var savedMarker = Assert.Single(markerRepository.Saved);
        Assert.Equal("Coffee", savedMarker.Label);
        Assert.Equal(new TimeOnly(13, 0), savedMarker.Time);
    }

    [Fact]
    public void Save_WithUnparsableBlockTime_SetsRowErrorAndDoesNotSave()
    {
        var scheduleRepository = new FakeScheduleRepository();
        var markerRepository = new FakeMarkerRepository();
        var viewModel = new ScheduleEditorViewModel(scheduleRepository, markerRepository);
        viewModel.AddBlockCommand.Execute(null);
        viewModel.Blocks[0].Title = "Focus";
        viewModel.Blocks[0].Start = "not a time";
        viewModel.Blocks[0].End = "11:00";

        var raised = false;
        viewModel.Saved += () => raised = true;
        viewModel.SaveCommand.Execute(null);

        Assert.False(raised);
        Assert.False(string.IsNullOrEmpty(viewModel.Blocks[0].Error));
        Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
        Assert.Empty(scheduleRepository.Saved.Blocks);
    }

    [Fact]
    public void Save_WithBlockStartEqualToEnd_SetsRowErrorAndDoesNotSave()
    {
        var scheduleRepository = new FakeScheduleRepository();
        var viewModel = new ScheduleEditorViewModel(scheduleRepository, new FakeMarkerRepository());
        viewModel.AddBlockCommand.Execute(null);
        viewModel.Blocks[0].Title = "Focus";
        viewModel.Blocks[0].Start = "08:00";
        viewModel.Blocks[0].End = "08:00";

        viewModel.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.Blocks[0].Error));
        Assert.Empty(scheduleRepository.Saved.Blocks);
    }

    [Fact]
    public void Save_WithUnparsableMarkerTime_SetsRowErrorAndDoesNotSave()
    {
        var markerRepository = new FakeMarkerRepository();
        var viewModel = new ScheduleEditorViewModel(new FakeScheduleRepository(), markerRepository);
        viewModel.AddMarkerCommand.Execute(null);
        viewModel.Markers[0].Label = "Coffee";
        viewModel.Markers[0].Time = "not a time";

        viewModel.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.Markers[0].Error));
        Assert.Empty(markerRepository.Saved);
    }

    [Fact]
    public void Save_SupportsOvernightBlocks()
    {
        var scheduleRepository = new FakeScheduleRepository();
        var viewModel = new ScheduleEditorViewModel(scheduleRepository, new FakeMarkerRepository());
        viewModel.AddBlockCommand.Execute(null);
        viewModel.Blocks[0].Title = "Sleep";
        viewModel.Blocks[0].Start = "23:00";
        viewModel.Blocks[0].End = "06:30";

        viewModel.SaveCommand.Execute(null);

        var savedBlock = Assert.Single(scheduleRepository.Saved.Blocks);
        Assert.True(savedBlock.CrossesMidnight);
    }
}
