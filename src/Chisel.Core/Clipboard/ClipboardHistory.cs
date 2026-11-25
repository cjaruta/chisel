using System.Text;

namespace Chisel.Core.Clipboard;

// One recorded copy. Preview is the text as one trimmed line, cut to PreviewLength with an ellipsis: the only part of the text the
// widget shows or reads out. ToString leaves the text out so it can't reach a log, an exception or test output.
public sealed record ClipboardEntry(Guid Id, string Text, DateTimeOffset CopiedAt, bool Pinned)
{
    public const int PreviewLength = 120;

    public string Preview { get; } = MakePreview(Text);

    public override string ToString() => $"ClipboardEntry({Text.Length} chars{(Pinned ? ", pinned" : "")})";

    private static string MakePreview(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, PreviewLength + 1));
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = builder.Length > 0;
                continue;
            }
            if (space)
            {
                builder.Append(' ');
                space = false;
            }
            if (builder.Length >= PreviewLength)
            {
                builder.Length = PreviewLength;
                return builder.Append('…').ToString();
            }
            builder.Append(c);
        }
        return builder.ToString();
    }
}

// The recorded copies, in memory: pinned ones first, then the rest, each newest first. Copying something already here moves it to
// the top instead of storing it again. At most MaxItems are kept: the oldest unpinned ones go first, and pinned ones never do.
public sealed class ClipboardHistory
{
    private readonly List<ClipboardEntry> _items = new();
    private int _maxItems;

    public ClipboardHistory(int maxItems)
    {
        _maxItems = Math.Max(1, maxItems);
    }

    public IReadOnlyList<ClipboardEntry> Items => _items;

    public int MaxItems
    {
        get => _maxItems;
        set
        {
            _maxItems = Math.Max(1, value);
            Trim();
        }
    }

    public ClipboardEntry Add(string text, DateTimeOffset now)
    {
        var index = _items.FindIndex(i => string.Equals(i.Text, text, StringComparison.Ordinal));
        ClipboardEntry entry;
        if (index >= 0)
        {
            entry = _items[index] with { CopiedAt = now };
            _items.RemoveAt(index);
        }
        else
        {
            entry = new ClipboardEntry(Guid.NewGuid(), text, now, false);
        }
        Insert(entry);
        Trim();
        return entry;
    }

    public bool Remove(Guid id) => _items.RemoveAll(i => i.Id == id) > 0;

    public void Clear() => _items.Clear();

    public void SetPinned(Guid id, bool pinned)
    {
        var index = _items.FindIndex(i => i.Id == id);
        if (index < 0 || _items[index].Pinned == pinned)
        {
            return;
        }
        var entry = _items[index] with { Pinned = pinned };
        _items.RemoveAt(index);
        Insert(entry);
    }

    // Puts saved entries back (newest first within each group, as saved), then trims to the cap.
    public void Load(IEnumerable<ClipboardEntry> entries)
    {
        _items.Clear();
        _items.AddRange(entries.Where(e => e.Pinned));
        _items.AddRange(entries.Where(e => !e.Pinned));
        Trim();
    }

    // The entries whose text contains the query, ignoring case, in list order. A blank query matches everything.
    public IEnumerable<ClipboardEntry> Search(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? _items
            : _items.Where(i => i.Text.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase));

    // A pinned entry goes first; an unpinned one goes among the unpinned by its time (a new copy is the newest, so it goes first).
    private void Insert(ClipboardEntry entry)
    {
        if (entry.Pinned)
        {
            _items.Insert(0, entry);
            return;
        }
        var index = _items.FindIndex(i => !i.Pinned && i.CopiedAt <= entry.CopiedAt);
        _items.Insert(index < 0 ? _items.Count : index, entry);
    }

    private void Trim()
    {
        for (var i = _items.Count - 1; i >= 0 && _items.Count > _maxItems; i--)
        {
            if (!_items[i].Pinned)
            {
                _items.RemoveAt(i);
            }
        }
    }
}

// "just now", "5 min ago", "3 h ago", "2 d ago".
public static class RelativeTime
{
    public static string Format(TimeSpan age) =>
        age < TimeSpan.FromMinutes(1) ? "just now"
        : age < TimeSpan.FromHours(1) ? $"{(int)age.TotalMinutes} min ago"
        : age < TimeSpan.FromDays(1) ? $"{(int)age.TotalHours} h ago"
        : $"{(int)age.TotalDays} d ago";
}

// Where saved history goes when "Keep history between sessions" is on. Never used otherwise.
public interface IClipboardHistoryStore
{
    // A short description of where the history is kept, for the widget to show.
    string Location { get; }

    IReadOnlyList<ClipboardEntry> Load();
    void Save(IReadOnlyList<ClipboardEntry> entries);
    void Delete();
}
