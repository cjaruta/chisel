using Chisel.App.ViewModels;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class HabitsViewModelTests
{
    // 2000-01-01 is a Saturday.
    private static readonly DateTimeOffset Morning = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2000, 1, 1);
    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly MemoryRepo _repo = new();

    private HabitsViewModel Create(TimeZoneInfo? zone = null)
    {
        _clock.SetLocalTimeZone(zone ?? TimeZoneInfo.Utc);
        return new HabitsViewModel(_repo, _clock);
    }

    private static HabitItem Item(HabitsViewModel vm, string name) => vm.Items.Single(i => i.Name == name);

    [Fact]
    public void CheckingToday_UpdatesTheSummaryAndStreak_NamesItForScreenReaders_AndSaves()
    {
        var vm = Create();
        vm.Add("Read", "📚", CountdownColor.None, HabitDays.Every);
        vm.Add("Walk", null, CountdownColor.Green, HabitDays.Every);
        Assert.Equal("0 of 2 done today", vm.SummaryText);
        Assert.Equal("Read, not done, no streak", Item(vm, "Read").AutomationName);

        vm.Toggle(Item(vm, "Read").Id);

        Assert.Equal("1 of 2 done today", vm.SummaryText);
        Assert.Equal(0.5, vm.Fraction);
        Assert.False(vm.AllDone);
        Assert.Equal("Read, done, 1 day streak", Item(vm, "Read").AutomationName);
        Assert.Equal("1 day streak", Item(vm, "Read").StreakText);
        Assert.Contains(Today, _repo.Data.Habits[0].Done);

        vm.Toggle(Item(vm, "Walk").Id);
        Assert.True(vm.AllDone);
        Assert.Equal("All 2 done today", vm.SummaryText);
    }

    [Fact]
    public void APastDay_CanBeFixed_ButAFutureDayCannot()
    {
        var vm = Create();
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);
        var id = vm.Items[0].Id;
        vm.Toggle(id, Today.AddDays(-2));
        vm.Toggle(id, Today);
        Assert.Equal(1, Item(vm, "Read").Streak.Current);

        Assert.True(vm.Toggle(id, Today.AddDays(-1))); // the missed check-in
        Assert.Equal(new HabitStreak(3, 3), Item(vm, "Read").Streak);
        Assert.Equal("Read, done, 3 day streak", Item(vm, "Read").AutomationName);

        Assert.False(vm.Toggle(id, Today.AddDays(1)));
        Assert.DoesNotContain(Today.AddDays(1), _repo.Data.Habits[0].Done);
    }

    [Fact]
    public void AtLocalMidnight_TheDayRollsOver_FromOneTimer_AndTheStreakHolds()
    {
        var vm = Create(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-5), "test", "test"));
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);
        Assert.Equal(Today, vm.Today); // 04:00 local
        vm.Toggle(vm.Items[0].Id);

        _clock.Advance(TimeSpan.FromHours(19) + TimeSpan.FromMinutes(59)); // 23:59 local
        Assert.Equal(Today, vm.Today);

        _clock.Advance(TimeSpan.FromMinutes(2)); // 00:01 local
        Assert.Equal(Today.AddDays(1), vm.Today);
        Assert.Equal("0 of 1 done today", vm.SummaryText);
        Assert.Equal(1, vm.Items[0].Streak.Current); // not broken until today ends unchecked
        Assert.Equal(HabitDayState.Pending, vm.Items[0].History[^1]);

        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(Today.AddDays(2), vm.Today);
        Assert.Equal(0, vm.Items[0].Streak.Current);
    }

    [Fact]
    public void ADaylightSavingDay_RollsOverAtItsOwnMidnight()
    {
        // Clocks go forward an hour at 02:00 on the last Sunday of March (2000-03-26), so that day is 23 hours long.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone("dst", TimeSpan.FromHours(1), "dst", "std", "dst", [rule]);
        _clock.SetUtcNow(new DateTimeOffset(2000, 3, 25, 23, 30, 0, TimeSpan.Zero)); // 00:30 on the 26th, local
        var vm = Create(zone);
        Assert.Equal(new DateOnly(2000, 3, 26), vm.Today);

        _clock.Advance(TimeSpan.FromHours(22) + TimeSpan.FromMinutes(31)); // 00:01 on the 27th: only 22.5 hours later
        Assert.Equal(new DateOnly(2000, 3, 27), vm.Today);
    }

    [Fact]
    public void WhileIdle_NothingIsRebuilt()
    {
        var vm = Create();
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);
        var rebuilds = vm.Rebuilds;

        _clock.Advance(TimeSpan.FromHours(14));

        Assert.Equal(rebuilds, vm.Rebuilds);
    }

    [Fact]
    public void Habits_CanBeAddedRenamedReorderedAndDeleted()
    {
        var vm = Create();
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);
        vm.Add("Walk", null, CountdownColor.None, HabitDays.Every);
        vm.Add("Stretch", null, CountdownColor.None, HabitDays.Every);
        var walk = Item(vm, "Walk").Id;

        Assert.Null(vm.Rename(walk, "  Run  "));
        vm.Move(walk, -1);
        Assert.Equal(new[] { "Run", "Read", "Stretch" }, vm.Items.Select(i => i.Name));
        vm.Move(walk, -1); // already first
        vm.Delete(Item(vm, "Read").Id);

        Assert.Equal(new[] { "Run", "Stretch" }, vm.Items.Select(i => i.Name));
        Assert.Equal(new[] { "Run", "Stretch" }, new HabitsViewModel(_repo, _clock).Items.Select(i => i.Name));
    }

    [Fact]
    public void TheForm_RefusesBadInput_AndAddsGoodInput()
    {
        var vm = Create();
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);

        vm.OpenAdd();
        vm.FormName = "read";
        Assert.False(vm.Save());
        Assert.Equal("You already have a habit with that name.", vm.FormError);
        Assert.True(vm.IsFormOpen);

        vm.FormName = "Gym";
        vm.FormDays = HabitDays.Monday | HabitDays.Thursday;
        Assert.True(vm.Save());
        Assert.False(vm.IsFormOpen);
        Assert.Equal(HabitDays.Monday | HabitDays.Thursday, Item(vm, "Gym").Habit.Days);

        vm.OpenEdit(Item(vm, "Gym").Id);
        Assert.Equal(("Gym", HabitDays.Monday | HabitDays.Thursday), (vm.FormName, vm.FormDays));
        vm.DeleteEditing();
        Assert.False(vm.IsFormOpen);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void AHabitNotDueToday_IsLeftOutOfTheCount()
    {
        var vm = Create();
        vm.Add("Gym", null, CountdownColor.None, HabitDays.Monday);
        vm.Add("Read", null, CountdownColor.None, HabitDays.Every);

        Assert.Equal("0 of 1 done today", vm.SummaryText);
        Assert.Equal("Gym, not due today, no streak", Item(vm, "Gym").AutomationName);
        Assert.Equal(HabitDayState.Off, Item(vm, "Gym").History[^1]);
    }

    private sealed class MemoryRepo : IHabitRepository
    {
        public HabitData Data { get; private set; } = HabitData.Empty;
        public HabitData Get() => Data;
        public void Save(HabitData data) => Data = data;
    }
}
