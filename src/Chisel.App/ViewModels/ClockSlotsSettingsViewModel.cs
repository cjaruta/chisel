using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One slot on the Clock page: pinned to one tool, or scrolling through the tools that are ticked.
public partial class ClockSlotSettingsViewModel : ObservableObject
{
    public string Title { get; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinned))]
    [NotifyPropertyChangedFor(nameof(ModeIndex))]
    public partial bool Scrolling { get; set; }

    public bool IsPinned => !Scrolling;

    // Position in ClockSlotsSettingsViewModel.ModeNames: 0 pinned, 1 scrolling.
    public int ModeIndex
    {
        get => Scrolling ? 1 : 0;
        set => Scrolling = value == 1;
    }

    // Which tool it is pinned to: a position in ClockSlotsSettingsViewModel.ToolNames.
    [ObservableProperty]
    public partial int PinnedIndex { get; set; }

    [ObservableProperty]
    public partial bool HasClock { get; set; }

    [ObservableProperty]
    public partial bool HasTimer { get; set; }

    [ObservableProperty]
    public partial bool HasStopwatch { get; set; }

    [ObservableProperty]
    public partial bool HasAlarm { get; set; }

    [ObservableProperty]
    public partial bool HasEmpty { get; set; }

    [RelayCommand]
    private void SelectMode(bool scrolling) => Scrolling = scrolling;

    public ClockSlotSettingsViewModel(string title)
    {
        Title = title;
    }

    public ClockSlotConfig ToConfig()
    {
        if (!Scrolling)
        {
            return ClockSlotConfig.Pinned((ClockTool)PinnedIndex);
        }
        var set = (HasClock ? ClockToolSet.Clock : 0) | (HasTimer ? ClockToolSet.Timer : 0) | (HasStopwatch ? ClockToolSet.Stopwatch : 0)
            | (HasAlarm ? ClockToolSet.Alarm : 0) | (HasEmpty ? ClockToolSet.Empty : 0);
        var first = Enum.GetValues<ClockTool>().Cast<ClockTool?>().FirstOrDefault(t => set.HasFlag(ClockSlotConfig.Flag(t!.Value))) ?? ClockTool.Empty;
        return new ClockSlotConfig(true, first, set);
    }

    public void Load(ClockSlotConfig config)
    {
        Scrolling = config.IsScrolling;
        PinnedIndex = (int)config.Tool;
        HasClock = config.Set.HasFlag(ClockToolSet.Clock);
        HasTimer = config.Set.HasFlag(ClockToolSet.Timer);
        HasStopwatch = config.Set.HasFlag(ClockToolSet.Stopwatch);
        HasAlarm = config.Set.HasFlag(ClockToolSet.Alarm);
        HasEmpty = config.Set.HasFlag(ClockToolSet.Empty);
    }
}

// The slots part of the Clock page: how many slots (one to three) and, for each, pinned or scrolling and which tools. These are
// the only place the slots are set up; the widget itself has no such controls. Every change is saved at once, and a change made
// elsewhere is shown here. A slot taken away keeps its settings, so adding it back restores them.
public partial class ClockSlotsSettingsViewModel : ViewModelBase
{
    private readonly ClockToolsViewModel _tools;
    private bool _syncing;

    public static IReadOnlyList<string> ToolNames { get; } = ["Clock", "Timer", "Stopwatch", "Alarm", "Empty"];
    public static IReadOnlyList<string> SlotCountNames { get; } = ["1", "2", "3"];
    public static IReadOnlyList<string> ModeNames { get; } = ["Pinned", "Scrolling"];

    public IReadOnlyList<ClockSlotSettingsViewModel> Slots { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSlotCountOne))]
    [NotifyPropertyChangedFor(nameof(IsSlotCountTwo))]
    [NotifyPropertyChangedFor(nameof(IsSlotCountThree))]
    public partial int SlotCountIndex { get; set; }

    public int SlotCount => SlotCountIndex + 1;

    public bool IsSlotCountOne => SlotCountIndex == 0;
    public bool IsSlotCountTwo => SlotCountIndex == 1;
    public bool IsSlotCountThree => SlotCountIndex == 2;

    [RelayCommand]
    private void SelectSlotCount(object? count) => SlotCountIndex = System.Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture) - 1;

    public ClockSlotsSettingsViewModel(ClockToolsViewModel tools)
    {
        _tools = tools;
        Slots = [new("Main slot"), new("Slot 2"), new("Slot 3")];
        foreach (var slot in Slots)
        {
            slot.Load(ClockSlotConfig.Pinned(ClockTool.Empty));
            slot.PropertyChanged += OnSlotChanged;
        }
        _tools.PropertyChanged += OnToolsChanged;
        Sync();
    }

    partial void OnSlotCountIndexChanged(int value)
    {
        if (!_syncing)
        {
            UpdateShown();
            Commit();
        }
    }

    private void OnSlotChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_syncing && e.PropertyName is not (nameof(ClockSlotSettingsViewModel.IsShown) or nameof(ClockSlotSettingsViewModel.IsPinned) or nameof(ClockSlotSettingsViewModel.ModeIndex)))
        {
            Commit();
        }
    }

    private void OnToolsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockToolsViewModel.Slots))
        {
            Sync();
        }
    }

    private void Commit() => _tools.SetSlots(new ClockSlotsConfig(Slots.Take(SlotCount).Select(s => s.ToConfig()).ToList()));

    private void UpdateShown()
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            Slots[i].IsShown = i < SlotCount;
        }
    }

    // Points the controls at the saved slots.
    private void Sync()
    {
        _syncing = true;
        var config = _tools.Slots.Slots;
        SlotCountIndex = config.Count - 1;
        for (var i = 0; i < config.Count; i++)
        {
            Slots[i].Load(config[i]);
        }
        UpdateShown();
        _syncing = false;
        OnPropertyChanged(nameof(SlotCount));
    }
}
