namespace Chisel.Core.Clock;

// A clock configuration the user saved under a name: how the faces look (the style: dial, markers, numerals, hands, colors,
// seconds, alignment) and what the widget's slots show (the layout). The built-in looks are ClockPresets and cannot be changed.
public sealed record CustomClockFace(Guid Id, string Name, ClockStyle Style, ClockSlotsConfig Slots);

public sealed record ClockFaceResult(CustomClockFace? Face, string? Error)
{
    public bool IsOk => Error is null;

    public static ClockFaceResult Ok(CustomClockFace? face = null) => new(face, null);

    public static ClockFaceResult Fail(string error) => new(null, error);
}

// Where the saved faces are kept (replace-all: the whole list is written each time).
public interface IClockFaceStore
{
    IReadOnlyList<CustomClockFace> Load();
    void Save(IReadOnlyList<CustomClockFace> faces);
}

// The user's saved faces and what can be done with them: save as new, rename, update, delete, duplicate. A name has to be there,
// short enough, and different (ignoring case) from every other saved face and from every built-in look.
public sealed class ClockFaceLibrary
{
    public const int MaxNameLength = 40;
    public const int MaxFaces = 50;

    private readonly List<CustomClockFace> _faces;

    public ClockFaceLibrary(IEnumerable<CustomClockFace>? faces = null) => _faces = faces?.ToList() ?? [];

    public IReadOnlyList<CustomClockFace> Faces => _faces;

    public CustomClockFace? Find(Guid id) => _faces.FirstOrDefault(f => f.Id == id);

    // Why a name cannot be used (for the face with id `except`, which may keep its own), or null when it can.
    public string? CheckName(string? name, Guid? except = null)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "Give the face a name.";
        }
        if (trimmed.Length > MaxNameLength)
        {
            return $"Use a name of {MaxNameLength} characters or fewer.";
        }
        if (ClockPresets.All.Any(p => Same(p.Title, trimmed)))
        {
            return "That name belongs to a built-in face. Choose another.";
        }
        if (_faces.Any(f => f.Id != except && Same(f.Name, trimmed)))
        {
            return "You already have a face with that name.";
        }
        return null;
    }

    public ClockFaceResult SaveAsNew(string? name, ClockStyle style, ClockSlotsConfig slots)
    {
        if (_faces.Count >= MaxFaces)
        {
            return ClockFaceResult.Fail($"You can keep up to {MaxFaces} faces. Delete one first.");
        }
        if (CheckName(name) is { } problem)
        {
            return ClockFaceResult.Fail(problem);
        }
        var face = new CustomClockFace(Guid.NewGuid(), name!.Trim(), style.Normalized(), slots.Normalized());
        _faces.Add(face);
        return ClockFaceResult.Ok(face);
    }

    public ClockFaceResult Rename(Guid id, string? name)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return Missing();
        }
        if (CheckName(name, id) is { } problem)
        {
            return ClockFaceResult.Fail(problem);
        }
        _faces[index] = _faces[index] with { Name = name!.Trim() };
        return ClockFaceResult.Ok(_faces[index]);
    }

    // Replaces a saved face's look and layout with the given ones; its name and place stay.
    public ClockFaceResult Update(Guid id, ClockStyle style, ClockSlotsConfig slots)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return Missing();
        }
        _faces[index] = _faces[index] with { Style = style.Normalized(), Slots = slots.Normalized() };
        return ClockFaceResult.Ok(_faces[index]);
    }

    public ClockFaceResult Delete(Guid id)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return Missing();
        }
        _faces.RemoveAt(index);
        return ClockFaceResult.Ok();
    }

    // A copy named "<name> copy" (then "<name> copy 2", and so on).
    public ClockFaceResult Duplicate(Guid id)
    {
        var source = Find(id);
        if (source is null)
        {
            return Missing();
        }
        var name = $"{source.Name} copy";
        for (var n = 2; CheckName(name) is not null && n < 1000; n++)
        {
            name = $"{source.Name} copy {n}";
        }
        // A long name plus the suffix may be too long: trim the original part.
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength];
        }
        return SaveAsNew(name, source.Style, source.Slots);
    }

    private int IndexOf(Guid id) => _faces.FindIndex(f => f.Id == id);

    private static ClockFaceResult Missing() => ClockFaceResult.Fail("That face is no longer there.");

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
