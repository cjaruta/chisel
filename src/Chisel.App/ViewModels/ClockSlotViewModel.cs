using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// One dot of a slot's indicator: a tool of the slot, whether it is the one on screen, and whether it has something going on
// (a running timer or stopwatch, a ringing alarm) with the short text for it.
public sealed record SlotDot(ClockTool Tool, bool IsCurrent, bool IsActive, string Text);

// One slot of the clock widget as drawn now: the tools it can show, the one on screen, and the dots that tell the others apart.
// The wheel cycles through the tools (wrapping); it never changes a value inside a tool.
public partial class ClockSlotViewModel : ViewModelBase
{
    private readonly ClockToolsViewModel _tools;
    private readonly AlarmsViewModel _alarms;

    public IReadOnlyList<ClockTool> Tools { get; }
    public bool Scrolling { get; }

    // The room this slot has along the widget, in pixels (kept current by ClockSlotsViewModel; the view sizes itself from it).
    public double Extent { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current))]
    public partial int Index { get; private set; }

    // The way the last move went (+1 forward, -1 back), for the slide transition.
    public int LastStep { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<SlotDot> Dots { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasActiveOffscreen { get; private set; }

    public ClockTool Current => Tools[Index];

    // Whether the wheel does anything here: only a scrolling slot with more than one tool.
    public bool CanCycle => Scrolling && Tools.Count > 1;

    public bool ShowDots => CanCycle;

    public ClockSlotViewModel(ClockSlotPlan plan, ClockToolsViewModel tools, AlarmsViewModel alarms, int startIndex = 0)
    {
        Tools = plan.Tools;
        Scrolling = plan.Scrolling;
        Extent = plan.Extent;
        _tools = tools;
        _alarms = alarms;
        Index = Tools.Count == 0 ? 0 : System.Math.Clamp(startIndex, 0, Tools.Count - 1);
        Refresh();
    }

    public void Cycle(int step)
    {
        if (!CanCycle || step == 0)
        {
            return;
        }
        LastStep = step;
        Index = ClockSlotLayout.Cycle(Index, Tools.Count, step);
        Refresh();
    }

    // Jumps to a tool of this slot (right-click menu, a ringing alarm); a pinned slot or a tool it does not have is ignored.
    public void Select(ClockTool tool)
    {
        var index = Tools.ToList().IndexOf(tool);
        if (!CanCycle || index < 0 || index == Index)
        {
            return;
        }
        LastStep = index > Index ? 1 : -1;
        Index = index;
        Refresh();
    }

    // Recomputes the dots from the tools' state. Cheap enough for every tick: nothing is raised unless something changed.
    public void Refresh()
    {
        var dots = Tools.Select((tool, i) =>
        {
            var indicator = tool == ClockTool.Alarm
                ? new ToolIndicator(_alarms.Ringing is not null, "Ringing")
                : _tools.IndicatorFor(tool);
            return new SlotDot(tool, i == Index, indicator.Active, indicator.Active ? indicator.Text : "");
        }).ToList();
        if (!dots.SequenceEqual(Dots))
        {
            Dots = dots;
        }
        HasActiveOffscreen = CanCycle && dots.Any(d => d.IsActive && !d.IsCurrent);
    }
}
