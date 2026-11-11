using System;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// Shared by every view model that shows a time. Changing Format makes them all re-render,
// so the clock format applies everywhere instantly. All text goes through TimeFormatter.
public partial class TimeDisplay : ViewModelBase
{
    [ObservableProperty]
    public partial ClockFormat Format { get; set; }

    public string Time(TimeOnly time, bool withSeconds = false) => TimeFormatter.Format(time, Format, withSeconds);

    public string Range(TimeOnly start, TimeOnly end) => TimeFormatter.FormatRange(start, end, Format);
}
