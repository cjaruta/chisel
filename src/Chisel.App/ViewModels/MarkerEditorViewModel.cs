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

    public void OpenNew(TimeOnly time)
    {
        Label = "";
        TimeText = _display.Time(time);
        Kind = MarkerKind.Reminder;
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

        var markers = _repository.GetAll().Append(new Marker(Label.Trim(), time, Kind));
        _repository.ReplaceAll(markers.ToList());
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
