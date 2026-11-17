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
    private readonly PaletteDisplay _palette;

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

    // The chosen BlockColor key; the matching swatch shows a ring.
    [ObservableProperty]
    public partial string ColorKey { get; set; } = BlockColor.Default;

    // One per color key, drawn in the current palette.
    public IReadOnlyList<ColorSwatchOption> Swatches { get; }

    partial void OnColorKeyChanged(string value)
    {
        foreach (var swatch in Swatches)
        {
            swatch.IsSelected = swatch.Key == value;
        }
    }

    [RelayCommand]
    private void SelectColor(string? key) => ColorKey = BlockColor.Normalize(key);

    public BlockEditorViewModel(IScheduleRepository repository, TimeDisplay display, Action saved, PaletteDisplay? palette = null)
    {
        _repository = repository;
        _display = display;
        _saved = saved;
        _palette = palette ?? new PaletteDisplay();
        Swatches = BlockColor.Keys
            .Select(key => new ColorSwatchOption(key, _palette.Palette.Get(key).Name, _palette.Brushes(key).Stripe))
            .ToList();
        _palette.PropertyChanged += (_, _) =>
        {
            foreach (var swatch in Swatches)
            {
                swatch.Brush = _palette.Brushes(swatch.Key).Stripe;
            }
        };
        OnColorKeyChanged(ColorKey);
    }

    // Position of the block being edited in the saved schedule; null when adding a new one.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(Heading))]
    public partial int? EditingIndex { get; set; }

    public bool IsEditing => EditingIndex is not null;
    public string Heading => IsEditing ? "Edit block" : "New block";

    public void OpenNew(TimeOnly start) => OpenNew(start, start.AddHours(1));

    public void OpenNew(TimeOnly start, TimeOnly end) => Open(null, "", start, end, BlockColor.Default);

    public void OpenEdit(int index, ScheduleBlock block) => Open(index, block.Title, block.Start, block.End, block.Color);

    private void Open(int? index, string title, TimeOnly start, TimeOnly end, string color)
    {
        EditingIndex = index;
        Title = title;
        StartText = _display.Time(start);
        EndText = _display.Time(end);
        ColorKey = BlockColor.Normalize(color);
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
        var block = new ScheduleBlock(Title, start, end, ColorKey);
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
