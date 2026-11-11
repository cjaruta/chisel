using System;
using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class ScheduleViewModel : ViewModelBase
{
    private readonly IScheduleRepository _repository;
    private readonly TimeProvider _time;

    [ObservableProperty]
    public partial string CurrentBlockTitle { get; set; } = "No block";

    public ScheduleViewModel(IScheduleRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
        Refresh();
    }

    public void Refresh()
    {
        var schedule = _repository.Get();
        var now = TimeOnly.FromDateTime(_time.GetLocalNow().DateTime);
        CurrentBlockTitle = schedule.CurrentBlock(now)?.Title ?? "No block";
    }
}
