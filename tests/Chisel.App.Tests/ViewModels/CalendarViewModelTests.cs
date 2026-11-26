using System.Globalization;
using Chisel.App.ViewModels;
using Chisel.Core.Calendar;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class CalendarViewModelTests
{
    private sealed class FakeRepository(CalendarSettings? initial = null) : ICalendarSettingsRepository
    {
        public CalendarSettings Saved { get; private set; } = initial ?? CalendarSettings.Default;
        public CalendarSettings Get() => Saved;
        public void Save(CalendarSettings settings) => Saved = settings;
    }

    // Wednesday 2026-11-25 at noon, UTC.
    private static readonly DateTimeOffset Noon = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 25);

    private static CalendarViewModel Create(FakeTimeProvider? time = null, CalendarSettings? settings = null, string culture = "en-US",
        FakeRepository? repository = null) =>
        new(new CalendarSettingsViewModel(repository ?? new FakeRepository(settings), CultureInfo.GetCultureInfo(culture)),
            time ?? new FakeTimeProvider(Noon), CultureInfo.GetCultureInfo(culture));

    [Fact]
    public void Opens_OnTodaysMonth_WithTodaySelectedAndHighlighted()
    {
        using var calendar = Create();

        Assert.Equal(new DateOnly(2026, 11, 1), calendar.Month);
        Assert.Equal(Today, calendar.Selected);
        Assert.Equal("November 2026", calendar.Title);
        Assert.Equal("November", calendar.MonthName);
        Assert.Equal(42, calendar.Days.Count);
        var today = Assert.Single(calendar.Days, d => d.IsToday);
        Assert.Equal(Today, today.Date);
        Assert.True(today.IsSelected);
        Assert.All(calendar.Days, d => Assert.Equal(d.Date.Month == 11, d.InMonth));
    }

    [Fact]
    public void FirstDay_DefaultsFromTheCulture_AndNamesFollowIt()
    {
        using var us = Create(culture: "en-US");
        using var german = Create(culture: "de-DE");

        Assert.Equal(DayOfWeek.Sunday, us.FirstDay);
        Assert.Equal("Sun", us.WeekdayNames[0]);
        Assert.Equal(DayOfWeek.Monday, german.FirstDay);
        Assert.Equal("Mo", german.WeekdayNames[0]);
        Assert.Equal("November 2026", german.Title);
        Assert.Equal(DayOfWeek.Monday, german.Days[0].Date.DayOfWeek);
    }

    [Fact]
    public void ChangingTheFirstDay_RebuildsTheGrid_AndIsSaved()
    {
        var repository = new FakeRepository();
        using var calendar = Create(repository: repository);

        calendar.Settings.IsMonday = true;

        Assert.Equal(DayOfWeek.Monday, calendar.Days[0].Date.DayOfWeek);
        Assert.Equal("Mon", calendar.WeekdayNames[0]);
        Assert.Equal(DayOfWeek.Monday, repository.Saved.FirstDay);
    }

    [Fact]
    public void PreviousAndNext_ChangeTheMonth_AcrossYears_AndTodayComesBack()
    {
        using var calendar = Create();

        calendar.NextMonthCommand.Execute(null);
        calendar.NextMonthCommand.Execute(null);
        Assert.Equal(new DateOnly(2027, 1, 1), calendar.Month);
        Assert.False(calendar.IsCurrentMonth);
        Assert.DoesNotContain(calendar.Days, d => d.IsToday);

        calendar.PreviousMonthCommand.Execute(null);
        calendar.PreviousMonthCommand.Execute(null);
        calendar.PreviousMonthCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 10, 1), calendar.Month);

        calendar.Select(new DateOnly(2026, 10, 3));
        calendar.GoToTodayCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.Month);
        Assert.Equal(Today, calendar.Selected);
        Assert.True(calendar.IsCurrentMonth);
    }

    [Fact]
    public void SelectingADayOfAnAdjacentMonth_MovesToThatMonth()
    {
        using var calendar = Create();

        calendar.Select(new DateOnly(2026, 12, 2));

        Assert.Equal(new DateOnly(2026, 12, 2), calendar.Selected);
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.Month);
        Assert.Single(calendar.Days, d => d.IsSelected);
    }

    [Theory]
    [InlineData(1, 0, 2026, 11, 26)]
    [InlineData(-1, 0, 2026, 11, 24)]
    [InlineData(7, 0, 2026, 12, 2)]
    [InlineData(-7, 0, 2026, 11, 18)]
    [InlineData(0, 1, 2026, 12, 25)]
    [InlineData(0, -1, 2026, 10, 25)]
    public void Keyboard_MovesTheSelectionByDayWeekOrMonth_AndTheMonthFollows(int days, int months, int year, int month, int day)
    {
        using var calendar = Create();

        if (days != 0)
        {
            calendar.MoveSelection(days);
        }
        else
        {
            calendar.MoveMonths(months);
        }

        Assert.Equal(new DateOnly(year, month, day), calendar.Selected);
        Assert.Equal(new DateOnly(year, month, 1), calendar.Month);
    }

    [Fact]
    public void MovingByAMonth_KeepsTheDay_OrTheMonthsLast()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2024, 1, 31, 12, 0, 0, TimeSpan.Zero));
        using var calendar = Create(time);

        calendar.MoveMonths(1);

        Assert.Equal(new DateOnly(2024, 2, 29), calendar.Selected);
    }

    [Fact]
    public void AtMidnight_TheTodayHighlightMovesOn_WithoutPolling()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 30, 23, 59, 0, TimeSpan.Zero));
        using var calendar = Create(time);
        var changes = 0;
        calendar.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(CalendarViewModel.Days) ? 1 : 0;

        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(0, changes);
        Assert.Equal(new DateOnly(2026, 11, 30), calendar.Today);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.Today);
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.Month);
        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(calendar.Days, d => d.IsToday).Date);
        Assert.Equal(1, changes);

        // The next midnight is set up too.
        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(new DateOnly(2026, 12, 2), calendar.Today);
    }

    [Fact]
    public void AtMidnight_ADayTheUserChoseStaysSelected_AndAnotherMonthStaysShown()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero));
        using var calendar = Create(time);
        calendar.Select(new DateOnly(2027, 3, 4));

        time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(new DateOnly(2027, 3, 4), calendar.Selected);
        Assert.Equal(new DateOnly(2027, 3, 1), calendar.Month);
    }

    [Fact]
    public void WeekNumbers_FollowTheRows()
    {
        using var calendar = Create(settings: new CalendarSettings(DayOfWeek.Monday, ShowWeekNumbers: true));

        Assert.True(calendar.ShowWeekNumbers);
        Assert.Equal(new[] { 44, 45, 46, 47, 48, 49 }, calendar.WeekNumbers);
    }

    [Fact]
    public void ToggleMark_PutsADotOnTheSelectedDay_AndIsSaved()
    {
        var repository = new FakeRepository();
        using var calendar = Create(repository: repository);

        calendar.ToggleMarkCommand.Execute(null);

        Assert.True(calendar.Days.Single(d => d.Date == Today).IsMarked);
        Assert.Equal(new[] { Today }, repository.Saved.MarkedDays);

        calendar.ToggleMarkCommand.Execute(null);
        Assert.False(calendar.Days.Single(d => d.Date == Today).IsMarked);
        Assert.Empty(repository.Saved.MarkedDays);
    }

    [Fact]
    public void DayCells_HaveAutomationNames()
    {
        using var calendar = Create(settings: new CalendarSettings(MarkedDays: [Today]));

        Assert.Equal("Wednesday, November 25, 2026, today, selected, marked", calendar.Days.Single(d => d.Date == Today).AutomationName);
        Assert.Equal("Thursday, November 26, 2026", calendar.Days.Single(d => d.Date == Today.AddDays(1)).AutomationName);
    }
}
