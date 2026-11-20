using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The slots the clock widget is showing right now: the configured slots fitted to the widget's size (see ClockSlotLayout), each with
// its own state. The view reports its size with Resize and clears it when it is not on screen. A slot that the new plan leaves
// unchanged keeps its object (and the tool it is on), so a resize does not reset what the user scrolled to.
public partial class ClockSlotsViewModel : ViewModelBase
{
    private readonly ClockToolsViewModel _tools;
    private readonly AlarmsViewModel _alarms;
    private (double Width, double Height)? _size;

    [ObservableProperty]
    public partial IReadOnlyList<ClockSlotViewModel> Slots { get; private set; } = [];

    [ObservableProperty]
    public partial bool Horizontal { get; private set; } = true;

    // Whether some slot can show the alarm; if not, a ringing alarm uses the banner instead.
    // How much room each slot has along the widget (pixels), in the order of Slots; changes with every resize.
    [ObservableProperty]
    public partial IReadOnlyList<double> Extents { get; private set; } = [];

    public bool AlarmReachable => Slots.Any(s => s.Tools.Contains(ClockTool.Alarm));

    public ClockSlotsViewModel(ClockToolsViewModel tools, AlarmsViewModel alarms)
    {
        _tools = tools;
        _alarms = alarms;
        _alarms.IsShownOnWidget = () => AlarmReachable;
        _tools.PropertyChanged += OnToolsChanged;
        _alarms.PropertyChanged += OnAlarmsChanged;
    }

    public void Resize(double width, double height)
    {
        _size = (width, height);
        Apply();
    }

    // The widget is off screen.
    public void Clear()
    {
        _size = null;
        Slots = [];
        OnPropertyChanged(nameof(AlarmReachable));
    }

    public void Refresh()
    {
        foreach (var slot in Slots)
        {
            slot.Refresh();
        }
    }

    private void OnToolsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockToolsViewModel.Slots))
        {
            Apply();
        }
    }

    private void OnAlarmsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlarmsViewModel.Ringing) && _alarms.Ringing is not null)
        {
            foreach (var slot in Slots)
            {
                slot.Select(ClockTool.Alarm);
            }
        }
    }

    private void Apply()
    {
        if (_size is not { } size)
        {
            return;
        }
        var plan = ClockSlotLayout.Plan(_tools.Slots, size.Width, size.Height);
        Horizontal = plan.Horizontal;
        var slots = plan.Slots.Select((p, i) => Reuse(i, p)).ToList();
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Extent = plan.Slots[i].Extent;
        }
        var extents = plan.Slots.Select(p => p.Extent).ToList();
        if (!extents.SequenceEqual(Extents))
        {
            Extents = extents;
        }
        if (!slots.SequenceEqual(Slots))
        {
            Slots = slots;
        }
        OnPropertyChanged(nameof(AlarmReachable));
    }

    private ClockSlotViewModel Reuse(int index, ClockSlotPlan plan)
    {
        if (index < Slots.Count && Slots[index] is { } old && old.Scrolling == plan.Scrolling && old.Tools.SequenceEqual(plan.Tools))
        {
            return old;
        }
        // A different set: keep the tool on screen if the new set still has it.
        var keep = index < Slots.Count ? plan.Tools.ToList().IndexOf(Slots[index].Current) : -1;
        return new ClockSlotViewModel(plan, _tools, _alarms, keep < 0 ? 0 : keep);
    }
}
