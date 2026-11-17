using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The in-window editor for one marker: a label at a time, either a Reminder or a Checkpoint.
public partial class MarkerEditorViewModel : ViewModelBase
{
    private readonly IMarkerRepository _repository;
    private readonly TimeDisplay _display;
    private readonly Action _saved;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    [ObservableProperty]
    public partial MarkerKind Kind { get; set; } = MarkerKind.Reminder;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public IReadOnlyList<MarkerKind> Kinds => MarkerKindOptions.All;

    public MarkerEditorViewModel(IMarkerRepository repository, TimeDisplay display, Action saved)
    {
        _repository = repository;
        _display = display;
        _saved = saved;
    }

    // Position of the marker being edited in the saved list; null when adding a new one.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(Heading))]
    public partial int? EditingIndex { get; set; }

    public bool IsEditing => EditingIndex is not null;
    public string Heading => IsEditing ? "Edit marker" : "New marker";

    public void OpenNew(TimeOnly time) => Open(null, "", time, MarkerKind.Reminder);

    public void OpenEdit(int index, Marker marker) => Open(index, marker.Label, marker.Time, marker.Kind);

    private void Open(int? index, string label, TimeOnly time, MarkerKind kind)
    {
        EditingIndex = index;
        Label = label;
        TimeText = _display.Time(time);
        Kind = kind;
        ErrorMessage = null;
        IsOpen = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            ErrorMessage = "Enter a label.";
            return;
        }
        if (!TimeFormatter.TryParse(TimeText, out var time))
        {
            ErrorMessage = $"Enter the time {BlockEditorViewModel.TimeHint}.";
            return;
        }

        var markers = _repository.GetAll().ToList();
        var marker = new Marker(Label.Trim(), time, Kind);
        if (EditingIndex is { } index && index < markers.Count)
        {
            markers[index] = marker;
        }
        else
        {
            markers.Add(marker);
        }
        Commit(markers);
    }

    [RelayCommand]
    private void Delete()
    {
        var markers = _repository.GetAll().ToList();
        if (EditingIndex is not { } index || index >= markers.Count)
        {
            return;
        }

        markers.RemoveAt(index);
        Commit(markers);
    }

    private void Commit(List<Marker> markers)
    {
        _repository.ReplaceAll(markers);
        ErrorMessage = null;
        IsOpen = false;
        _saved();
    }

    [RelayCommand]
    public void Cancel()
    {
        ErrorMessage = null;
        IsOpen = false;
    }
}
