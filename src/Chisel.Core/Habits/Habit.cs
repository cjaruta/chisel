using System.Collections.Immutable;
using Chisel.Core.Countdown;

namespace Chisel.Core.Habits;

// The weekdays a habit is due on, one bit per DayOfWeek (Sunday is bit 0).
[Flags]
public enum HabitDays : byte
{
    None = 0,
    Sunday = 1,
    Monday = 2,
    Tuesday = 4,
    Wednesday = 8,
    Thursday = 16,
    Friday = 32,
    Saturday = 64,
    Every = 127,
}

public static class HabitDaysExtensions
{
    public static HabitDays Of(DayOfWeek day) => (HabitDays)(1 << (int)day);

    public static bool Includes(this HabitDays days, DayOfWeek day) => (days & Of(day)) != 0;
}

// One daily habit: a name, an optional emoji or color tag, the weekdays it is due, and the local calendar dates it was done.
// Dates are calendar dates (never timestamps), so time zone and daylight-saving changes cannot move a check-in to another day.
// Created is the day it was added: days before it are shown as not yet tracked rather than missed.
public sealed record Habit(
    Guid Id, string Name, string? Emoji = null, CountdownColor Color = CountdownColor.None, HabitDays Days = HabitDays.Every,
    ImmutableSortedSet<DateOnly>? Done = null, DateOnly Created = default)
{
    public ImmutableSortedSet<DateOnly> Done { get; init; } = Done ?? ImmutableSortedSet<DateOnly>.Empty;

    public bool IsDue(DateOnly date) => Days.Includes(date.DayOfWeek);

    public bool IsDone(DateOnly date) => Done.Contains(date);

    // The same habit with the date checked if it was not, and unchecked if it was.
    public Habit Toggled(DateOnly date) => this with { Done = Done.Contains(date) ? Done.Remove(date) : Done.Add(date) };
}

// Every habit, in the user's order. Saved as one record (replace-all).
public sealed record HabitData(IReadOnlyList<Habit> Habits)
{
    public static HabitData Empty { get; } = new([]);
}

public interface IHabitRepository
{
    HabitData Get();
    void Save(HabitData data);
}
