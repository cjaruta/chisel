using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chisel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Chisel.Infrastructure.Transfer;

// One database table in a backup: how it is written out, read back and replaced. Rows are written as the entity's own
// properties (camelCase), so a table that gains a column reads older files with that column's default.
internal abstract class BackupTable
{
    protected BackupTable(string name, bool singleton)
    {
        Name = name;
        Singleton = singleton;
    }

    public string Name { get; }

    // At most one row (a settings record or a JSON document).
    public bool Singleton { get; }

    public abstract JsonArray Export(ChiselDbContext context, JsonSerializerOptions options);

    // Throws JsonException when a row has the wrong shape.
    public abstract IList Read(JsonArray rows, JsonSerializerOptions options);

    public abstract void RemoveAll(ChiselDbContext context);

    public abstract void AddAll(ChiselDbContext context, IList rows);
}

internal sealed class BackupTable<T>(string name, Func<ChiselDbContext, DbSet<T>> set, bool singleton = false, Action<T>? scrub = null)
    : BackupTable(name, singleton) where T : class
{
    public DbSet<T> Set(ChiselDbContext context) => set(context);

    public override JsonArray Export(ChiselDbContext context, JsonSerializerOptions options)
    {
        var key = context.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties[0].PropertyInfo!;
        var rows = set(context).AsNoTracking().AsEnumerable().OrderBy(r => Convert.ToString(key.GetValue(r), System.Globalization.CultureInfo.InvariantCulture));
        var array = new JsonArray();
        foreach (var row in rows)
        {
            scrub?.Invoke(row);
            array.Add(JsonSerializer.SerializeToNode(row, options));
        }
        return array;
    }

    public override IList Read(JsonArray rows, JsonSerializerOptions options)
    {
        var list = new List<T>();
        foreach (var row in rows)
        {
            if (row is not JsonObject)
            {
                throw new JsonException($"A row in {Name} is not an object.");
            }
            list.Add(row.Deserialize<T>(options) ?? throw new JsonException($"A row in {Name} is empty."));
        }
        return list;
    }

    public override void RemoveAll(ChiselDbContext context) => set(context).RemoveRange(set(context));

    public override void AddAll(ChiselDbContext context, IList rows) => set(context).AddRange(rows.Cast<T>());
}

internal static class BackupTables
{
    public const string Tasks = "tasks";
    public const string Notes = "notes";
    public const string QuickLinks = "quickLinks";
    public const string Alarms = "alarms";
    public const string Habits = "habits";
    public const string Countdown = "countdown";
    public const string ClockFaces = "clockFaces";
    public const string LayoutProfiles = "layoutProfiles";
    public const string WidgetPlacements = "widgetPlacements";
    public const string PictureAssets = "pictureAssets";

    // Everything a backup holds. Left out on purpose: the weather cache (fetched again) and clipboard history (private,
    // and kept in its own file).
    public static readonly IReadOnlyList<BackupTable> All =
    [
        new BackupTable<UserSettingsEntity>("userSettings", c => c.UserSettings, singleton: true),
        new BackupTable<SystemSettingsEntity>("systemSettings", c => c.SystemSettings, singleton: true),
        new BackupTable<LayoutColumnEntity>("layoutColumns", c => c.LayoutColumns),
        new BackupTable<WidgetPlacementEntity>(WidgetPlacements, c => c.WidgetPlacements),
        new BackupTable<HiddenWidgetEntity>("hiddenWidgets", c => c.HiddenWidgets),
        new BackupTable<LayoutProfileEntity>(LayoutProfiles, c => c.LayoutProfiles),
        new BackupTable<ScheduleBlockEntity>("scheduleBlocks", c => c.ScheduleBlocks),
        new BackupTable<MarkerEntity>("markers", c => c.Markers),
        new BackupTable<TaskEntity>(Tasks, c => c.Tasks),
        new BackupTable<NoteEntity>(Notes, c => c.Notes),
        new BackupTable<NotepadSettingsEntity>("notepadSettings", c => c.NotepadSettings, singleton: true),
        new BackupTable<QuickLinkEntity>(QuickLinks, c => c.QuickLinks),
        new BackupTable<AlarmEntity>(Alarms, c => c.Alarms),
        new BackupTable<HabitsEntity>(Habits, c => c.Habits, singleton: true),
        new BackupTable<CountdownSettingsEntity>(Countdown, c => c.CountdownSettings, singleton: true),
        new BackupTable<ClockFaceLibraryEntity>(ClockFaces, c => c.ClockFaceLibraries, singleton: true),
        new BackupTable<ClockStyleEntity>("clockStyle", c => c.ClockStyles, singleton: true),
        new BackupTable<ClockToolsEntity>("clockTools", c => c.ClockTools, singleton: true),
        new BackupTable<FocusTimerEntity>("focusTimer", c => c.FocusTimer, singleton: true),
        new BackupTable<CalculatorSettingsEntity>("calculatorSettings", c => c.CalculatorSettings, singleton: true),
        new BackupTable<ConverterSettingsEntity>("converterSettings", c => c.ConverterSettings, singleton: true),
        new BackupTable<CalendarSettingsEntity>("calendarSettings", c => c.CalendarSettings, singleton: true),
        new BackupTable<SystemMonitorSettingsEntity>("systemMonitorSettings", c => c.SystemMonitorSettings, singleton: true),
        new BackupTable<ClipboardHistorySettingsEntity>("clipboardSettings", c => c.ClipboardHistorySettings, singleton: true),
        new BackupTable<WeatherLocationEntity>("weatherLocation", c => c.WeatherLocations, singleton: true),
        // Where a picture came from is kept only when it is a web address: a local path says nothing useful elsewhere.
        new BackupTable<PictureAssetEntity>(PictureAssets, c => c.PictureAssets,
            scrub: a => a.SourceUrl = BackupPaths.IsLocalPath(a.SourceUrl) ? null : a.SourceUrl),
        new BackupTable<FramePictureEntity>("framePictures", c => c.FramePictures),
        new BackupTable<FrameOptionsEntity>("frameOptions", c => c.FrameOptions),
    ];

    public static BackupTable? Find(string name) => All.FirstOrDefault(t => t.Name == name);
}

public static class BackupPaths
{
    // A drive path (C:\...), a UNC share (\\server\...) or a file: URL.
    public static bool IsLocalPath(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith(@"\\", StringComparison.Ordinal) ||
         value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
         (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/'));

    private static readonly string[] ProgramExtensions = [".exe", ".bat", ".cmd", ".com", ".msi", ".ps1", ".vbs", ".js", ".scr", ".lnk"];

    // A local path that starts a program when opened.
    public static bool IsProgram(string? value) =>
        IsLocalPath(value) && ProgramExtensions.Any(e => value!.TrimEnd().EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
