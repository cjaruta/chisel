using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;

namespace Chisel.App.ViewModels;

// One habit as the widget shows it today: whether it is done and due, its streaks, its recent days (oldest first, today last),
// and the text for the streak line and for screen readers.
public sealed record HabitItem(Habit Habit, bool DoneToday, bool DueToday, HabitStreak Streak, HabitDayState[] History, string StreakText, string AutomationName)
{
    public Guid Id => Habit.Id;
    public string Name => Habit.Name;
}

// The habit tracker's state: the habits in the user's order, today's check-ins and the summary. "Today" is the local calendar
// date; one timer wakes at the next local midnight (worked out in the clock's own zone, so a daylight-saving day is the right
// length) to roll the day over, and nothing else runs while idle. Check-ins are stored as calendar dates, so a time zone or
// daylight-saving change never moves one to another day. Every change is saved at once.
public sealed class HabitsViewModel : ViewModelBase, IDisposable
{
    // How many past days the items carry: enough for the largest history the widget draws (5 weeks).
    public const int HistoryDays = HabitLayout.WeekDays * HabitLayout.GridWeeks;

    private readonly IHabitRepository? _repository;
    private readonly TimeProvider _time;
    private readonly Action<Action> _dispatch;
    private readonly ITimer _midnight;
    private List<Habit> _habits;
    private bool _disposed;

    public HabitsViewModel(IHabitRepository? repository, TimeProvider time, Action<Action>? dispatch = null)
    {
        _repository = repository;
        _time = time;
        _dispatch = dispatch ?? (action => action());
        _habits = (repository?.Get() ?? HabitData.Empty).Habits.ToList();
        Today = LocalToday();
        _midnight = time.CreateTimer(_ => _dispatch(Refresh), null, UntilMidnight(), Timeout.InfiniteTimeSpan);
        Rebuild();
    }

    // Shown ------------------------------------------------------------------------------------------------------------------

    public DateOnly Today { get; private set; }

    public IReadOnlyList<HabitItem> Items { get; private set; } = [];

    public bool IsEmpty => _habits.Count == 0;

    // Habits due today, and how many of them are done.
    public int DueCount { get; private set; }

    public int DoneCount { get; private set; }

    public double Fraction => DueCount == 0 ? 0 : (double)DoneCount / DueCount;

    // Every habit due today is done (the quiet celebration).
    public bool AllDone => DueCount > 0 && DoneCount == DueCount;

    public string SummaryText { get; private set; } = "";

    // "3/5" for inside the ring.
    public string CountText { get; private set; } = "";

    // How many times the shown values were rebuilt (for tests that nothing runs while idle).
    public int Rebuilds { get; private set; }

    // Check-ins --------------------------------------------------------------------------------------------------------------

    public bool Toggle(Guid id) => Toggle(id, Today);

    // Checks or unchecks a day. Today and past days can be changed (to fix a missed check-in); future days cannot.
    public bool Toggle(Guid id, DateOnly date)
    {
        var index = _habits.FindIndex(h => h.Id == id);
        if (index < 0 || date > Today)
        {
            return false;
        }
        _habits[index] = _habits[index].Toggled(date);
        Changed();
        return true;
    }

    // Editing ----------------------------------------------------------------------------------------------------------------

    // Adds a habit; returns what was wrong (nothing is added) or null.
    public string? Add(string? name, string? emoji, CountdownColor color, HabitDays days)
    {
        if (HabitValidation.Error(name, emoji, days, _habits, null) is { } error)
        {
            return error;
        }
        _habits.Add(new Habit(Guid.NewGuid(), name!.Trim(), Emoji(emoji), color, days, Created: Today));
        Changed();
        return null;
    }

    // Changes a habit's name, marker and days, keeping its history.
    public string? Update(Guid id, string? name, string? emoji, CountdownColor color, HabitDays days)
    {
        var index = _habits.FindIndex(h => h.Id == id);
        if (index < 0)
        {
            return null;
        }
        if (HabitValidation.Error(name, emoji, days, _habits, id) is { } error)
        {
            return error;
        }
        _habits[index] = _habits[index] with { Name = name!.Trim(), Emoji = Emoji(emoji), Color = color, Days = days };
        Changed();
        return null;
    }

    public string? Rename(Guid id, string? name) =>
        _habits.FirstOrDefault(h => h.Id == id) is { } h ? Update(id, name, h.Emoji, h.Color, h.Days) : null;

    public void Delete(Guid id)
    {
        if (_habits.RemoveAll(h => h.Id == id) > 0)
        {
            Changed();
        }
    }

    // Moves a habit up (-1) or down (+1) in the list.
    public void Move(Guid id, int delta)
    {
        var index = _habits.FindIndex(h => h.Id == id);
        var to = index + delta;
        if (index < 0 || to < 0 || to >= _habits.Count)
        {
            return;
        }
        var habit = _habits[index];
        _habits.RemoveAt(index);
        _habits.Insert(to, habit);
        Changed();
    }

    private static string? Emoji(string? emoji) => string.IsNullOrWhiteSpace(emoji) ? null : emoji.Trim();

    // The inline form ----------------------------------------------------------------------------------------------------------

    public bool IsFormOpen { get; private set; }

    public Guid? EditingId { get; private set; }

    public bool IsEditing => EditingId is not null;

    public string FormName { get; set; } = "";

    public string FormEmoji { get; set; } = "";

    public CountdownColor FormColor { get; set; }

    public HabitDays FormDays { get; set; } = HabitDays.Every;

    public string? FormError { get; private set; }

    public void OpenAdd() => OpenForm(null, "", "", CountdownColor.None, HabitDays.Every);

    public void OpenEdit(Guid id)
    {
        if (_habits.FirstOrDefault(h => h.Id == id) is { } h)
        {
            OpenForm(id, h.Name, h.Emoji ?? "", h.Color, h.Days);
        }
    }

    private void OpenForm(Guid? id, string name, string emoji, CountdownColor color, HabitDays days)
    {
        (EditingId, FormName, FormEmoji, FormColor, FormDays, FormError) = (id, name, emoji, color, days, null);
        IsFormOpen = true;
        FormChanged();
    }

    // Adds or updates from the form; keeps it open with the error when the input is refused.
    public bool Save()
    {
        FormError = EditingId is { } id
            ? Update(id, FormName, FormEmoji, FormColor, FormDays)
            : Add(FormName, FormEmoji, FormColor, FormDays);
        if (FormError is null)
        {
            IsFormOpen = false;
            EditingId = null;
        }
        FormChanged();
        return FormError is null;
    }

    public void Cancel()
    {
        IsFormOpen = false;
        EditingId = null;
        FormError = null;
        FormChanged();
    }

    public void DeleteEditing()
    {
        if (EditingId is { } id)
        {
            Delete(id);
            Cancel();
        }
    }

    private void FormChanged()
    {
        foreach (var name in new[] { nameof(FormError), nameof(EditingId), nameof(IsEditing), nameof(IsFormOpen) })
        {
            OnPropertyChanged(name);
        }
    }

    // Day roll-over ----------------------------------------------------------------------------------------------------------

    // Looks at the date again (the midnight timer calls this; it is safe to call any time) and sets the next wake-up.
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }
        var today = LocalToday();
        if (today != Today)
        {
            Today = today;
            OnPropertyChanged(nameof(Today));
            Rebuild();
        }
        _midnight.Change(UntilMidnight(), Timeout.InfiniteTimeSpan);
    }

    private DateOnly LocalToday() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    // Time to the next local midnight, from the clock's own zone (so a daylight-saving change that day is counted).
    private TimeSpan UntilMidnight()
    {
        var now = _time.GetLocalNow();
        var next = now.Date.AddDays(1);
        var due = new DateTimeOffset(next, _time.LocalTimeZone.GetUtcOffset(next)) - now;
        return due > TimeSpan.Zero ? due : TimeSpan.FromSeconds(1);
    }

    public void Dispose()
    {
        _disposed = true;
        _midnight.Dispose();
    }

    // Building what is shown -------------------------------------------------------------------------------------------------

    private void Changed()
    {
        _repository?.Save(new HabitData(_habits.ToList()));
        Rebuild();
    }

    private void Rebuild()
    {
        var items = new List<HabitItem>(_habits.Count);
        int due = 0, done = 0;
        foreach (var habit in _habits)
        {
            var isDue = habit.IsDue(Today);
            var isDone = habit.IsDone(Today);
            if (isDue)
            {
                due++;
                done += isDone ? 1 : 0;
            }
            var streak = HabitStreaks.For(habit, Today);
            var streakText = StreakText(streak.Current);
            var status = isDone ? "done" : isDue ? "not done" : "not due today";
            items.Add(new HabitItem(habit, isDone, isDue, streak, HabitStreaks.History(habit, Today, HistoryDays),
                streak.Best > streak.Current ? $"{streakText} · best {streak.Best}" : streakText,
                $"{habit.Name}, {status}, {(streak.Current == 0 ? "no streak" : streakText)}"));
        }
        Items = items;
        DueCount = due;
        DoneCount = done;
        SummaryText = _habits.Count == 0 ? "No habits yet"
            : due == 0 ? "Nothing due today"
            : done == due ? $"All {due} done today"
            : $"{done} of {due} done today";
        CountText = $"{done}/{due}";
        Rebuilds++;
        foreach (var name in new[]
        {
            nameof(Items), nameof(IsEmpty), nameof(DueCount), nameof(DoneCount), nameof(Fraction), nameof(AllDone), nameof(SummaryText),
            nameof(CountText), nameof(Rebuilds),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private static string StreakText(int days) => days switch
    {
        0 => "No streak yet",
        1 => "1 day streak",
        _ => $"{days} day streak",
    };
}
