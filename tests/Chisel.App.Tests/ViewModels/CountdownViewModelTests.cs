using System.Globalization;
using Chisel.App.ViewModels;
using Chisel.Core.Countdown;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class CountdownViewModelTests
{
    private sealed class FakeRepository(CountdownSettings? initial = null) : ICountdownRepository
    {
        public CountdownSettings? Saved { get; private set; } = initial;
        public int Saves { get; private set; }

        public CountdownSettings Get() => Saved ?? CountdownSettings.Default;

        public void Save(CountdownSettings settings)
        {
            Saved = settings;
            Saves++;
        }
    }

    // Saturday 2000-01-01 at noon, UTC.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly AlertsViewModel _alerts = new();

    private CountdownViewModel Create(FakeRepository repository) =>
        new(repository, _time, _alerts, culture: CultureInfo.GetCultureInfo("en-US"));

    private static CountdownEntry Entry(string name, DateOnly date, TimeOnly? time = null, CountdownRepeat repeat = CountdownRepeat.None) =>
        new(Guid.NewGuid(), name, date, time, repeat);

    private static void Fill(CountdownViewModel vm, string name, DateOnly date, string time = "", bool yearly = false)
    {
        vm.OpenAdd();
        vm.FormName = name;
        vm.FormDate = date.ToDateTime(TimeOnly.MinValue);
        vm.FormTime = time;
        vm.FormYearly = yearly;
    }

    [Fact]
    public void Add_SavesTheEvent_AndShowsTheNearestOneFirst()
    {
        var repository = new FakeRepository();
        using var vm = Create(repository);

        Fill(vm, "Trip", new DateOnly(2000, 1, 13));
        Assert.True(vm.Save());
        Fill(vm, "Party", new DateOnly(2000, 1, 3), "18:00");
        Assert.True(vm.Save());

        Assert.Equal("Party", vm.Nearest!.Entry.Name);
        Assert.Equal("2 days 6 hours", vm.Nearest.Status.Text);
        Assert.Equal(new[] { "Trip" }, vm.Others.Select(i => i.Entry.Name));
        Assert.Equal("Trip, 12 days remaining", vm.Others[0].AutomationName);
        Assert.Equal("Thu, Jan 13, 2000", vm.Others[0].DateText);
        Assert.Equal(2, repository.Saved!.Events.Count);
        Assert.Equal(_time.GetUtcNow(), repository.Saved.Events[0].Created);
        Assert.False(vm.IsFormOpen);
    }

    [Fact]
    public void Add_WithBadInput_ShowsTheErrorAndKeepsTheFormOpen()
    {
        var repository = new FakeRepository();
        using var vm = Create(repository);

        Fill(vm, " ", new DateOnly(2000, 1, 13));
        Assert.False(vm.Save());
        Assert.Equal("Give the event a name.", vm.FormError);
        Assert.True(vm.IsFormOpen);
        Assert.Null(repository.Saved);

        vm.FormName = "Trip";
        vm.FormTime = "later";
        Assert.False(vm.Save());
        Assert.Equal("Enter the time like 14:30, or leave it empty.", vm.FormError);
    }

    [Fact]
    public void Add_StopsAtTheLimit()
    {
        var events = Enumerable.Range(0, CountdownValidation.MaxEvents).Select(i => Entry($"E{i}", new DateOnly(2000, 2, 1))).ToList();
        using var vm = Create(new FakeRepository(new CountdownSettings(events)));

        Assert.False(vm.CanAdd);
        Fill(vm, "One more", new DateOnly(2000, 2, 2));
        Assert.False(vm.Save());
        Assert.Equal($"You can track up to {CountdownValidation.MaxEvents} events.", vm.FormError);
    }

    [Fact]
    public void Edit_ChangesTheEvent_AndDeleteRemovesIt()
    {
        var trip = Entry("Trip", new DateOnly(2000, 1, 13));
        var repository = new FakeRepository(new CountdownSettings([trip]));
        using var vm = Create(repository);

        vm.OpenEdit(trip.Id);
        Assert.Equal("Trip", vm.FormName);
        vm.FormName = "Holiday";
        vm.FormColor = CountdownColor.Green;
        vm.FormEmoji = "🌴";
        Assert.True(vm.Save());
        var saved = Assert.Single(repository.Saved!.Events);
        Assert.Equal(("Holiday", CountdownColor.Green, "🌴", trip.Id), (saved.Name, saved.Color, saved.Emoji, saved.Id));

        vm.OpenEdit(trip.Id);
        vm.DeleteEditing();
        Assert.Empty(repository.Saved!.Events);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void Reorder_InYourOwnOrder_IsSaved()
    {
        var a = Entry("A", new DateOnly(2000, 1, 5));
        var b = Entry("B", new DateOnly(2000, 1, 3));
        var c = Entry("C", new DateOnly(2000, 1, 9));
        var repository = new FakeRepository(new CountdownSettings([a, b, c], SortByDate: false));
        using var vm = Create(repository);

        Assert.Equal(new[] { "A", "B", "C" }, vm.Items.Select(i => i.Entry.Name));
        vm.Move(c.Id, -1);
        Assert.Equal(new[] { "A", "C", "B" }, vm.Items.Select(i => i.Entry.Name));
        Assert.Equal(new[] { "A", "C", "B" }, repository.Saved!.Events.Select(e => e.Name));

        vm.SortByDate = true;
        Assert.Equal(new[] { "B", "A", "C" }, vm.Items.Select(i => i.Entry.Name));
    }

    [Fact]
    public void Midnight_UpdatesTheDays_RollsYearlyEvents_AndAppliesThePastSetting()
    {
        var repository = new FakeRepository(new CountdownSettings(
        [
            Entry("Soon", new DateOnly(2000, 1, 3)),
            Entry("Today", new DateOnly(2000, 1, 1)),
            Entry("Birthday", new DateOnly(1990, 1, 1), repeat: CountdownRepeat.Yearly),
        ], PastEventPolicy.Archive, Notify: false));
        using var vm = Create(repository);
        Assert.Equal("2 days", vm.Items.Single(i => i.Entry.Name == "Soon").Status.Text);

        _time.Advance(TimeSpan.FromHours(12));

        Assert.Equal("Tomorrow", vm.Items.Single(i => i.Entry.Name == "Soon").Status.Text);
        Assert.Equal("365 days", vm.Items.Single(i => i.Entry.Name == "Birthday").Status.Text);
        Assert.DoesNotContain(vm.Items, i => i.Entry.Name == "Today");
        Assert.Equal(1, vm.ArchivedCount);
        Assert.True(repository.Saved!.Events.Single(e => e.Name == "Today").Archived);

        vm.DeleteArchived();
        Assert.Equal(0, vm.ArchivedCount);
        Assert.Equal(2, repository.Saved!.Events.Count);
    }

    [Fact]
    public void PastEvents_CanShowHowLongAgo_OrBeDeleted()
    {
        var repository = new FakeRepository(new CountdownSettings([Entry("Done", new DateOnly(1999, 12, 29))]));
        using var vm = Create(repository);
        Assert.Equal("3 days ago", vm.Items.Single().Status.Text);

        vm.IsDelete = true;

        Assert.True(vm.IsEmpty);
        Assert.Empty(repository.Saved!.Events);
        Assert.Equal(PastEventPolicy.Delete, repository.Saved.PastEvents);
    }

    // Days-only events need no timer until midnight; hours and minutes tick once a minute, but only while a widget shows them.
    [Fact]
    public void Updates_EveryMinute_OnlyWhileHoursOrMinutesAreOnScreen()
    {
        using var vm = Create(new FakeRepository(new CountdownSettings([Entry("Call", new DateOnly(2000, 1, 1), new TimeOnly(15, 0))])));
        Assert.Equal("3 hours", vm.Nearest!.Status.Text);

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("3 hours", vm.Nearest!.Status.Text); // nobody is watching

        vm.Attach(this);
        Assert.Equal("2 hours 55 minutes", vm.Nearest!.Status.Text);
        var updates = vm.Updates;
        for (var i = 0; i < 3; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
        }
        Assert.Equal(updates + 3, vm.Updates);
        Assert.Equal("2 hours 52 minutes", vm.Nearest!.Status.Text);

        vm.Detach(this);
        _time.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal(updates + 3, vm.Updates);
    }

    [Fact]
    public void NoEvents_NoTimer()
    {
        using var vm = Create(new FakeRepository());
        vm.Attach(this);
        var updates = vm.Updates;

        _time.Advance(TimeSpan.FromDays(3));

        Assert.Equal(updates, vm.Updates);
    }

    [Fact]
    public void TheDayAnEventArrives_ShowsOneGentleAlert_OnlyOnce()
    {
        var repository = new FakeRepository(new CountdownSettings(
            [Entry("Trip", new DateOnly(2000, 1, 2)), Entry("Party", new DateOnly(2000, 1, 2))]));
        using (var vm = Create(repository))
        {
            Assert.Null(_alerts.Current);
            _time.Advance(TimeSpan.FromHours(12));

            Assert.Equal(new Alert("Countdown", "Today: Trip, Party"), _alerts.Current);
            Assert.All(vm.Items, i => Assert.True(i.IsToday));
        }

        _alerts.DismissCommand.Execute(null);
        using var reopened = Create(repository);
        Assert.Null(_alerts.Current);
    }
}
