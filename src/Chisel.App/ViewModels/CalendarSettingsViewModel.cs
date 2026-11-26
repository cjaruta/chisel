using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Chisel.Core.Calendar;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The Calendar page in the Widgets settings: the first day of the week (the system culture's until the user picks Sunday or Monday)
// and whether week numbers are shown. Also holds the marked days, which are set on the widget itself. Each change is saved at once.
public partial class CalendarSettingsViewModel : ViewModelBase
{
    private readonly ICalendarSettingsRepository? _repository;
    private readonly CultureInfo _culture;
    private readonly HashSet<DateOnly> _marked;
    private DayOfWeek? _chosenFirstDay;
    private readonly bool _loading = true;

    [ObservableProperty]
    public partial bool ShowWeekNumbers { get; set; }

    public CalendarSettingsViewModel(ICalendarSettingsRepository? repository, CultureInfo? culture = null)
    {
        _repository = repository;
        _culture = culture ?? CultureInfo.CurrentCulture;
        var saved = repository?.Get() ?? CalendarSettings.Default;
        _chosenFirstDay = saved.FirstDay;
        ShowWeekNumbers = saved.ShowWeekNumbers;
        _marked = new HashSet<DateOnly>(saved.MarkedDays);
        _loading = false;
    }

    public DayOfWeek FirstDay => _chosenFirstDay ?? _culture.DateTimeFormat.FirstDayOfWeek;

    public bool IsSunday
    {
        get => FirstDay == DayOfWeek.Sunday;
        set
        {
            if (value)
            {
                ChooseFirstDay(DayOfWeek.Sunday);
            }
        }
    }

    public bool IsMonday
    {
        get => FirstDay == DayOfWeek.Monday;
        set
        {
            if (value)
            {
                ChooseFirstDay(DayOfWeek.Monday);
            }
        }
    }

    public IReadOnlyCollection<DateOnly> MarkedDays => _marked;

    public bool IsMarked(DateOnly date) => _marked.Contains(date);

    public void ToggleMark(DateOnly date)
    {
        if (!_marked.Remove(date))
        {
            _marked.Add(date);
        }
        OnPropertyChanged(nameof(MarkedDays));
        Save();
    }

    private void ChooseFirstDay(DayOfWeek day)
    {
        if (_chosenFirstDay == day)
        {
            return;
        }
        _chosenFirstDay = day;
        OnPropertyChanged(nameof(FirstDay));
        OnPropertyChanged(nameof(IsSunday));
        OnPropertyChanged(nameof(IsMonday));
        Save();
    }

    partial void OnShowWeekNumbersChanged(bool value) => Save();

    private void Save()
    {
        if (!_loading)
        {
            _repository?.Save(new CalendarSettings(_chosenFirstDay, ShowWeekNumbers, _marked.Order().ToList()));
        }
    }
}
