namespace Chisel.Core.Habits;

public readonly record struct HabitStreak(int Current, int Best);

// How one day of a habit's history looks.
public enum HabitDayState
{
    Done,    // checked (on a due day or not)
    Missed,  // a due day that ended unchecked
    Pending, // today, due and not checked yet
    Off,     // not a due day, or before the habit was added
}

// Streaks count due days in a row that were done. Days the habit is not due on are skipped: they neither count nor break a
// streak. Today counts once it is done, but an unchecked today does not break the streak until the day is over. Dates after
// today are ignored.
public static class HabitStreaks
{
    public static HabitStreak For(Habit habit, DateOnly today)
    {
        if (habit.Done.Count == 0 || habit.Days == HabitDays.None || habit.Done.Min > today)
        {
            return default;
        }

        var best = 0;
        var run = 0;
        for (var d = habit.Done.Min; d <= today; d = d.AddDays(1))
        {
            if (!habit.IsDue(d))
            {
                continue;
            }
            if (habit.IsDone(d))
            {
                run++;
                best = Math.Max(best, run);
            }
            else if (d != today)
            {
                run = 0;
            }
        }
        return new HabitStreak(run, best);
    }

    // The last `days` days, oldest first, with today last.
    public static HabitDayState[] History(Habit habit, DateOnly today, int days)
    {
        var states = new HabitDayState[days];
        for (var i = 0; i < days; i++)
        {
            states[i] = State(habit, today.AddDays(i - days + 1), today);
        }
        return states;
    }

    public static HabitDayState State(Habit habit, DateOnly date, DateOnly today) =>
        habit.IsDone(date) ? HabitDayState.Done
        : !habit.IsDue(date) || date < habit.Created || date > today ? HabitDayState.Off
        : date == today ? HabitDayState.Pending
        : HabitDayState.Missed;
}
