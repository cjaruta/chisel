using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Chisel.Core.Countdown;

namespace Chisel.App.ViewModels;

// One event as the widget shows it: how far off it is, its progress bar, its date, and the full text for screen readers.
public sealed record CountdownItem(CountdownEntry Entry, CountdownStatus Status, double Progress, string DateText, string AutomationName)
{
    public bool IsToday => Status.Phase == CountdownPhase.Today;
    public bool IsPast => Status.Phase == CountdownPhase.Past;
}

// The countdown widget's state: the saved events and options, the items to show, and the inline add/edit form. Every change is
// saved at once. One timer keeps the text current: it wakes at the next local midnight (new day counts, yearly roll-over, the
// past-event setting and the "event day" alert), and once a minute only while a widget is on screen showing hours or minutes.
// With no events it does not run at all.
public sealed class CountdownViewModel : ViewModelBase, IDisposable
{
    private readonly ICountdownRepository? _repository;
    private readonly TimeProvider _time;
    private readonly AlertsViewModel? _alerts;
    private readonly Action<Action> _dispatch;
    private readonly CultureInfo _culture;
    private readonly ITimer _timer;
    private readonly List<object> _watchers = [];
    private List<CountdownEntry> _events;
    private PastEventPolicy _pastEvents;
    private bool _sortByDate;
    private bool _notify;

    public CountdownViewModel(ICountdownRepository? repository, TimeProvider time, AlertsViewModel? alerts = null,
        Action<Action>? dispatch = null, CultureInfo? culture = null)
    {
        _repository = repository;
        _time = time;
        _alerts = alerts;
        _dispatch = dispatch ?? (action => action());
        _culture = culture ?? CultureInfo.CurrentCulture;
        var saved = repository?.Get() ?? CountdownSettings.Default;
        _events = saved.Events.ToList();
        _pastEvents = saved.PastEvents;
        _sortByDate = saved.SortByDate;
        _notify = saved.Notify;
        _timer = time.CreateTimer(_ => _dispatch(Refresh), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Refresh();
    }

    // Items ----------------------------------------------------------------------------------------------------------------

    // The events to show (not archived), in date order or the user's own order.
    public IReadOnlyList<CountdownItem> Items { get; private set; } = [];

    // The event shown large: the soonest one still to come (or today's), else the most recent past one.
    public CountdownItem? Nearest { get; private set; }

    // The items other than Nearest, in display order.
    public IReadOnlyList<CountdownItem> Others { get; private set; } = [];

    public bool IsEmpty => Items.Count == 0;

    public int ArchivedCount => _events.Count(e => e.Archived);

    public bool CanAdd => _events.Count < CountdownValidation.MaxEvents;

    // How many times the items have been worked out (for tests of the timer).
    public int Updates { get; private set; }

    // Options --------------------------------------------------------------------------------------------------------------

    public PastEventPolicy PastEvents
    {
        get => _pastEvents;
        set
        {
            if (_pastEvents == value)
            {
                return;
            }
            _pastEvents = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsShowAgo));
            OnPropertyChanged(nameof(IsArchive));
            OnPropertyChanged(nameof(IsDelete));
            Persist();
            Refresh();
        }
    }

    public bool IsShowAgo { get => _pastEvents == PastEventPolicy.ShowAgo; set { if (value) PastEvents = PastEventPolicy.ShowAgo; } }
    public bool IsArchive { get => _pastEvents == PastEventPolicy.Archive; set { if (value) PastEvents = PastEventPolicy.Archive; } }
    public bool IsDelete { get => _pastEvents == PastEventPolicy.Delete; set { if (value) PastEvents = PastEventPolicy.Delete; } }

    public bool SortByDate
    {
        get => _sortByDate;
        set
        {
            if (_sortByDate == value)
            {
                return;
            }
            _sortByDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomOrder));
            Persist();
            Refresh();
        }
    }

    public bool IsCustomOrder { get => !_sortByDate; set => SortByDate = !value; }

    public bool Notify
    {
        get => _notify;
        set
        {
            if (_notify == value)
            {
                return;
            }
            _notify = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public void DeleteArchived()
    {
        if (_events.RemoveAll(e => e.Archived) > 0)
        {
            Persist();
            Refresh();
        }
    }

    // Form -----------------------------------------------------------------------------------------------------------------

    public bool IsFormOpen { get; private set; }
    public Guid? EditingId { get; private set; }
    public bool IsEditing => EditingId is not null;
    public string FormName { get; set; } = "";
    public DateTime? FormDate { get; set; }
    public string FormTime { get; set; } = "";
    public string FormEmoji { get; set; } = "";
    public CountdownColor FormColor { get; set; }
    public bool FormYearly { get; set; }
    public string? FormError { get; private set; }

    public void OpenAdd()
    {
        EditingId = null;
        FormName = "";
        FormDate = Today().AddDays(7).ToDateTime(TimeOnly.MinValue);
        FormTime = "";
        FormEmoji = "";
        FormColor = CountdownColor.None;
        FormYearly = false;
        FormError = CanAdd ? null : $"You can track up to {CountdownValidation.MaxEvents} events.";
        SetFormOpen(true);
    }

    public void OpenEdit(Guid id)
    {
        if (_events.FirstOrDefault(e => e.Id == id) is not { } e)
        {
            return;
        }
        EditingId = id;
        FormName = e.Name;
        FormDate = e.Date.ToDateTime(TimeOnly.MinValue);
        FormTime = e.Time?.ToString("t", _culture) ?? "";
        FormEmoji = e.Emoji ?? "";
        FormColor = e.Color;
        FormYearly = e.Repeat == CountdownRepeat.Yearly;
        FormError = null;
        SetFormOpen(true);
    }

    public void Cancel() => SetFormOpen(false);

    // Checks the form and adds or updates the event. False (with FormError set) when the input is not valid.
    public bool Save()
    {
        var date = FormDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
        var result = CountdownValidation.Validate(FormName, date, FormTime, FormEmoji, _events.Count, adding: EditingId is null, _culture);
        if (result.Error is not null)
        {
            FormError = result.Error;
            OnPropertyChanged(nameof(FormError));
            return false;
        }
        var emoji = string.IsNullOrWhiteSpace(FormEmoji) ? null : FormEmoji.Trim();
        var repeat = FormYearly ? CountdownRepeat.Yearly : CountdownRepeat.None;
        var index = _events.FindIndex(e => e.Id == EditingId);
        if (index >= 0)
        {
            var old = _events[index];
            var moved = old.Date != date!.Value || old.Time != result.Time || old.Repeat != repeat;
            _events[index] = old with
            {
                Name = FormName.Trim(), Date = date!.Value, Time = result.Time, Repeat = repeat, Emoji = emoji, Color = FormColor,
                Archived = moved ? false : old.Archived, NotifiedOn = moved ? null : old.NotifiedOn,
            };
        }
        else
        {
            _events.Add(new CountdownEntry(Guid.NewGuid(), FormName.Trim(), date!.Value, result.Time, repeat, emoji, FormColor, _time.GetUtcNow()));
        }
        Persist();
        SetFormOpen(false);
        Refresh();
        return true;
    }

    public void DeleteEditing()
    {
        if (EditingId is { } id && _events.RemoveAll(e => e.Id == id) > 0)
        {
            Persist();
        }
        SetFormOpen(false);
        Refresh();
    }

    // Moves an event up (-1) or down (+1) among the shown ones, in the user's own order.
    public void Move(Guid id, int delta)
    {
        var shown = _events.Where(e => !e.Archived).ToList();
        var at = shown.FindIndex(e => e.Id == id);
        var to = at + delta;
        if (at < 0 || to < 0 || to >= shown.Count)
        {
            return;
        }
        var a = _events.IndexOf(shown[at]);
        var b = _events.IndexOf(shown[to]);
        (_events[a], _events[b]) = (_events[b], _events[a]);
        Persist();
        Refresh();
    }

    private void SetFormOpen(bool open)
    {
        IsFormOpen = open;
        if (!open)
        {
            EditingId = null;
        }
        foreach (var name in new[]
        {
            nameof(IsFormOpen), nameof(EditingId), nameof(IsEditing), nameof(FormName), nameof(FormDate), nameof(FormTime),
            nameof(FormEmoji), nameof(FormColor), nameof(FormYearly), nameof(FormError),
        })
        {
            OnPropertyChanged(name);
        }
    }

    // Updating -------------------------------------------------------------------------------------------------------------

    // A widget showing the countdown; while one is, hours and minutes are kept current.
    public void Attach(object watcher)
    {
        if (!_watchers.Contains(watcher))
        {
            _watchers.Add(watcher);
            Refresh();
        }
    }

    public void Detach(object watcher)
    {
        if (_watchers.Remove(watcher))
        {
            Schedule(_time.GetUtcNow());
        }
    }

    // Works the items out for now, applies the past-event setting and the event-day alert, and sets the timer for the next change.
    public void Refresh()
    {
        var now = _time.GetUtcNow();
        var zone = _time.LocalTimeZone;
        var today = CountdownMath.Today(now, zone);

        var kept = CountdownMath.ApplyPastPolicy(_events, _pastEvents, today);
        var changed = !ReferenceEquals(kept, _events);
        _events = kept.ToList();
        changed |= AlertToday(now, zone, today);
        if (changed)
        {
            Persist();
        }

        var shown = _sortByDate ? CountdownMath.Sorted(_events, now, zone) : _events.Where(e => !e.Archived).ToList();
        Items = shown.Select(e => Item(e, now, zone)).ToList();
        var nearestEntry = CountdownMath.Sorted(_events, now, zone).FirstOrDefault();
        Nearest = Items.FirstOrDefault(i => i.Entry.Id == nearestEntry?.Id && !i.IsPast) ?? Items.FirstOrDefault(i => !i.IsPast) ?? Items.FirstOrDefault();
        Others = Items.Where(i => !ReferenceEquals(i, Nearest)).ToList();
        Updates++;
        foreach (var name in new[] { nameof(Items), nameof(Nearest), nameof(Others), nameof(IsEmpty), nameof(ArchivedCount), nameof(CanAdd) })
        {
            OnPropertyChanged(name);
        }
        Schedule(now);
    }

    private CountdownItem Item(CountdownEntry e, DateTimeOffset now, TimeZoneInfo zone)
    {
        var status = CountdownMath.Status(e, now, zone);
        var date = status.Date.ToString("ddd, MMM d, yyyy", _culture) + (e.Time is { } t ? " · " + t.ToString("t", _culture) : "");
        return new CountdownItem(e, status, CountdownMath.Progress(e, now, zone), date, CountdownMath.Spoken(e, status));
    }

    // One alert for all of today's events not yet announced today. True when it marked any (so they are saved).
    private bool AlertToday(DateTimeOffset now, TimeZoneInfo zone, DateOnly today)
    {
        if (!_notify || _alerts is null)
        {
            return false;
        }
        var due = _events.Where(e => !e.Archived && e.NotifiedOn != today && CountdownMath.Status(e, now, zone).Phase == CountdownPhase.Today).ToList();
        if (due.Count == 0)
        {
            return false;
        }
        _alerts.Show("Countdown", "Today: " + string.Join(", ", due.Select(e => e.Name)));
        _events = _events.Select(e => due.Contains(e) ? e with { NotifiedOn = today } : e).ToList();
        return true;
    }

    private void Schedule(DateTimeOffset now)
    {
        var zone = _time.LocalTimeZone;
        var next = CountdownMath.NextUpdate(_events, now, zone);
        if (next is { } at && _watchers.Count == 0)
        {
            // Nobody is looking: only the day change matters (counts, archiving, the alert).
            at = CountdownMath.Instant(CountdownMath.Local(now, zone).Date.AddDays(1), zone);
            next = at;
        }
        _timer.Change(next is { } due ? Max(due - now, TimeSpan.FromMilliseconds(1)) : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private DateOnly Today() => CountdownMath.Today(_time.GetUtcNow(), _time.LocalTimeZone);

    private void Persist() => _repository?.Save(new CountdownSettings(_events.ToList(), _pastEvents, _sortByDate, _notify));

    public void Dispose() => _timer.Dispose();
}
