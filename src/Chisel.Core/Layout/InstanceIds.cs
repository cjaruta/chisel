namespace Chisel.Core.Layout;

// Widget ids. A single-instance widget's id is its type id ("clock"). An instance of a widget type that allows
// several is "<typeId>:<key>" ("frame:7k2m9q"). Everything that belongs to one widget on the dashboard (its card,
// view, header choice, tab) is keyed by this instance id, never by the type id.
public static class InstanceIds
{
    public const char Separator = ':';

    // Type and key of an id; the key is null for a plain type id. Throws for a malformed id (see TryParse).
    public static (string TypeId, string? Key) Parse(string id) =>
        TryParse(id) ?? throw new ArgumentException($"'{id}' is not a widget id.", nameof(id));

    // Null for a malformed id: empty, an empty type or key, or more than one separator.
    public static (string TypeId, string? Key)? TryParse(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        var at = id.IndexOf(Separator);
        if (at < 0)
        {
            return (id, null);
        }
        var key = id[(at + 1)..];
        return at == 0 || key.Length == 0 || key.Contains(Separator) ? null : (id[..at], key);
    }

    // The type an id belongs to (the id itself when it has no key).
    public static string TypeOf(string id) => TryParse(id)?.TypeId ?? id;

    public static string Create(string typeId, string key) => $"{typeId}{Separator}{key}";

    // Whether the id is one instance of a multi-instance type (it has a key). Such an instance is deleted, not hidden, when removed.
    public static bool IsInstance(string id) => TryParse(id) is { Key: not null };

    // A new instance id of a type that is not in taken. keyFactory makes the keys (a short random one by default);
    // a key that gives a taken id is tried again.
    public static string NewInstanceId(string typeId, IEnumerable<string> taken, Func<string>? keyFactory = null)
    {
        var used = taken.ToHashSet();
        keyFactory ??= () => Guid.NewGuid().ToString("N")[..6];
        while (true)
        {
            var id = Create(typeId, keyFactory());
            if (!used.Contains(id))
            {
                return id;
            }
        }
    }
}

// Which widget types exist and which allow several instances. Ids not in the types are unknown.
public sealed class WidgetTypes
{
    private readonly HashSet<string> _single;
    private readonly HashSet<string> _multiple;

    public WidgetTypes(IEnumerable<string> singleInstance, IEnumerable<string> multiInstance)
    {
        _single = singleInstance.ToHashSet();
        _multiple = multiInstance.ToHashSet();
    }

    public bool AllowsMultiple(string typeId) => _multiple.Contains(typeId);

    public bool Knows(string typeId) => _single.Contains(typeId) || _multiple.Contains(typeId);

    // Whether an id names a widget that can be on the dashboard: a single-instance type's own id, or an instance
    // of a multi-instance type (with or without a key).
    public bool IsValidId(string id) =>
        InstanceIds.TryParse(id) is { } parsed
        && (parsed.Key is null ? Knows(parsed.TypeId) : AllowsMultiple(parsed.TypeId));
}

// What each widget on the dashboard is called. A type with more than one instance on the dashboard numbers them
// in dashboard order: "Frame", "Frame 2", "Frame 3"; a lone instance has no number.
public static class WidgetTitles
{
    public static IReadOnlyDictionary<string, string> For(IEnumerable<string> dashboardIds, Func<string, string> baseTitle)
    {
        var titles = new Dictionary<string, string>();
        var counts = new Dictionary<string, int>();
        var ids = dashboardIds.ToList();
        foreach (var id in ids)
        {
            var type = InstanceIds.TypeOf(id);
            var number = counts[type] = counts.GetValueOrDefault(type) + 1;
            titles[id] = number == 1 ? baseTitle(id) : $"{baseTitle(id)} {number}";
        }
        return titles;
    }
}
