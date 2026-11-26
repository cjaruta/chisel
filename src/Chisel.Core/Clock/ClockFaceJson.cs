using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chisel.Core.Clock;

// The saved faces as a versioned JSON document: { "version": 1, "faces": [ { "id", "name", "style", "slots" } ] }. Reading is
// forgiving, so a damaged or hand-edited file never stops the app: text that is not a face file gives no faces, a face that
// cannot be understood is skipped (the others stay), out-of-range values are clamped, colors that are not #RRGGBB fall back to the
// theme color, and repeated ids or names are made unique. A file written by a newer version is read as far as it is understood.
public static class ClockFaceJson
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static string Serialize(IEnumerable<CustomClockFace> faces) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["version"] = CurrentVersion, ["faces"] = faces.Select(Dto).ToList() }, Options);

    // One face as its own small file (to share it).
    public static string SerializeOne(CustomClockFace face) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["version"] = CurrentVersion, ["face"] = Dto(face) }, Options);

    public static IReadOnlyList<CustomClockFace> Parse(string? json)
    {
        var faces = new List<CustomClockFace>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return faces;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("faces", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return faces;
            }
            var ids = new HashSet<Guid>();
            var names = new HashSet<string>(ClockPresets.All.Select(p => p.Title), StringComparer.OrdinalIgnoreCase);
            foreach (var element in list.EnumerateArray())
            {
                if (faces.Count >= ClockFaceLibrary.MaxFaces)
                {
                    break;
                }
                if (ReadFace(element) is { } face)
                {
                    var id = ids.Add(face.Id) ? face.Id : NewId(ids);
                    faces.Add(face with { Id = id, Name = UniqueName(face.Name, names) });
                }
            }
        }
        catch (JsonException)
        {
            // Not a face file: no faces. The caller keeps the text as it is until something is saved.
        }
        return faces;
    }

    public static bool TryParseOne(string? json, out CustomClockFace? face)
    {
        face = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("face", out var one))
            {
                face = ReadFace(one);
            }
        }
        catch (JsonException)
        {
            face = null;
        }
        return face is not null;
    }

    private static object Dto(CustomClockFace face) => new Dictionary<string, object> { ["id"] = face.Id, ["name"] = face.Name, ["style"] = face.Style, ["slots"] = face.Slots };

    private static CustomClockFace? ReadFace(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        try
        {
            var name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            name = name.Length > ClockFaceLibrary.MaxNameLength ? name[..ClockFaceLibrary.MaxNameLength] : name;
            var id = element.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String && Guid.TryParse(i.GetString(), out var g) && g != Guid.Empty
                ? g
                : Guid.NewGuid();
            var style = element.TryGetProperty("style", out var s) && s.ValueKind == JsonValueKind.Object
                ? s.Deserialize<ClockStyle>(Options) ?? ClockStyle.Default
                : ClockStyle.Default;
            var slots = element.TryGetProperty("slots", out var l) && l.ValueKind == JsonValueKind.Object
                ? l.Deserialize<ClockSlotsConfig>(Options) ?? ClockSlotsConfig.Default
                : ClockSlotsConfig.Default;
            return new CustomClockFace(id, name, style.Normalized(), slots.Normalized());
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException)
        {
            return null;
        }
    }

    private static Guid NewId(HashSet<Guid> ids)
    {
        var id = Guid.NewGuid();
        while (!ids.Add(id))
        {
            id = Guid.NewGuid();
        }
        return id;
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var n = 2; !taken.Add(candidate); n++)
        {
            var suffix = $" {n}";
            candidate = (name.Length + suffix.Length > ClockFaceLibrary.MaxNameLength ? name[..(ClockFaceLibrary.MaxNameLength - suffix.Length)] : name) + suffix;
        }
        return candidate;
    }
}
