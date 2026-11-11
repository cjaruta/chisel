using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class ClockViewModel : ViewModelBase
{
    private readonly TimeProvider _time;

    [ObservableProperty]
    public partial DateTimeOffset Now { get; set; }

    public ClockViewModel(TimeProvider time)
    {
        _time = time;
        Now = _time.GetLocalNow();
    }

    public void Refresh() => Now = _time.GetLocalNow();
}
