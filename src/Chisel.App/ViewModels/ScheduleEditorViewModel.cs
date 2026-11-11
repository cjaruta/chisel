using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class ScheduleEditorViewModel : ViewModelBase
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IMarkerRepository _markerRepository;

    public ObservableCollection<EditableBlockRow> Blocks { get; } = new();
    public ObservableCollection<EditableMarkerRow> Markers { get; } = new();

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public event Action? Saved;

    public ScheduleEditorViewModel(IScheduleRepository scheduleRepository, IMarkerRepository markerRepository)
    {
        _scheduleRepository = scheduleRepository;
        _markerRepository = markerRepository;

        foreach (var block in _scheduleRepository.Get().Blocks)
        {
            Blocks.Add(new EditableBlockRow
            {
                Title = block.Title,
                Start = block.Start.ToString("HH:mm"),
                End = block.End.ToString("HH:mm")
            });
        }

        foreach (var marker in _markerRepository.GetAll())
        {
            Markers.Add(new EditableMarkerRow
            {
                Label = marker.Label,
                Time = marker.Time.ToString("HH:mm"),
                Kind = marker.Kind
            });
        }
    }

    [RelayCommand]
    private void AddBlock() => Blocks.Add(new EditableBlockRow { Title = "New block", Start = "09:00", End = "10:00" });

    [RelayCommand]
    private void RemoveBlock(EditableBlockRow row) => Blocks.Remove(row);

    [RelayCommand]
    private void AddMarker() => Markers.Add(new EditableMarkerRow { Label = "New marker", Time = "12:00" });

    [RelayCommand]
    private void RemoveMarker(EditableMarkerRow row) => Markers.Remove(row);

    [RelayCommand]
    private void Save()
    {
        var parsedBlocks = new List<ScheduleBlock>();
        foreach (var row in Blocks)
        {
            row.Error = null;

            if (!TimeOnly.TryParse(row.Start, out var start) || !TimeOnly.TryParse(row.End, out var end))
            {
                row.Error = "Enter times as HH:mm.";
                continue;
            }

            try
            {
                parsedBlocks.Add(new ScheduleBlock(row.Title, start, end));
            }
            catch (ArgumentException ex)
            {
                row.Error = ex.Message;
            }
        }

        var parsedMarkers = new List<Marker>();
        foreach (var row in Markers)
        {
            row.Error = null;

            if (!TimeOnly.TryParse(row.Time, out var time))
            {
                row.Error = "Enter the time as HH:mm.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Label))
            {
                row.Error = "Label cannot be empty.";
                continue;
            }

            parsedMarkers.Add(new Marker(row.Label, time, row.Kind));
        }

        if (Blocks.Any(b => b.Error is not null) || Markers.Any(m => m.Error is not null))
        {
            ErrorMessage = "Fix the highlighted rows before saving.";
            return;
        }

        ErrorMessage = null;
        _scheduleRepository.Save(new DaySchedule(parsedBlocks));
        _markerRepository.ReplaceAll(parsedMarkers);
        Saved?.Invoke();
    }
}
