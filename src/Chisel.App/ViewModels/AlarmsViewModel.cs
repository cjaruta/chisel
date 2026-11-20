using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class DayToggle : ObservableObject
{
    public AlarmDays Day { get; }
    public string Title { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public DayToggle(AlarmDays day, string title)
    {
        Day = day;
        Title = title;
    }
}

public partial class AlarmItemViewModel : ObservableObject
{
    private readonly AlarmsViewModel _owner;

    public Guid Id { get; }
    public string TimeText { get; }
    public string Label { get; }
    public string DaysText { get; }

    // Setting it switches the alarm on or off (and saves).
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
            {
                _owner.SetEnabled(Id, value);
            }
        }
    }
    private bool _enabled;

    public AlarmItemViewModel(AlarmsViewModel owner, Alarm alarm, string timeText)
    {
        _owner = owner;
        Id = alarm.Id;
        TimeText = timeText;
        Label = alarm.Label;
        DaysText = AlarmFormat.Days(alarm.Days);
        _enabled = alarm.Enabled;
    }

    [RelayCommand]
    private void Delete() => _owner.Delete(Id);
}

// The Alarm tab of the clock panel: the list, a small form to add one, and the check that makes them ring (Tick, about every
// 200 ms, from the main view model). A ringing alarm goes to the alerts banner.
public partial class AlarmsViewModel : ViewModelBase
{
    private readonly AlarmService _service;
    private readonly AlertsViewModel _alerts;
    private readonly TimeDisplay _display;

    public ObservableCollection<AlarmItemViewModel> Items { get; } = [];

    public DayToggle[] Days { get; } =
    [
        new(AlarmDays.Monday, "Mon"), new(AlarmDays.Tuesday, "Tue"), new(AlarmDays.Wednesday, "Wed"), new(AlarmDays.Thursday, "Thu"),
        new(AlarmDays.Friday, "Fri"), new(AlarmDays.Saturday, "Sat"), new(AlarmDays.Sunday, "Sun"),
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewTime { get; set; } = "07:00";

    [ObservableProperty]
    public partial string NewLabel { get; set; } = "";

    public AlarmsViewModel(AlarmService service, AlertsViewModel alerts, TimeDisplay? display = null)
    {
        _service = service;
        _alerts = alerts;
        _display = display ?? new TimeDisplay();
        _service.Fired += OnFired;
        Rebuild();
    }

    public void Tick() => _service.Check();

    private bool CanAdd() => TimeOnly.TryParse(NewTime, CultureInfo.InvariantCulture, out _);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (!TimeOnly.TryParse(NewTime, CultureInfo.InvariantCulture, out var time))
        {
            return;
        }
        var days = Days.Where(d => d.IsOn).Aggregate(AlarmDays.None, (all, d) => all | d.Day);
        _service.Add(time, NewLabel, days);
        NewLabel = "";
        foreach (var day in Days)
        {
            day.IsOn = false;
        }
        Rebuild();
    }

    internal void SetEnabled(Guid id, bool enabled) => _service.SetEnabled(id, enabled);

    internal void Delete(Guid id)
    {
        _service.Delete(id);
        Rebuild();
    }

    private void OnFired(Alarm alarm)
    {
        _alerts.Show(string.IsNullOrWhiteSpace(alarm.Label) ? "Alarm" : alarm.Label, _display.Time(alarm.Time));
        Rebuild();
    }

    private void Rebuild()
    {
        Items.Clear();
        foreach (var alarm in _service.Alarms)
        {
            Items.Add(new AlarmItemViewModel(this, alarm, _display.Time(alarm.Time)));
        }
    }
}
