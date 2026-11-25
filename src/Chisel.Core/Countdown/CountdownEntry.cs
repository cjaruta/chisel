namespace Chisel.Core.Countdown;

public enum CountdownRepeat { None, Yearly }

// What happens to a one-off event once its day is over: keep it and show how long ago, archive it, or delete it.
public enum PastEventPolicy { ShowAgo, Archive, Delete }

// A color tag for an event. The theme turns each into a color.
public enum CountdownColor { None, Red, Orange, Green, Blue, Purple }

// One tracked event: a name, a local date and optional local time, and an optional yearly repeat (birthdays). Created is when it
// was added, the start of its progress bar. NotifiedOn is the last day the "event day" alert was shown for it.
public sealed record CountdownEntry(
    Guid Id, string Name, DateOnly Date, TimeOnly? Time = null, CountdownRepeat Repeat = CountdownRepeat.None,
    string? Emoji = null, CountdownColor Color = CountdownColor.None, DateTimeOffset Created = default,
    bool Archived = false, DateOnly? NotifiedOn = null);

// Everything the countdown widget keeps, saved as one record (replace-all). Events are in the user's own order, which is the
// list order when SortByDate is off.
public sealed record CountdownSettings(
    IReadOnlyList<CountdownEntry> Events, PastEventPolicy PastEvents = PastEventPolicy.ShowAgo, bool SortByDate = true, bool Notify = true)
{
    public static CountdownSettings Default { get; } = new([]);
}

public interface ICountdownRepository
{
    CountdownSettings Get();
    void Save(CountdownSettings settings);
}
