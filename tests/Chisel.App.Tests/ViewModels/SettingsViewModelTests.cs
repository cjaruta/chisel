using Chisel.App.ViewModels;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    private sealed class FakeSettingsRepository : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; }
        public FakeSettingsRepository(UserSettings? initial) => Saved = initial;
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings) => Saved = settings;
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Get() => new(new[] { new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30)) });
        public void Save(DaySchedule schedule) { }
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> GetAll() => new[] { new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Checkpoint) };
        public void ReplaceAll(IEnumerable<Marker> markers) { }
    }

    private sealed class FakeNoteRepository : INoteRepository
    {
        private readonly List<Note> _items = new();
        public IReadOnlyList<Note> GetAll() => _items;
        public void Add(Note note) => _items.Add(note);
        public void Update(Note note) { }
        public void Remove(Guid id) { }
    }

    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    [Fact]
    public void Constructor_LoadsTheSavedFormatIntoTheSharedDisplay()
    {
        var display = new TimeDisplay();

        var viewModel = new SettingsViewModel(new FakeSettingsRepository(Defaults.WithClockFormat(ClockFormat.TwelveHour)), display);

        Assert.True(viewModel.UseTwelveHour);
        Assert.False(viewModel.UseTwentyFourHour);
        Assert.Equal(ClockFormat.TwelveHour, display.Format);
    }

    [Fact]
    public void Constructor_WithNoSavedSettings_DefaultsToTwentyFourHour()
    {
        var display = new TimeDisplay();

        var viewModel = new SettingsViewModel(new FakeSettingsRepository(null), display);

        Assert.True(viewModel.UseTwentyFourHour);
        Assert.Equal(ClockFormat.TwentyFourHour, display.Format);
    }

    [Fact]
    public void SwitchingToTwelveHour_ReformatsClockScheduleAndNotesAndSaves()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 22, 36, 12, TimeSpan.Zero));
        var display = new TimeDisplay();
        var repository = new FakeSettingsRepository(Defaults);
        var settings = new SettingsViewModel(repository, display);
        var clock = new ClockViewModel(time, display);
        var schedule = new ScheduleViewModel(new FakeScheduleRepository(), new FakeMarkerRepository(), time, display);
        var noteRepository = new FakeNoteRepository();
        noteRepository.Add(new Note(time, "Ideas", "Body"));
        var notes = new NotesViewModel(noteRepository, time, display);

        Assert.Equal("22:36:12", clock.TimeText);
        Assert.Equal(new[] { "13:00", "23:00-06:30" }, schedule.Timeline.Select(e => e.TimeLabel));
        Assert.Equal("Updated Jan 1, 22:36", notes.Notes.Single().UpdatedLabel);

        settings.UseTwelveHour = true;

        Assert.Equal("10:36:12 PM", clock.TimeText);
        Assert.Equal(new[] { "1:00 PM", "11:00 PM-6:30 AM" }, schedule.Timeline.Select(e => e.TimeLabel));
        Assert.Equal("Updated Jan 1, 10:36 PM", notes.Notes.Single().UpdatedLabel);
        Assert.Equal(ClockFormat.TwelveHour, repository.Saved!.ClockFormat);
        Assert.False(settings.UseTwentyFourHour);
    }

    [Fact]
    public void UseTwentyFourHour_SetTrue_SwitchesBack()
    {
        var display = new TimeDisplay();
        var settings = new SettingsViewModel(new FakeSettingsRepository(Defaults.WithClockFormat(ClockFormat.TwelveHour)), display);

        settings.UseTwentyFourHour = true;

        Assert.False(settings.UseTwelveHour);
        Assert.Equal(ClockFormat.TwentyFourHour, display.Format);
    }

    [Fact]
    public void ToggleAndClose_OpenAndCloseThePanel()
    {
        var settings = new SettingsViewModel(new FakeSettingsRepository(Defaults), new TimeDisplay());

        settings.ToggleCommand.Execute(null);
        Assert.True(settings.IsOpen);
        settings.ToggleCommand.Execute(null);
        Assert.False(settings.IsOpen);

        settings.ToggleCommand.Execute(null);
        settings.CloseCommand.Execute(null);
        Assert.False(settings.IsOpen);
    }
}
