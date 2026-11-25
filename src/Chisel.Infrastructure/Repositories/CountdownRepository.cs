using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chisel.Core.Countdown;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

// Saves the countdown as one JSON document with a format version. Reading is forgiving: JSON that does not parse gives the
// defaults, an event without a name or a valid date is skipped, an unknown value falls back to its default, fields a newer
// version added are ignored, and the list is cut to the event limit. Bad data is never written back on its own.
public class CountdownRepository : ICountdownRepository
{
    public const int CurrentVersion = 1;
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    private readonly ChiselDbContext _context;

    public CountdownRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public CountdownSettings Get()
    {
        if (_context.CountdownSettings.FirstOrDefault() is not { } row)
        {
            return CountdownSettings.Default;
        }
        try
        {
            return JsonNode.Parse(row.Json) is JsonObject root ? Read(root) : CountdownSettings.Default;
        }
        catch (JsonException)
        {
            return CountdownSettings.Default;
        }
    }

    public void Save(CountdownSettings settings)
    {
        var root = new JsonObject
        {
            ["pastEvents"] = settings.PastEvents.ToString(),
            ["sortByDate"] = settings.SortByDate,
            ["notify"] = settings.Notify,
            ["events"] = new JsonArray(settings.Events.Select(Write).ToArray<JsonNode?>()),
        };
        _context.CountdownSettings.RemoveRange(_context.CountdownSettings);
        _context.CountdownSettings.Add(new CountdownSettingsEntity { Version = CurrentVersion, Json = root.ToJsonString() });
        _context.SaveChanges();
    }

    private static JsonObject Write(CountdownEntry e)
    {
        var o = new JsonObject
        {
            ["id"] = e.Id.ToString(),
            ["name"] = e.Name,
            ["date"] = e.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            ["repeat"] = e.Repeat.ToString(),
            ["color"] = e.Color.ToString(),
            ["created"] = e.Created.ToString("o", CultureInfo.InvariantCulture),
            ["archived"] = e.Archived,
        };
        if (e.Time is { } time)
        {
            o["time"] = time.ToString(TimeFormat, CultureInfo.InvariantCulture);
        }
        if (!string.IsNullOrEmpty(e.Emoji))
        {
            o["emoji"] = e.Emoji;
        }
        if (e.NotifiedOn is { } notified)
        {
            o["notifiedOn"] = notified.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
        return o;
    }

    private static CountdownSettings Read(JsonObject root)
    {
        var events = new List<CountdownEntry>();
        if (root["events"] is JsonArray array)
        {
            foreach (var node in array)
            {
                if (events.Count < CountdownValidation.MaxEvents && node is JsonObject o && ReadEvent(o) is { } e)
                {
                    events.Add(e);
                }
            }
        }
        return new CountdownSettings(events,
            Enum<PastEventPolicy>(root, "pastEvents") ?? PastEventPolicy.ShowAgo,
            Bool(root, "sortByDate") ?? true,
            Bool(root, "notify") ?? true);
    }

    private static CountdownEntry? ReadEvent(JsonObject o)
    {
        var name = Text(o, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || Date(o, "date") is not { } date)
        {
            return null;
        }
        if (name.Length > CountdownValidation.MaxNameLength)
        {
            name = name[..CountdownValidation.MaxNameLength];
        }
        return new CountdownEntry(
            Guid.TryParse(Text(o, "id"), out var id) && id != Guid.Empty ? id : Guid.NewGuid(),
            name,
            date,
            TimeOnly.TryParseExact(Text(o, "time"), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null,
            Enum<CountdownRepeat>(o, "repeat") ?? CountdownRepeat.None,
            string.IsNullOrWhiteSpace(Text(o, "emoji")) ? null : Text(o, "emoji"),
            Enum<CountdownColor>(o, "color") ?? CountdownColor.None,
            DateTimeOffset.TryParse(Text(o, "created"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var created) ? created : default,
            Bool(o, "archived") ?? false,
            Date(o, "notifiedOn"));
    }

    private static string? Text(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? Bool(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static DateOnly? Date(JsonObject o, string key) =>
        DateOnly.TryParseExact(Text(o, key), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static T? Enum<T>(JsonObject o, string key) where T : struct, Enum =>
        System.Enum.TryParse<T>(Text(o, key), ignoreCase: true, out var value) && System.Enum.IsDefined(value) ? value : null;
}
