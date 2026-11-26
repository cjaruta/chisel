using System.Globalization;

namespace Chisel.Core.Habits;

// Checks what the user typed into the habit form. Returns the message to show, or null when it is fine. editing: the habit being
// changed (it may keep its own name and does not count against the cap), or null when adding.
public static class HabitValidation
{
    public const int MaxHabits = 30;
    public const int MaxNameLength = 40;

    public static string? Error(string? name, string? emoji, HabitDays days, IReadOnlyList<Habit> habits, Guid? editing)
    {
        if (editing is null && habits.Count >= MaxHabits)
        {
            return $"You can track up to {MaxHabits} habits.";
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Give the habit a name.";
        }
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            return $"Keep the name under {MaxNameLength} characters.";
        }
        if (habits.Any(h => h.Id != editing && string.Equals(h.Name.Trim(), trimmed, StringComparison.CurrentCultureIgnoreCase)))
        {
            return "You already have a habit with that name.";
        }
        if (!string.IsNullOrWhiteSpace(emoji) && new StringInfo(emoji.Trim()).LengthInTextElements > 1)
        {
            return "Use a single emoji, or leave it empty.";
        }
        if (days == HabitDays.None)
        {
            return "Pick at least one day.";
        }
        return null;
    }
}
