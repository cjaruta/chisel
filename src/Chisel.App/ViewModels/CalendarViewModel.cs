using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Chisel.Core.Calendar;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One cell of the month grid.
public sealed record CalendarDay(DateOnly Date, string Label, bool InMonth, bool IsToday, bool IsSelected, bool IsMarked, string AutomationName);

// The calendar widget's state: the month shown, the selected day and today. Month and day names come from the culture. Today moves on
// at midnight from a single timer set for the next midnight (no polling); a day the user did not choose and a month view that was
// showing today follow it. dispatch runs the midnight update on the UI thread (the timer fires on another thread in the app).
public partial class CalendarViewModel : ViewModelBase, IDisposable
{
    // The months the grid can show without running off the ends of DateOnly.
    private static readonly DateOnly FirstMonth = new(1, 2, 1);
    private static readonly DateOnly LastMonth = new(9999, 11, 1);

    private readonly TimeProvider _time;
    private readonly CultureInfo _culture;
    private readonly Action<Action> _dispatch;
    private readonly ITimer _midnight;

    public CalendarViewModel(CalendarSettingsViewModel settings, TimeProvider time, CultureInfo? culture = null, Action<Action>? dispatch = null)
    {
        Settings = settings;
        _time = time;
        _culture = culture ?? CultureInfo.CurrentCulture;
        _dispatch = dispatch ?? (a => a());
        Today = LocalToday();
        Selected = Today;
        Month = FirstOf(Today);
        Settings.PropertyChanged += OnSettingsChanged;
        _midnight = time.CreateTimer(_ => _dispatch(OnMidnight), null, UntilMidnight(), Timeout.InfiniteTimeSpan);
    }

    public CalendarSettingsViewModel Settings { get; }

    public DateOnly Today { get; private set; }

    // The first of the month shown.
    public DateOnly Month { get; private set; }

    public DateOnly Selected { get; private set; }

    public DayOfWeek FirstDay => Settings.FirstDay;

    public bool ShowWeekNumbers => Settings.ShowWeekNumbers;

    public bool IsCurrentMonth => Month == FirstOf(Today);

    public string MonthName => _culture.DateTimeFormat.GetMonthName(Month.Month);

    public string Title => Month.ToString(_culture.DateTimeFormat.YearMonthPattern, _culture);

    public IReadOnlyList<string> WeekdayNames =>
        MonthGrid.WeekdayOrder(FirstDay).Select(d => _culture.DateTimeFormat.GetAbbreviatedDayName(d)).ToList();

    public IReadOnlyList<string> WeekdayFullNames =>
        MonthGrid.WeekdayOrder(FirstDay).Select(d => _culture.DateTimeFormat.GetDayName(d)).ToList();

    public IReadOnlyList<CalendarDay> Days => MonthGrid.Days(Month.Year, Month.Month, FirstDay).Select(Cell).ToList();

    public IReadOnlyList<int> WeekNumbers
    {
        get
        {
            var days = MonthGrid.Days(Month.Year, Month.Month, FirstDay);
            return Enumerable.Range(0, MonthGrid.Rows).Select(r => MonthGrid.WeekNumber(days[r * MonthGrid.Columns], FirstDay)).ToList();
        }
    }

    [RelayCommand]
    private void PreviousMonth() => ShowMonth(Month.AddMonths(-1));

    [RelayCommand]
    private void NextMonth() => ShowMonth(Month.AddMonths(1));

    [RelayCommand]
    private void GoToToday()
    {
        Selected = Today;
        ShowMonth(FirstOf(Today));
    }

    // Puts a dot on the selected day, or takes it off.
    [RelayCommand]
    private void ToggleMark() => Settings.ToggleMark(Selected);

    // Selects a day; one from the month before or after brings that month into view.
    public void Select(DateOnly date)
    {
        Selected = Clamp(date);
        ShowMonth(FirstOf(Selected));
    }

    public void MoveSelection(int days) => Select(Selected.AddDays(Math.Clamp(days, -366, 366)));

    // AddMonths keeps the day, or the month's last when it is shorter.
    public void MoveMonths(int months) => Select(Selected.AddMonths(Math.Clamp(months, -12, 12)));

    private void ShowMonth(DateOnly first)
    {
        Month = first < FirstMonth ? FirstMonth : first > LastMonth ? LastMonth : first;
        Changed();
    }

    private void OnMidnight()
    {
        var was = Today;
        var now = LocalToday();
        if (now != was)
        {
            Today = now;
            if (Selected == was)
            {
                Selected = now;
            }
            if (Month == FirstOf(was))
            {
                Month = FirstOf(now);
            }
            Changed();
        }
        _midnight.Change(UntilMidnight(), Timeout.InfiniteTimeSpan);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => Changed();

    private void Changed()
    {
        foreach (var name in new[]
        {
            nameof(Today), nameof(Month), nameof(Selected), nameof(FirstDay), nameof(ShowWeekNumbers), nameof(IsCurrentMonth),
            nameof(MonthName), nameof(Title), nameof(WeekdayNames), nameof(WeekdayFullNames), nameof(WeekNumbers), nameof(Days),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private CalendarDay Cell(DateOnly date)
    {
        var isToday = date == Today;
        var isSelected = date == Selected;
        var isMarked = Settings.IsMarked(date);
        var name = date.ToString("D", _culture)
            + (isToday ? ", today" : "") + (isSelected ? ", selected" : "") + (isMarked ? ", marked" : "");
        return new CalendarDay(date, date.Day.ToString(_culture), date.Month == Month.Month, isToday, isSelected, isMarked, name);
    }

    private DateOnly LocalToday() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    // Time to the next local midnight, from the clock's own zone (so a daylight-saving change that day is counted).
    private TimeSpan UntilMidnight()
    {
        var now = _time.GetLocalNow();
        var next = now.Date.AddDays(1);
        var at = new DateTimeOffset(next, _time.LocalTimeZone.GetUtcOffset(next));
        var due = at - now;
        return due > TimeSpan.Zero ? due : TimeSpan.FromSeconds(1);
    }

    private static DateOnly FirstOf(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly Clamp(DateOnly date)
    {
        var last = LastMonth.AddMonths(1).AddDays(-1);
        return date < FirstMonth ? FirstMonth : date > last ? last : date;
    }

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        _midnight.Dispose();
    }
}
