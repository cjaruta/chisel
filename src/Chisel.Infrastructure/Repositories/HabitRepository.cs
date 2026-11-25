using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

// Saves every habit as one JSON document with a format version. Each habit keeps the set of local calendar dates it was done,
// written compactly as yyyymmdd numbers in order. Nothing is ever pruned. Reading is forgiving: JSON that does not parse gives
// no habits, an entry without a name, with a repeated id or name, or past the cap is skipped, a bad date is dropped, and any
// other bad value falls back to its default. Bad data is never written back on its own.
public class HabitRepository : IHabitRepository
{
    public const int CurrentVersion = 1;
    private const int MinYear = 1900;
    private const int MaxYear = 2199;

    private readonly ChiselDbContext _context;

    public HabitRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public HabitData Get()
    {
        if (_context.Habits.FirstOrDefault() is not { } row)
        {
            return HabitData.Empty;
        }
        try
        {
            return JsonNode.Parse(row.Json) is JsonObject { } root && root["habits"] is JsonArray list ? Read(list) : HabitData.Empty;
        }
        catch (JsonException)
        {
            return HabitData.Empty;
        }
    }

    public void Save(HabitData data)
    {
        var list = new JsonArray();
        foreach (var h in data.Habits)
        {
            var done = new JsonArray();
            foreach (var date in h.Done)
            {
                done.Add(Number(date));
            }
            var habit = new JsonObject
            {
                ["id"] = h.Id.ToString(),
                ["name"] = h.Name,
                ["days"] = (int)h.Days,
            };
            if (h.Emoji is { } emoji)
            {
                habit["emoji"] = emoji;
            }
            if (h.Color != CountdownColor.None)
            {
                habit["color"] = h.Color.ToString();
            }
            if (h.Created != default)
            {
                habit["created"] = Number(h.Created);
            }
            habit["done"] = done;
            list.Add(habit);
        }
        var root = new JsonObject { ["habits"] = list };
        _context.Habits.RemoveRange(_context.Habits);
        _context.Habits.Add(new HabitsEntity { Version = CurrentVersion, Json = root.ToJsonString() });
        _context.SaveChanges();
    }

    // Reads saved habit JSON the same forgiving way Get does (used to merge an imported backup).
    public static HabitData Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject { } root && root["habits"] is JsonArray list ? Read(list) : HabitData.Empty;
        }
        catch (JsonException)
        {
            return HabitData.Empty;
        }
    }

    private static HabitData Read(JsonArray list)
    {
        var habits = new List<Habit>();
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var node in list)
        {
            if (habits.Count >= HabitValidation.MaxHabits)
            {
                break;
            }
            if (node is not JsonObject o || Text(o, "name")?.Trim() is not { Length: > 0 } name)
            {
                continue;
            }
            if (name.Length > HabitValidation.MaxNameLength)
            {
                name = name[..HabitValidation.MaxNameLength].Trim();
            }
            var id = Guid.TryParse(Text(o, "id"), out var parsed) && parsed != Guid.Empty ? parsed : Guid.NewGuid();
            if (!ids.Add(id) || !names.Add(name))
            {
                continue;
            }
            var emoji = Text(o, "emoji")?.Trim();
            if (emoji is { Length: 0 } || (emoji is not null && new StringInfo(emoji).LengthInTextElements > 1))
            {
                emoji = null;
            }
            var color = Enum.TryParse<CountdownColor>(Text(o, "color"), ignoreCase: true, out var c) && Enum.IsDefined(c) ? c : CountdownColor.None;
            var days = Int(o["days"]) is { } d && d is > 0 and <= (int)HabitDays.Every ? (HabitDays)d : HabitDays.Every;
            var done = ImmutableSortedSet.CreateBuilder<DateOnly>();
            if (o["done"] is JsonArray dates)
            {
                foreach (var date in dates)
                {
                    if (Date(date) is { } day)
                    {
                        done.Add(day);
                    }
                }
            }
            habits.Add(new Habit(id, name, emoji, color, days, done.ToImmutable(), Date(o["created"]) ?? default));
        }
        return new HabitData(habits);
    }

    private static int Number(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

    private static DateOnly? Date(JsonNode? node)
    {
        if (Int(node) is not { } n)
        {
            return null;
        }
        var (year, month, day) = (n / 10000, n / 100 % 100, n % 100);
        return year is >= MinYear and <= MaxYear && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    private static string? Text(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int? Int(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
}
