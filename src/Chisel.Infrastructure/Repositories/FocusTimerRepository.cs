using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chisel.Core.Focus;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

// Saves the focus timer as one JSON document with a format version. Reading is forgiving: JSON that does not parse gives the
// defaults, a missing or unknown value falls back to its default, numbers out of range are clamped, and a running state without
// its start time is read as idle. Bad data is never written back on its own.
public class FocusTimerRepository : IFocusTimerRepository
{
    public const int CurrentVersion = 1;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ChiselDbContext _context;

    public FocusTimerRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public FocusTimerData Get()
    {
        if (_context.FocusTimer.FirstOrDefault() is not { } row)
        {
            return FocusTimerData.Default;
        }
        try
        {
            return JsonNode.Parse(row.Json) is JsonObject root ? Read(root) : FocusTimerData.Default;
        }
        catch (JsonException)
        {
            return FocusTimerData.Default;
        }
    }

    public void Save(FocusTimerData data)
    {
        var (s, t) = (data.Settings, data.State);
        var settings = new JsonObject
        {
            ["focus"] = s.FocusMinutes,
            ["shortBreak"] = s.ShortBreakMinutes,
            ["longBreak"] = s.LongBreakMinutes,
            ["sessionsBeforeLong"] = s.SessionsBeforeLongBreak,
            ["autoStart"] = s.AutoStartNext,
            ["volume"] = s.ChimeVolume,
            ["face"] = s.Face.ToString(),
        };
        if (s.TaskId is { } task)
        {
            settings["taskId"] = task.ToString();
        }
        var state = new JsonObject
        {
            ["phase"] = t.Phase.ToString(),
            ["run"] = t.Run.ToString(),
            ["elapsedMs"] = t.Elapsed.TotalMilliseconds,
            ["doneInCycle"] = t.FocusDoneInCycle,
            ["sessions"] = t.SessionsToday,
            ["sessionsDay"] = t.SessionsDay.ToString(DateFormat, CultureInfo.InvariantCulture),
        };
        if (t.RunStartedUtc is { } started)
        {
            state["runStarted"] = started.ToString("o", CultureInfo.InvariantCulture);
        }
        var root = new JsonObject { ["settings"] = settings, ["state"] = state };
        _context.FocusTimer.RemoveRange(_context.FocusTimer);
        _context.FocusTimer.Add(new FocusTimerEntity { Version = CurrentVersion, Json = root.ToJsonString() });
        _context.SaveChanges();
    }

    private static FocusTimerData Read(JsonObject root)
    {
        var d = FocusSettings.Default;
        var s = root["settings"] as JsonObject ?? new JsonObject();
        var settings = new FocusSettings(
            Int(s, "focus") ?? d.FocusMinutes,
            Int(s, "shortBreak") ?? d.ShortBreakMinutes,
            Int(s, "longBreak") ?? d.LongBreakMinutes,
            Int(s, "sessionsBeforeLong") ?? d.SessionsBeforeLongBreak,
            Bool(s, "autoStart") ?? d.AutoStartNext,
            Int(s, "volume") ?? d.ChimeVolume,
            Enum<FocusFace>(s, "face") ?? d.Face,
            Guid.TryParse(Text(s, "taskId"), out var task) && task != Guid.Empty ? task : null).Clamped();

        var t = root["state"] as JsonObject ?? new JsonObject();
        var started = DateTimeOffset.TryParse(Text(t, "runStarted"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : (DateTimeOffset?)null;
        var run = Enum<FocusRun>(t, "run") ?? FocusRun.Idle;
        if (run == FocusRun.Running && started is null)
        {
            run = FocusRun.Idle;
        }
        var elapsed = TimeSpan.FromMilliseconds(Math.Max(0, Double(t, "elapsedMs") ?? 0));
        var state = new FocusTimerState(
            Enum<FocusPhase>(t, "phase") ?? FocusPhase.Focus,
            run,
            run == FocusRun.Idle ? TimeSpan.Zero : elapsed,
            run == FocusRun.Running ? started : null,
            Math.Max(0, Int(t, "doneInCycle") ?? 0),
            Math.Max(0, Int(t, "sessions") ?? 0),
            DateOnly.TryParseExact(Text(t, "sessionsDay"), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : default);
        return new FocusTimerData(settings, state);
    }

    private static string? Text(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? Bool(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static int? Int(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static double? Double(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : null;

    private static T? Enum<T>(JsonObject o, string key) where T : struct, Enum =>
        System.Enum.TryParse<T>(Text(o, key), ignoreCase: true, out var value) && System.Enum.IsDefined(value) ? value : null;
}
