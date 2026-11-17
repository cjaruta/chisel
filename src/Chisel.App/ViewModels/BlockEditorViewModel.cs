using System;
using System.Linq;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The in-window editor for one schedule block. Times are typed in either clock format.
public partial class BlockEditorViewModel : ViewModelBase
{
    public const string TimeHint = "like 14:30 or 2:30 PM";

    private readonly IScheduleRepository _repository;
    private readonly TimeDisplay _display;
    private readonly Action _saved;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string StartText { get; set; } = "";

    [ObservableProperty]
    public partial string EndText { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public BlockEditorViewModel(IScheduleRepository repository, TimeDisplay display, Action saved)
    {
        _repository = repository;
        _display = display;
        _saved = saved;
    }

    public void OpenNew(TimeOnly start)
    {
        Title = "";
        StartText = _display.Time(start);
        EndText = _display.Time(start.AddHours(1));
        ErrorMessage = null;
        IsOpen = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Enter a title.";
            return;
        }
        if (!TimeFormatter.TryParse(StartText, out var start))
        {
            ErrorMessage = $"Enter the start time {TimeHint}.";
            return;
        }
        if (!TimeFormatter.TryParse(EndText, out var end))
        {
            ErrorMessage = $"Enter the end time {TimeHint}.";
            return;
        }
        if (start == end)
        {
            ErrorMessage = "End must be different from start. Overnight blocks are fine.";
            return;
        }

        var blocks = _repository.Get().Blocks.Append(new ScheduleBlock(Title, start, end));
        _repository.Save(new DaySchedule(blocks));
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
