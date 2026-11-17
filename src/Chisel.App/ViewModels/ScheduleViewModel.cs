using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The day grid. Refresh reloads blocks and markers from the repositories;
// UpdateNow (every clock tick) only moves the now-line and the current/past flags.
public partial class ScheduleViewModel : ViewModelBase
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IMarkerRepository _markerRepository;
    private readonly TimeProvider _time;
    private readonly TimeDisplay _display;
    private IReadOnlyList<ScheduleBlock> _blocks = Array.Empty<ScheduleBlock>();
    private IReadOnlyList<Marker> _markers = Array.Empty<Marker>();

    [ObservableProperty]
    public partial string CurrentBlockTitle { get; set; } = "No block";

    [ObservableProperty]
    public partial double NowMinute { get; set; }

    [ObservableProperty]
    public partial string NowLabel { get; set; } = "";

    public ObservableCollection<ScheduleGridBlock> GridBlocks { get; } = new();
    public ObservableCollection<ScheduleGridMarker> GridMarkers { get; } = new();
    public ObservableCollection<ScheduleHourLabel> HourLabels { get; } = new();

    public BlockEditorViewModel BlockEditor { get; }
    public MarkerEditorViewModel MarkerEditor { get; }

    public ScheduleViewModel(IScheduleRepository scheduleRepository, IMarkerRepository markerRepository, TimeProvider time, TimeDisplay? display = null)
    {
        _scheduleRepository = scheduleRepository;
        _markerRepository = markerRepository;
        _time = time;
        _display = display ?? new TimeDisplay();
        _display.PropertyChanged += (_, _) => Refresh();
        BlockEditor = new BlockEditorViewModel(scheduleRepository, _display, Refresh);
        MarkerEditor = new MarkerEditorViewModel(markerRepository, _display, Refresh);
        Refresh();
    }

    private TimeOnly Now => TimeOnly.FromDateTime(_time.GetLocalNow().DateTime);

    // New items start at the next full hour.
    private TimeOnly NextHour => new TimeOnly(Now.Hour, 0).AddHours(1);

    [RelayCommand]
    private void AddBlock()
    {
        MarkerEditor.Cancel();
        BlockEditor.OpenNew(NextHour);
    }

    // The hover guide line: where a click on empty grid space would add a block.
    [ObservableProperty]
    public partial bool IsHoverVisible { get; set; }

    [ObservableProperty]
    public partial double HoverMinute { get; set; }

    [ObservableProperty]
    public partial string HoverLabel { get; set; } = "";

    private TimeOnly _hoverTime;

    // overItem: the pointer is over a block or marker, which are clicked to edit instead.
    public void HoverAt(TimeOnly time, bool overItem = false)
    {
        if (overItem)
        {
            HideHover();
            return;
        }
        _hoverTime = time;
        HoverMinute = ScheduleGridLayout.MinuteOfDay(time);
        HoverLabel = _display.Time(time);
        IsHoverVisible = true;
    }

    public void HideHover() => IsHoverVisible = false;

    // A new block from the guide line, an hour long but ending at midnight at the latest.
    [RelayCommand]
    private void AddBlockAtHover()
    {
        if (!IsHoverVisible)
        {
            return;
        }
        var end = _hoverTime.AddHours(1);
        if (end < _hoverTime)
        {
            end = TimeOnly.MinValue;
        }
        MarkerEditor.Cancel();
        BlockEditor.OpenNew(_hoverTime, end);
    }

    [RelayCommand]
    private void AddMarker()
    {
        BlockEditor.Cancel();
        MarkerEditor.OpenNew(NextHour);
    }

    // Clicking a block on the grid. Both pieces of an overnight block carry the same BlockIndex.
    [RelayCommand]
    private void EditBlock(ScheduleGridBlock? item)
    {
        if (item is null || item.BlockIndex >= _blocks.Count)
        {
            return;
        }
        MarkerEditor.Cancel();
        BlockEditor.OpenEdit(item.BlockIndex, _blocks[item.BlockIndex]);
    }

    [RelayCommand]
    private void EditMarker(ScheduleGridMarker? item)
    {
        if (item is null || item.MarkerIndex >= _markers.Count)
        {
            return;
        }
        BlockEditor.Cancel();
        MarkerEditor.OpenEdit(item.MarkerIndex, _markers[item.MarkerIndex]);
    }

    [RelayCommand]
    private void CloseEditors()
    {
        BlockEditor.Cancel();
        MarkerEditor.Cancel();
    }

    public void Refresh()
    {
        _blocks = _scheduleRepository.Get().Blocks;
        _markers = _markerRepository.GetAll();
        HoverLabel = _display.Time(_hoverTime);

        HourLabels.Clear();
        for (var hour = 0; hour <= 24; hour++)
        {
            HourLabels.Add(new ScheduleHourLabel { StartMinute = hour * 60, Text = _display.Time(new TimeOnly(hour % 24, 0)) });
        }

        GridBlocks.Clear();
        foreach (var segment in ScheduleGridLayout.Segments(_blocks))
        {
            var block = _blocks[segment.BlockIndex];
            GridBlocks.Add(new ScheduleGridBlock
            {
                BlockIndex = segment.BlockIndex,
                Title = block.Title,
                TimeLabel = _display.Range(block.Start, block.End),
                StartMinute = segment.StartMinute,
                EndMinute = segment.EndMinute,
                Column = segment.Column,
                ColumnCount = segment.ColumnCount,
            });
        }

        GridMarkers.Clear();
        for (var i = 0; i < _markers.Count; i++)
        {
            GridMarkers.Add(new ScheduleGridMarker
            {
                MarkerIndex = i,
                Label = _markers[i].Label,
                TimeLabel = _display.Time(_markers[i].Time),
                Kind = _markers[i].Kind,
                StartMinute = ScheduleGridLayout.MinuteOfDay(_markers[i].Time),
            });
        }

        UpdateNow();
    }

    public void UpdateNow()
    {
        var now = Now;
        NowMinute = ScheduleGridLayout.MinuteOfDay(now);
        NowLabel = _display.Time(now);
        CurrentBlockTitle = _blocks.FirstOrDefault(b => b.Contains(now))?.Title ?? "No block";

        foreach (var item in GridBlocks)
        {
            item.IsCurrent = _blocks[item.BlockIndex].Contains(now);
            item.IsPast = !item.IsCurrent && NowMinute >= item.EndMinute;
        }

        foreach (var item in GridMarkers)
        {
            item.IsPast = _markers[item.MarkerIndex].HasPassed(now);
        }
    }
}
