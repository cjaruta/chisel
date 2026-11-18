using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class ClockViewModel : ViewModelBase
{
    private readonly TimeProvider _time;

    private readonly TimeDisplay _display;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText))]
    public partial DateTimeOffset Now { get; set; }

    // True once the local whole second has moved on since the last Refresh.
    public bool NeedsRefresh => _time.GetLocalNow().DateTime.Ticks / TimeSpan.TicksPerSecond != Now.DateTime.Ticks / TimeSpan.TicksPerSecond;

    public string TimeText => _display.Time(TimeOnly.FromDateTime(Now.DateTime), withSeconds: true);

    public ClockViewModel(TimeProvider time, TimeDisplay? display = null)
    {
        _time = time;
        _display = display ?? new TimeDisplay();
        _display.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TimeText));
        Now = _time.GetLocalNow();
    }

    public void Refresh() => Now = _time.GetLocalNow();
}
