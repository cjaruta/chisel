using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class ScheduleViewModel : ViewModelBase
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IMarkerRepository _markerRepository;
    private readonly TimeProvider _time;

    [ObservableProperty]
    public partial string CurrentBlockTitle { get; set; } = "No block";

    public ObservableCollection<ScheduleTimelineEntry> Timeline { get; } = new();

    public ScheduleViewModel(IScheduleRepository scheduleRepository, IMarkerRepository markerRepository, TimeProvider time)
    {
        _scheduleRepository = scheduleRepository;
        _markerRepository = markerRepository;
        _time = time;
        Refresh();
    }

    public void Refresh()
    {
        var schedule = _scheduleRepository.Get();
        var markers = _markerRepository.GetAll();
        var now = TimeOnly.FromDateTime(_time.GetLocalNow().DateTime);

        CurrentBlockTitle = schedule.CurrentBlock(now)?.Title ?? "No block";

        var entries = schedule.Blocks
            // An overnight block that's still running after midnight started yesterday, so it leads the list.
            .Select(block => (Sort: block.CrossesMidnight && now < block.End ? TimeOnly.MinValue : block.Start, Entry: new ScheduleTimelineEntry
            {
                Title = block.Title,
                TimeLabel = $"{block.Start:HH:mm}-{block.End:HH:mm}",
                IsCurrent = block.Contains(now),
                IsPast = !block.Contains(now) && !block.CrossesMidnight && now >= block.End
            }))
            .Concat(markers.Select(marker => (Sort: marker.Time, Entry: new ScheduleTimelineEntry
            {
                Title = marker.Label,
                TimeLabel = $"{marker.Time:HH:mm}",
                IsCurrent = false,
                IsPast = marker.HasPassed(now)
            })))
            .OrderBy(x => x.Sort)
            .Select(x => x.Entry);

        Timeline.Clear();
        foreach (var entry in entries)
        {
            Timeline.Add(entry);
        }
    }
}
