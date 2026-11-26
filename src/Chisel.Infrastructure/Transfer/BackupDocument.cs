using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;

namespace Chisel.Infrastructure.Transfer;

public enum ImportMode
{
    // Everything in the app becomes what the backup holds.
    Replace,
    // The backup's tasks, notes pads, quick links, alarms, habits, countdown events, clock faces and layout profiles (into
    // free slots) are added; the dashboard, settings, schedule and pictures stay as they are.
    Merge,
}

// What a backup holds, for the confirmation before importing.
public sealed record BackupSummary(int Widgets, int NotesPads, int Tasks, int QuickLinks, int Alarms, int Habits,
    int CountdownEvents, int ClockFaces, int LayoutProfiles, int Pictures)
{
    public override string ToString()
    {
        var parts = new List<string> { Count(Widgets, "widget") };
        void Add(int n, string one, string? many = null)
        {
            if (n > 0)
            {
                parts.Add(Count(n, one, many));
            }
        }
        Add(NotesPads, "notes pad");
        Add(Tasks, "task");
        Add(QuickLinks, "quick link");
        Add(Alarms, "alarm");
        Add(Habits, "habit");
        Add(CountdownEvents, "countdown event");
        Add(ClockFaces, "clock face");
        Add(LayoutProfiles, "layout profile");
        Add(Pictures, "picture");
        return string.Join(", ", parts);
    }

    private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";
}

// A backup file that passed every check, ready to apply. Rows are entity objects, one list per table.
public sealed class BackupPackage
{
    internal BackupPackage(int formatVersion, string appVersion, DateTimeOffset exportedAt, IReadOnlyDictionary<string, IList> tables,
        BackupSummary summary, IReadOnlyList<string> warnings)
    {
        FormatVersion = formatVersion;
        AppVersion = appVersion;
        ExportedAt = exportedAt;
        Tables = tables;
        Summary = summary;
        Warnings = warnings;
    }

    public int FormatVersion { get; }
    public string AppVersion { get; }
    public DateTimeOffset ExportedAt { get; }
    internal IReadOnlyDictionary<string, IList> Tables { get; }
    public BackupSummary Summary { get; }
    // Things to know before importing (pictures or linked files that may be missing on this computer).
    public IReadOnlyList<string> Warnings { get; }

    // Merge only adds lists of things; a backup without any has nothing to merge.
    public bool CanMerge =>
        new[] { Summary.Tasks, Summary.NotesPads, Summary.QuickLinks, Summary.Alarms, Summary.Habits, Summary.CountdownEvents, Summary.ClockFaces, Summary.LayoutProfiles }
            .Any(n => n > 0);

    internal IList Rows(string table) => Tables.TryGetValue(table, out var rows) ? rows : Array.Empty<object>();
}

public sealed record BackupReadResult(BackupPackage? Package, string? Error)
{
    public static BackupReadResult Fail(string error) => new(null, error);
}

// The backup file: one JSON document with a header (format version, app version, export date), the rows of every table, and
// the list of rows that point at files on the computer they came from.
public static class BackupDocument
{
    public const string AppName = "Chisel";
    public const int FormatVersion = 1;
    public const long MaxBytes = 20 * 1024 * 1024;

    // Older format versions and the step that brings each one up to the next. Format 1 is the first, so there are none yet:
    // when the format changes, bump FormatVersion and add the step from the old number here.
    private static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Upgrades = new Dictionary<int, Func<JsonObject, JsonObject>>();
    private const int OldestFormat = 1;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Export(ChiselDbContext context, string appVersion, DateTimeOffset now)
    {
        var data = new JsonObject();
        foreach (var table in BackupTables.All)
        {
            data[table.Name] = table.Export(context, Json);
        }
        var root = new JsonObject
        {
            ["app"] = AppName,
            ["formatVersion"] = FormatVersion,
            ["appVersion"] = appVersion,
            ["exportedAt"] = now.ToString("o"),
            ["machineBound"] = MachineBound(data),
            ["data"] = data,
        };
        return root.ToJsonString(Json);
    }

    // Rows that only make sense on the computer they came from: picture files (kept in the app's own folder, not in the
    // backup) and quick links to local files or folders. They are imported as they are, with a warning.
    private static JsonArray MachineBound(JsonObject data)
    {
        var list = new JsonArray();
        foreach (var row in data[BackupTables.PictureAssets]!.AsArray())
        {
            list.Add(new JsonObject { ["table"] = BackupTables.PictureAssets, ["id"] = row!["id"]!.DeepClone(), ["kind"] = "pictureFile" });
        }
        foreach (var row in data[BackupTables.QuickLinks]!.AsArray())
        {
            if (BackupPaths.IsLocalPath(row!["url"]?.GetValue<string>()))
            {
                list.Add(new JsonObject { ["table"] = BackupTables.QuickLinks, ["id"] = row["id"]!.DeepClone(), ["kind"] = "localPath" });
            }
        }
        return list;
    }

    // Checks a file completely before anything is changed: size, that it is a Chisel backup and not something else, its
    // format version (older ones are upgraded, newer ones refused), and the shape of every row.
    // pictureExists: whether a stored picture file name is in this computer's pictures folder.
    public static BackupReadResult Read(byte[] bytes, Func<string, bool>? pictureExists = null)
    {
        if (bytes.LongLength > MaxBytes)
        {
            return BackupReadResult.Fail($"This file is too large to be a Chisel backup (the limit is {MaxBytes / (1024 * 1024)} MB).");
        }
        if (!LooksLikeJson(bytes))
        {
            return BackupReadResult.Fail("This file isn't a Chisel backup. Choose a .json file made with Export.");
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return BackupReadResult.Fail("This backup is damaged and can't be read.");
        }

        if (Text(root, "app") != AppName || root["formatVersion"] is not JsonValue versionNode || !versionNode.TryGetValue<int>(out var version))
        {
            return BackupReadResult.Fail("This file isn't a Chisel backup. Choose a .json file made with Export.");
        }
        if (version > FormatVersion)
        {
            return BackupReadResult.Fail($"This backup was made by a newer version of Chisel ({Text(root, "appVersion") ?? "unknown"}). Update Chisel, then import it again.");
        }
        if (version < OldestFormat)
        {
            return BackupReadResult.Fail("This backup's format isn't recognized.");
        }
        for (var v = version; v < FormatVersion; v++)
        {
            root = Upgrades[v](root);
        }

        if (root["data"] is not JsonObject data)
        {
            return BackupReadResult.Fail("This backup is damaged: it has no data.");
        }

        var tables = new Dictionary<string, IList>();
        try
        {
            foreach (var (name, node) in data)
            {
                var table = BackupTables.Find(name) ?? throw new JsonException($"unknown section \"{name}\"");
                if (node is not JsonArray rows)
                {
                    throw new JsonException($"{name} is not a list");
                }
                var read = table.Read(rows, Json);
                if (table.Singleton && read.Count > 1)
                {
                    throw new JsonException($"{name} has more than one row");
                }
                if (name == BackupTables.PictureAssets)
                {
                    CheckPictures(read);
                }
                tables[name] = read;
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or NotSupportedException)
        {
            return BackupReadResult.Fail("This backup is damaged and can't be read.");
        }

        // Quick links follow the app's own rules: programs, scripts and other kinds of address are left out, as when adding one.
        var refused = 0;
        if (tables.TryGetValue(BackupTables.QuickLinks, out var links))
        {
            var kept = links.Cast<QuickLinkEntity>().Where(l => Core.Links.QuickLinkTarget.CheckShape(l.Url) is null).ToList();
            refused = links.Count - kept.Count;
            tables[BackupTables.QuickLinks] = kept;
        }

        var exportedAt = DateTimeOffset.TryParse(Text(root, "exportedAt"), out var at) ? at : default;
        var warnings = Warn(tables, pictureExists);
        if (refused > 0)
        {
            warnings.Add($"{refused} quick links were left out: programs, scripts and addresses other than http and https can't be quick links.");
        }
        return new BackupReadResult(
            new BackupPackage(version, Text(root, "appVersion") ?? "", exportedAt, tables, Summarize(tables), warnings),
            null);
    }

    // The picture file's name in the pictures folder, as the app derives it.
    public static string StoredName(PictureAssetEntity e) =>
        new Core.Pictures.PictureAsset(e.Id, e.Hash, e.FileName, e.MimeType, e.Width, e.Height, e.Bytes, e.SourceUrl, e.AddedAt).StoredName;

    // A picture's file name is made from its hash and extension, and the app reads and deletes files by that name: anything
    // but a plain hash and a plain file name could reach outside the pictures folder.
    private static void CheckPictures(IList rows)
    {
        foreach (var e in rows.Cast<PictureAssetEntity>())
        {
            var hashOk = e.Hash.Length == 64 && e.Hash.All(char.IsAsciiHexDigit);
            var nameOk = e.FileName.Length is > 0 and <= 255 && e.FileName.IndexOfAny(['/', '\\', ':']) < 0 && !e.FileName.Contains("..");
            if (!hashOk || !nameOk || StoredName(e) != Path.GetFileName(StoredName(e)))
            {
                throw new JsonException("a picture has an unsafe file name");
            }
        }
    }

    private static bool LooksLikeJson(byte[] bytes)
    {
        var start = bytes.AsSpan();
        if (start.StartsWith(Encoding.UTF8.Preamble))
        {
            start = start[Encoding.UTF8.Preamble.Length..];
        }
        foreach (var b in start)
        {
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }
            return b == (byte)'{';
        }
        return false;
    }

    private static string? Text(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static T? Single<T>(IReadOnlyDictionary<string, IList> tables, string name) where T : class =>
        tables.TryGetValue(name, out var rows) ? rows.Cast<T>().FirstOrDefault() : null;

    private static int Rows(IReadOnlyDictionary<string, IList> tables, string name) => tables.TryGetValue(name, out var rows) ? rows.Count : 0;

    private static BackupSummary Summarize(IReadOnlyDictionary<string, IList> tables) => new(
        Rows(tables, BackupTables.WidgetPlacements),
        Rows(tables, BackupTables.Notes),
        Rows(tables, BackupTables.Tasks),
        Rows(tables, BackupTables.QuickLinks),
        Rows(tables, BackupTables.Alarms),
        Single<HabitsEntity>(tables, BackupTables.Habits) is { } h ? HabitRepository.Parse(h.Json).Habits.Count : 0,
        Single<CountdownSettingsEntity>(tables, BackupTables.Countdown) is { } c ? CountdownRepository.Parse(c.Json).Events.Count : 0,
        Single<ClockFaceLibraryEntity>(tables, BackupTables.ClockFaces) is { } f ? ClockFaceJson.Parse(f.Json).Count : 0,
        Rows(tables, BackupTables.LayoutProfiles),
        Rows(tables, BackupTables.PictureAssets));

    private static List<string> Warn(IReadOnlyDictionary<string, IList> tables, Func<string, bool>? pictureExists)
    {
        var warnings = new List<string>();
        if (tables.TryGetValue(BackupTables.PictureAssets, out var assets) && assets.Count > 0)
        {
            var pictures = assets.Cast<PictureAssetEntity>().ToList();
            var missing = pictureExists is null ? pictures.Count : pictures.Count(p => !pictureExists(StoredName(p)));
            if (missing > 0)
            {
                warnings.Add($"{missing} of {pictures.Count} pictures aren't on this computer; their frames will stay empty until you add the pictures again.");
            }
        }
        if (tables.TryGetValue(BackupTables.QuickLinks, out var links))
        {
            var local = links.Cast<QuickLinkEntity>().Where(l => BackupPaths.IsLocalPath(l.Url)).ToList();
            if (local.Count > 0)
            {
                warnings.Add($"{local.Count} quick links open files or folders that may not exist on this computer.");
            }
        }
        return warnings;
    }
}
