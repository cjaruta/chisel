using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
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
        BlockEditor.PropertyChanged += OnBlockEditorChanged;
        MarkerEditor.PropertyChanged += OnMarkerEditorChanged;
        Refresh();
    }

    private TimeOnly Now => TimeOnly.FromDateTime(_time.GetLocalNow().DateTime);

    // New items start at the next full hour.
    private TimeOnly NextHour => new TimeOnly(Now.Hour, 0).AddHours(1);

    [RelayCommand]
    private void AddBlock()
    {
        OpenNewBlock(NextHour, NextHour.AddHours(1));
    }

    // The hover guide line: where a click on empty grid space would add a marker.
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
        if (IsDragging)
        {
            return;
        }
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

    // A click on empty grid space: a new marker at that time, shown as a pending line until Save.
    [ObservableProperty]
    public partial bool IsMarkerPending { get; private set; }

    [ObservableProperty]
    public partial double PendingMarkerMinute { get; private set; }

    [ObservableProperty]
    public partial string PendingMarkerTime { get; private set; } = "";

    [ObservableProperty]
    public partial string PendingMarkerLabel { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsPendingMarkerCheckpoint { get; private set; }

    private void OpenPendingMarker(TimeOnly time)
    {
        IsPending = false;
        BlockEditor.Cancel();
        MarkerEditor.OpenNew(time);
        IsMarkerPending = true;
        UpdatePendingMarkerFromEditor();
    }

    // The pending marker follows what is typed in the editor; an unreadable time keeps the last one.
    private void UpdatePendingMarkerFromEditor()
    {
        if (!IsMarkerPending)
        {
            return;
        }
        PendingMarkerLabel = string.IsNullOrWhiteSpace(MarkerEditor.Label) ? "New marker" : MarkerEditor.Label;
        IsPendingMarkerCheckpoint = MarkerEditor.Kind == MarkerKind.Checkpoint;
        if (TimeFormatter.TryParse(MarkerEditor.TimeText, out var time))
        {
            PendingMarkerMinute = ScheduleGridLayout.MinuteOfDay(time);
            PendingMarkerTime = _display.Time(time);
        }
    }

    private void OnMarkerEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkerEditorViewModel.IsOpen) && !MarkerEditor.IsOpen)
        {
            IsMarkerPending = false;
        }
        else if (e.PropertyName is nameof(MarkerEditorViewModel.Label) or nameof(MarkerEditorViewModel.TimeText) or nameof(MarkerEditorViewModel.Kind))
        {
            UpdatePendingMarkerFromEditor();
        }
    }

    private void OpenNewBlock(TimeOnly start, TimeOnly end)
    {
        IsPending = false;
        MarkerEditor.Cancel();
        BlockEditor.OpenNew(start, end);
    }

    // Dragging on empty grid space. y and scrollOffset mean the same as in ScheduleGridLayout.SnapTime;
    // the scroll offset can change mid-drag when the grid auto-scrolls.
    // Moving less than this, or not past the pressed mark, is still a plain click, which adds a marker.
    public const double DragThresholdPixels = 4;

    private double _pressContentY;
    private int _pressMinute;
    private GridRange _dragRange = new(0, ScheduleGridLayout.HoverSnapMinutes);

    // The pointer is down on empty space and being tracked.
    [ObservableProperty]
    public partial bool IsDragActive { get; private set; }

    // The pointer has moved enough to be a drag: the preview shows.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    public partial bool IsDragging { get; private set; }

    // A dragged block waiting in the editor; nothing is saved until Save.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    public partial bool IsPending { get; private set; }

    public bool IsPreviewVisible => IsDragging || IsPending;

    [ObservableProperty]
    public partial double PreviewStartMinute { get; private set; }

    [ObservableProperty]
    public partial double PreviewEndMinute { get; private set; }

    [ObservableProperty]
    public partial string PreviewLabel { get; private set; } = "";

    [ObservableProperty]
    public partial string PreviewTitle { get; private set; } = "";

    public void BeginDrag(double y, double scrollOffset)
    {
        _pressContentY = y + scrollOffset;
        _pressMinute = ScheduleGridLayout.SnapMinute(y, scrollOffset, max: ScheduleGridLayout.MinutesPerDay - ScheduleGridLayout.HoverSnapMinutes);
        _dragRange = new GridRange(_pressMinute, _pressMinute + ScheduleGridLayout.HoverSnapMinutes);
        IsDragging = false;
        IsDragActive = true;
    }

    public void UpdateDrag(double y, double scrollOffset)
    {
        if (!IsDragActive)
        {
            return;
        }

        if (!IsDragging)
        {
            var moved = Math.Abs(y + scrollOffset - _pressContentY) >= DragThresholdPixels;
            var pastMark = ScheduleGridLayout.SnapMinute(y, scrollOffset) != _pressMinute;
            if (!moved || !pastMark)
            {
                return;
            }
            HideHover();
            IsDragging = true;
        }

        _dragRange = ScheduleGridLayout.DragRange(_pressContentY - scrollOffset, y, scrollOffset);
        ShowPreview(_dragRange.StartMinute, _dragRange.EndMinute);
        PreviewTitle = "New block";
    }

    public void EndDrag()
    {
        if (!IsDragActive)
        {
            return;
        }
        IsDragActive = false;

        if (!IsDragging)
        {
            OpenPendingMarker(GridRange.TimeAt(_pressMinute));
            return;
        }

        OpenNewBlock(_dragRange.Start, _dragRange.End);
        IsDragging = false;
        IsPending = true;
        UpdatePendingFromEditor();
    }

    public void CancelDrag()
    {
        IsDragActive = false;
        IsDragging = false;
    }

    private void ShowPreview(int startMinute, int endMinute)
    {
        PreviewStartMinute = startMinute;
        PreviewEndMinute = endMinute;
        PreviewLabel = _display.Range(GridRange.TimeAt(startMinute), GridRange.TimeAt(endMinute));
    }

    // The pending block follows what is typed in the editor. Overnight times show up to midnight.
    private void UpdatePendingFromEditor()
    {
        if (!IsPending)
        {
            return;
        }
        PreviewTitle = string.IsNullOrWhiteSpace(BlockEditor.Title) ? "New block" : BlockEditor.Title;
        if (TimeFormatter.TryParse(BlockEditor.StartText, out var start) && TimeFormatter.TryParse(BlockEditor.EndText, out var end))
        {
            var startMinute = (int)ScheduleGridLayout.MinuteOfDay(start);
            var endMinute = (int)ScheduleGridLayout.MinuteOfDay(end);
            ShowPreview(startMinute, endMinute <= startMinute ? ScheduleGridLayout.MinutesPerDay : endMinute);
        }
    }

    private void OnBlockEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BlockEditorViewModel.IsOpen) && !BlockEditor.IsOpen)
        {
            IsPending = false;
        }
        else if (e.PropertyName is nameof(BlockEditorViewModel.Title) or nameof(BlockEditorViewModel.StartText) or nameof(BlockEditorViewModel.EndText))
        {
            UpdatePendingFromEditor();
        }
    }

    [RelayCommand]
    private void AddMarker()
    {
        IsMarkerPending = false;
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
        IsPending = false;
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
        IsMarkerPending = false;
        BlockEditor.Cancel();
        MarkerEditor.OpenEdit(item.MarkerIndex, _markers[item.MarkerIndex]);
    }

    [RelayCommand]
    private void CloseEditors()
    {
        // Escape during a drag only cancels the drag.
        if (IsDragActive)
        {
            CancelDrag();
            return;
        }
        BlockEditor.Cancel();
        MarkerEditor.Cancel();
    }

    public void Refresh()
    {
        _blocks = _scheduleRepository.Get().Blocks;
        _markers = _markerRepository.GetAll();
        HoverLabel = _display.Time(_hoverTime);
        if (IsPreviewVisible)
        {
            ShowPreview((int)PreviewStartMinute, (int)PreviewEndMinute);
        }
        UpdatePendingMarkerFromEditor();

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
