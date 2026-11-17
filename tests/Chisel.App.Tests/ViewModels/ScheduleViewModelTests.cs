using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class ScheduleViewModelTests
{
    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        private DaySchedule _schedule = new(Array.Empty<ScheduleBlock>());

        public int GetCalls { get; private set; }
        public DaySchedule Get()
        {
            GetCalls++;
            return _schedule;
        }
        public void Save(DaySchedule schedule) => _schedule = schedule;
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        private IReadOnlyList<Marker> _markers = Array.Empty<Marker>();

        public IReadOnlyList<Marker> GetAll() => _markers;
        public void ReplaceAll(IEnumerable<Marker> markers) => _markers = markers.ToList();
    }

    private static FakeTimeProvider At(int hour, int minute, int second = 0) =>
        new(new DateTimeOffset(2026, 11, 10, hour, minute, second, TimeSpan.Zero));

    private static FakeScheduleRepository Blocks(params ScheduleBlock[] blocks)
    {
        var repository = new FakeScheduleRepository();
        repository.Save(new DaySchedule(blocks));
        return repository;
    }

    private static FakeMarkerRepository Markers(params Marker[] markers)
    {
        var repository = new FakeMarkerRepository();
        repository.ReplaceAll(markers);
        return repository;
    }

    private static readonly ScheduleBlock Focus = new("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0));
    private static readonly ScheduleBlock Workout = new("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
    private static readonly ScheduleBlock Sleep = new("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30));
    private static readonly Marker Coffee = new("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder);

    [Fact]
    public void Constructor_WhenNowIsInABlock_SetsCurrentBlockTitle()
    {
        var viewModel = new ScheduleViewModel(Blocks(Focus), new FakeMarkerRepository(), At(9, 0));

        Assert.Equal("Focus", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Constructor_WhenNowIsNotInABlock_SetsNoBlockText()
    {
        var viewModel = new ScheduleViewModel(Blocks(Focus), new FakeMarkerRepository(), At(12, 0));

        Assert.Equal("No block", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void Refresh_AfterScheduleChanges_ReloadsTheGrid()
    {
        var repository = new FakeScheduleRepository();
        var viewModel = new ScheduleViewModel(repository, new FakeMarkerRepository(), At(9, 0));
        Assert.Empty(viewModel.GridBlocks);

        repository.Save(new DaySchedule(new[] { Focus }));
        viewModel.Refresh();

        Assert.Equal("Focus", Assert.Single(viewModel.GridBlocks).Title);
        Assert.Equal("Focus", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void GridBlocks_ArePlacedByMinutesWithTheirTimeRange()
    {
        var viewModel = new ScheduleViewModel(Blocks(Focus, Workout), new FakeMarkerRepository(), At(9, 0));

        var focus = viewModel.GridBlocks.Single(b => b.Title == "Focus");
        Assert.Equal((480.0, 660.0), (focus.StartMinute, focus.EndMinute));
        Assert.Equal((0, 1), (focus.Column, focus.ColumnCount));
        Assert.Equal(0, focus.BlockIndex);
        Assert.Equal("08:00-11:00", focus.TimeLabel);
        Assert.Equal(1, viewModel.GridBlocks.Single(b => b.Title == "Workout").BlockIndex);
    }

    [Fact]
    public void GridBlocks_OverlappingBlocksGetColumns()
    {
        var callBlock = new ScheduleBlock("Call", new TimeOnly(9, 0), new TimeOnly(10, 0));
        var viewModel = new ScheduleViewModel(Blocks(Focus, callBlock), new FakeMarkerRepository(), At(9, 0));

        var focus = viewModel.GridBlocks.Single(b => b.Title == "Focus");
        var call = viewModel.GridBlocks.Single(b => b.Title == "Call");
        Assert.Equal((0, 2), (focus.Column, focus.ColumnCount));
        Assert.Equal((1, 2), (call.Column, call.ColumnCount));
    }

    [Fact]
    public void GridBlocks_OvernightBlockShowsInTheEarlyMorningAndLateNight()
    {
        var viewModel = new ScheduleViewModel(Blocks(Sleep), new FakeMarkerRepository(), At(12, 0));

        var pieces = viewModel.GridBlocks.Where(b => b.Title == "Sleep").OrderBy(b => b.StartMinute).ToList();
        Assert.Equal(2, pieces.Count);
        Assert.Equal((0.0, 390.0), (pieces[0].StartMinute, pieces[0].EndMinute));
        Assert.Equal((1380.0, 1440.0), (pieces[1].StartMinute, pieces[1].EndMinute));
        Assert.All(pieces, p => Assert.Equal("23:00-06:30", p.TimeLabel));
    }

    [Fact]
    public void GridBlocks_ShortBlocksAreFlaggedForSingleLineText()
    {
        var wake = new ScheduleBlock("Wake up", new TimeOnly(6, 30), new TimeOnly(7, 0));
        var viewModel = new ScheduleViewModel(Blocks(wake, Focus), new FakeMarkerRepository(), At(9, 0));

        Assert.True(viewModel.GridBlocks.Single(b => b.Title == "Wake up").IsShort);
        Assert.False(viewModel.GridBlocks.Single(b => b.Title == "Focus").IsShort);
    }

    [Fact]
    public void GridBlocks_FlagCurrentAndDimPast()
    {
        var viewModel = new ScheduleViewModel(Blocks(Focus, Workout), new FakeMarkerRepository(), At(9, 0));
        var focus = viewModel.GridBlocks.Single(b => b.Title == "Focus");
        var workout = viewModel.GridBlocks.Single(b => b.Title == "Workout");
        Assert.True(focus.IsCurrent);
        Assert.False(focus.IsPast);
        Assert.False(workout.IsCurrent);
        Assert.False(workout.IsPast);
    }

    [Theory]
    [InlineData(22, 59, false, true, false)]  // before it starts: the morning piece is already behind us
    [InlineData(12, 0, false, true, false)]   // midday: morning piece past, tonight's piece upcoming
    [InlineData(23, 30, true, false, false)]  // running: both pieces are current
    [InlineData(2, 0, true, false, false)]
    [InlineData(6, 30, false, true, false)]   // just ended
    public void GridBlocks_OvernightPieces_FlagCurrentAndPastPerPiece(
        int hour, int minute, bool expectedCurrent, bool morningPast, bool nightPast)
    {
        var viewModel = new ScheduleViewModel(Blocks(Sleep), new FakeMarkerRepository(), At(hour, minute));

        var morning = viewModel.GridBlocks.Single(b => b.StartMinute == 0);
        var night = viewModel.GridBlocks.Single(b => b.StartMinute == 1380);
        Assert.Equal(expectedCurrent, morning.IsCurrent);
        Assert.Equal(expectedCurrent, night.IsCurrent);
        Assert.Equal(morningPast, morning.IsPast);
        Assert.Equal(nightPast, night.IsPast);
    }

    [Fact]
    public void GridMarkers_ArePointsAtTheirTimeWithKindAndPastFlag()
    {
        var checkpoint = new Marker("Midday", new TimeOnly(12, 0), MarkerKind.Checkpoint);
        var viewModel = new ScheduleViewModel(new FakeScheduleRepository(), Markers(Coffee, checkpoint), At(12, 30));

        var coffee = viewModel.GridMarkers.Single(m => m.Label == "Last coffee");
        Assert.Equal(780.0, coffee.StartMinute);
        Assert.True(double.IsNaN(coffee.EndMinute));
        Assert.Equal("13:00", coffee.TimeLabel);
        Assert.False(coffee.IsCheckpoint);
        Assert.False(coffee.IsPast);
        Assert.Equal(0, coffee.MarkerIndex);

        var midday = viewModel.GridMarkers.Single(m => m.Label == "Midday");
        Assert.True(midday.IsCheckpoint);
        Assert.True(midday.IsPast);
        Assert.Equal(1, midday.MarkerIndex);
    }

    [Fact]
    public void HourLabels_RunFromMidnightToMidnightInTheClockFormat()
    {
        var display = new TimeDisplay();
        var viewModel = new ScheduleViewModel(new FakeScheduleRepository(), new FakeMarkerRepository(), At(9, 0), display);

        Assert.Equal(25, viewModel.HourLabels.Count);
        Assert.Equal(("00:00", 0.0), (viewModel.HourLabels[0].Text, viewModel.HourLabels[0].StartMinute));
        Assert.Equal(("13:00", 780.0), (viewModel.HourLabels[13].Text, viewModel.HourLabels[13].StartMinute));
        Assert.Equal(("00:00", 1440.0), (viewModel.HourLabels[24].Text, viewModel.HourLabels[24].StartMinute));

        display.Format = ClockFormat.TwelveHour;

        Assert.Equal("12:00 AM", viewModel.HourLabels[0].Text);
        Assert.Equal("1:00 PM", viewModel.HourLabels[13].Text);
    }

    [Fact]
    public void Now_IsPlacedByMinutesAndLabelledInTheClockFormat()
    {
        var display = new TimeDisplay { Format = ClockFormat.TwelveHour };
        var viewModel = new ScheduleViewModel(new FakeScheduleRepository(), new FakeMarkerRepository(), At(10, 30, 30), display);

        Assert.Equal(630.5, viewModel.NowMinute);
        Assert.Equal("10:30 AM", viewModel.NowLabel);
    }

    [Fact]
    public void UpdateNow_MovesTheNowLineAndFlagsWithoutReloading()
    {
        var time = At(10, 59);
        var repository = Blocks(Focus, Workout);
        var viewModel = new ScheduleViewModel(repository, Markers(Coffee), time);
        var focus = viewModel.GridBlocks.Single(b => b.Title == "Focus");
        var calls = repository.GetCalls;

        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero));
        viewModel.UpdateNow();

        Assert.Equal(calls, repository.GetCalls);
        Assert.Same(focus, viewModel.GridBlocks.Single(b => b.Title == "Focus"));
        Assert.Equal(840.0, viewModel.NowMinute);
        Assert.Equal("14:00", viewModel.NowLabel);
        Assert.True(focus.IsPast);
        Assert.False(focus.IsCurrent);
        Assert.True(viewModel.GridBlocks.Single(b => b.Title == "Workout").IsCurrent);
        Assert.True(viewModel.GridMarkers.Single().IsPast);
        Assert.Equal("Workout", viewModel.CurrentBlockTitle);
    }

    [Fact]
    public void SwitchingClockFormat_ReformatsBlockAndMarkerLabels()
    {
        var display = new TimeDisplay();
        var viewModel = new ScheduleViewModel(Blocks(Sleep), Markers(Coffee), At(9, 0), display);

        display.Format = ClockFormat.TwelveHour;

        Assert.All(viewModel.GridBlocks, b => Assert.Equal("11:00 PM-6:30 AM", b.TimeLabel));
        Assert.Equal("1:00 PM", viewModel.GridMarkers.Single().TimeLabel);
    }
}
