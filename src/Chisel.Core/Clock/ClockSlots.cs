namespace Chisel.Core.Clock;

// What a slot of the clock widget can show.
public enum ClockTool { Clock, Timer, Stopwatch, Alarm, Empty }

[Flags]
public enum ClockToolSet { None = 0, Clock = 1, Timer = 2, Stopwatch = 4, Alarm = 8, Empty = 16 }

// One slot, set up in settings: pinned to one tool, or scrolling through a set (the mouse wheel moves between them).
public sealed record ClockSlotConfig(bool IsScrolling, ClockTool Tool, ClockToolSet Set)
{
    public static ClockSlotConfig Pinned(ClockTool tool) => new(false, tool, ClockToolSet.None);

    public static ClockSlotConfig Scrolling(params ClockTool[] tools) =>
        new(true, tools.FirstOrDefault(), tools.Aggregate(ClockToolSet.None, (all, t) => all | Flag(t)));

    // The tools this slot shows, in the fixed order Clock, Timer, Stopwatch, Alarm, Empty. A scrolling slot with nothing chosen is Empty.
    public IReadOnlyList<ClockTool> Tools =>
        !IsScrolling ? [Tool]
        : Set == ClockToolSet.None ? [ClockTool.Empty]
        : Enum.GetValues<ClockTool>().Where(t => Set.HasFlag(Flag(t))).ToList();

    public static ClockToolSet Flag(ClockTool tool) => (ClockToolSet)(1 << (int)tool);
}

// The slots from the main one on (one to three). The default is the clock pinned in the main slot and one scrolling slot.
public sealed record ClockSlotsConfig(IReadOnlyList<ClockSlotConfig> Slots)
{
    public const int MaxSlots = 3;

    public static ClockSlotsConfig Default { get; } = new([
        ClockSlotConfig.Pinned(ClockTool.Clock),
        ClockSlotConfig.Scrolling(ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm, ClockTool.Empty),
    ]);

    public ClockSlotsConfig Normalized()
    {
        var slots = Slots.Take(MaxSlots).ToList();
        return new(slots.Count == 0 ? [ClockSlotConfig.Pinned(ClockTool.Clock)] : slots);
    }

    public bool Equals(ClockSlotsConfig? other) => other is not null && Slots.SequenceEqual(other.Slots);

    public override int GetHashCode() => Slots.Aggregate(0, (hash, slot) => HashCode.Combine(hash, slot));
}

// What is drawn: one slot with the tools it can show (after absorbing) and its share of the widget.
public sealed record ClockSlotPlan(IReadOnlyList<ClockTool> Tools, bool Scrolling, double Weight);

public sealed record ClockLayoutPlan(bool Horizontal, IReadOnlyList<ClockSlotPlan> Slots);

public static class ClockSlotLayout
{
    // The room one slot needs along the widget's long side.
    public const double MinSlotExtent = 160;

    // Horizontal when the widget is as wide as it is tall or wider, vertical when it is taller.
    public static bool IsHorizontal(double width, double height) => width >= height;

    // How many slots fit: one per MinSlotExtent along the side the slots run along, from one to three.
    public static int Capacity(double width, double height) =>
        Math.Clamp((int)Math.Floor((IsHorizontal(width, height) ? width : height) / MinSlotExtent), 1, ClockSlotsConfig.MaxSlots);

    // The slots that fit. If some do not, the last visible one scrolls through its own tools and those of the hidden slots, so
    // every tool stays reachable. The main (first) slot is given twice the room of each other one.
    public static ClockLayoutPlan Plan(ClockSlotsConfig config, double width, double height)
    {
        var slots = config.Normalized().Slots;
        var capacity = Math.Min(Capacity(width, height), slots.Count);
        var plans = new List<ClockSlotPlan>();
        for (var i = 0; i < capacity; i++)
        {
            var tools = slots[i].Tools;
            var scrolling = slots[i].IsScrolling;
            if (i == capacity - 1 && slots.Count > capacity)
            {
                tools = Absorb(tools, slots.Skip(capacity).SelectMany(s => s.Tools));
                scrolling = true;
            }
            plans.Add(new ClockSlotPlan(tools, scrolling, i == 0 ? 2 : 1));
        }
        return new ClockLayoutPlan(IsHorizontal(width, height), plans);
    }

    private static IReadOnlyList<ClockTool> Absorb(IReadOnlyList<ClockTool> own, IEnumerable<ClockTool> hidden)
    {
        var tools = own.Where(t => t != ClockTool.Empty).ToList();
        tools.AddRange(hidden.Where(t => t != ClockTool.Empty && !tools.Contains(t)));
        if (own.Contains(ClockTool.Empty) || tools.Count == 0)
        {
            tools.Add(ClockTool.Empty);
        }
        return tools;
    }

    // The index after moving `step` places, wrapping at both ends.
    public static int Cycle(int index, int count, int step) => count <= 0 ? 0 : ((index + step) % count + count) % count;

    // One step per wheel notch: up goes back, down goes forward.
    public static int StepOf(double wheelDelta) => wheelDelta > 0 ? -1 : wheelDelta < 0 ? 1 : 0;
}
