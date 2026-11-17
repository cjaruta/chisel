using System;
using System.Collections.Generic;
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

    // Position of the block being edited in the saved schedule; null when adding a new one.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(Heading))]
    public partial int? EditingIndex { get; set; }

    public bool IsEditing => EditingIndex is not null;
    public string Heading => IsEditing ? "Edit block" : "New block";

    public void OpenNew(TimeOnly start) => Open(null, "", start, start.AddHours(1));

    public void OpenEdit(int index, ScheduleBlock block) => Open(index, block.Title, block.Start, block.End);

    private void Open(int? index, string title, TimeOnly start, TimeOnly end)
    {
        EditingIndex = index;
        Title = title;
        StartText = _display.Time(start);
        EndText = _display.Time(end);
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

        var blocks = _repository.Get().Blocks.ToList();
        var block = new ScheduleBlock(Title, start, end);
        if (EditingIndex is { } index && index < blocks.Count)
        {
            blocks[index] = block;
        }
        else
        {
            blocks.Add(block);
        }
        Commit(blocks);
    }

    [RelayCommand]
    private void Delete()
    {
        var blocks = _repository.Get().Blocks.ToList();
        if (EditingIndex is not { } index || index >= blocks.Count)
        {
            return;
        }

        blocks.RemoveAt(index);
        Commit(blocks);
    }

    private void Commit(IEnumerable<ScheduleBlock> blocks)
    {
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
