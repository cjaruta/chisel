using System.Text.Json.Serialization;
using Chisel.Core.Theme;

namespace Chisel.Core.Clock;

public enum ClockAlignment { Left, Center, Right }
public enum DialShape { Round, Square, RoundedSquare }
public enum MarkerStyle { None, Ticks, Dots, Bars, Lume }
public enum NumeralStyle { None, Twelve, Four, All, Roman }
public enum HandStyle { Line, Tapered, Baton, Leaf, Sword }
// What the dial is: a 12-hour dial, a 24-hour dial (the hour hand goes round once a day), or a chronograph (12-hour with sub-dials).
public enum DialKind { Standard, TwentyFourHour, Chronograph }
// How the stopwatch tool shows the time: digits, or a round dial with a sweeping seconds hand and a minutes sub-dial.
public enum StopwatchDisplay { Digital, Analog }

// Colors per part as #RRGGBB; null means the theme's own color for that part.
public readonly record struct ClockColors(string? Dial, string? Markers, string? Numerals, string? Hour, string? Minute, string? Second)
{
    public static ClockColors Theme => default;

    public ClockColors Normalized() => new(Clean(Dial), Clean(Markers), Clean(Numerals), Clean(Hour), Clean(Minute), Clean(Second));

    private static string? Clean(string? hex) => Rgb.TryParse(hex, out var rgb) ? rgb.ToHex() : null;
}

// Everything the one clock renderer needs. Named looks are ClockPresets (data), not separate renderers.
public sealed record ClockStyle
{
    public const double MinThickness = 1;
    public const double MaxThickness = 10;
    public const double MaxBorder = 8;

    public static ClockStyle Default { get; } = new();

    public ClockAlignment Alignment { get; init; } = ClockAlignment.Center;
    public DialShape Shape { get; init; } = DialShape.Round;
    public double BorderWidth { get; init; } = 2;
    public MarkerStyle Markers { get; init; } = MarkerStyle.Ticks;
    // Longer, heavier markers at 12, 3, 6 and 9.
    public bool EmphasizeQuarters { get; init; } = true;
    public NumeralStyle Numerals { get; init; } = NumeralStyle.None;
    public HandStyle Hands { get; init; } = HandStyle.Line;
    public double HourThickness { get; init; } = 4;
    public double MinuteThickness { get; init; } = 3;
    public double SecondThickness { get; init; } = 1.5;
    public bool ShowSeconds { get; init; } = true;
    public ClockColors Colors { get; init; }
    public DialKind Kind { get; init; } = DialKind.Standard;
    // A solid dial color (#RRGGBB), or null for no fill (the card shows through).
    public string? DialFill { get; init; }
    // A ring of 60 small marks just inside the rim.
    public bool MinuteTrack { get; init; }
    // The speed scale around the outside (see ClockTachymeter).
    public bool Tachymeter { get; init; }
    public StopwatchDisplay Stopwatch { get; init; } = StopwatchDisplay.Digital;

    [JsonIgnore]
    public string? DialColor { get => Colors.Dial; init => Colors = Colors with { Dial = value }; }
    [JsonIgnore]
    public string? MarkerColor { get => Colors.Markers; init => Colors = Colors with { Markers = value }; }

    public ClockStyle Normalized() => this with
    {
        BorderWidth = Math.Clamp(BorderWidth, 0, MaxBorder),
        HourThickness = Math.Clamp(HourThickness, MinThickness, MaxThickness),
        MinuteThickness = Math.Clamp(MinuteThickness, MinThickness, MaxThickness),
        SecondThickness = Math.Clamp(SecondThickness, MinThickness, MaxThickness),
        Colors = Colors.Normalized(),
        DialFill = Rgb.TryParse(DialFill, out var fill) ? fill.ToHex() : null,
    };
}

// Colors: a face with a solid dial brings its own colors (they have to read on that dial); a face without keeps the user's.
public sealed record ClockPreset(string Id, string Title, ClockStyle Style, ClockColors? Colors = null);

public static class ClockPresets
{
    public static IReadOnlyList<ClockPreset> All { get; } =
    [
        new("minimal", "Minimal", new ClockStyle { Markers = MarkerStyle.None, EmphasizeQuarters = false, BorderWidth = 2, Hands = HandStyle.Line, HourThickness = 4, MinuteThickness = 3 }),
        new("roman", "Roman", new ClockStyle { Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.Roman, Hands = HandStyle.Leaf, HourThickness = 5, MinuteThickness = 3 }),
        new("numbers", "Numbers", new ClockStyle { Markers = MarkerStyle.None, Numerals = NumeralStyle.All, Hands = HandStyle.Baton, HourThickness = 5, MinuteThickness = 4 }),
        new("ticks", "Ticks", new ClockStyle { Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.None, Hands = HandStyle.Tapered, BorderWidth = 3 }),
        new("modern", "Modern", new ClockStyle { Shape = DialShape.RoundedSquare, Markers = MarkerStyle.Bars, Numerals = NumeralStyle.Four, Hands = HandStyle.Baton, BorderWidth = 3, HourThickness = 6, MinuteThickness = 4 }),
        new("dots", "Dots", new ClockStyle { Markers = MarkerStyle.Dots, Numerals = NumeralStyle.None, Hands = HandStyle.Line, BorderWidth = 0, HourThickness = 5, MinuteThickness = 3 }),
        // Classic white dial with Roman numerals.
        new("classic", "Classic", new ClockStyle { Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.Roman, Hands = HandStyle.Leaf, BorderWidth = 3, HourThickness = 5, MinuteThickness = 3, SecondThickness = 1.2, DialFill = "#F6F3EA" },
            new ClockColors("#2A2A2E", "#2A2A2E", "#1A1A1E", "#1A1A1E", "#1A1A1E", "#B3261E")),
        // Minimal modern dial: thin hands, no numbers, no markers.
        new("slim", "Slim", new ClockStyle { Markers = MarkerStyle.None, EmphasizeQuarters = false, Numerals = NumeralStyle.None, Hands = HandStyle.Line, BorderWidth = 1, HourThickness = 2, MinuteThickness = 1.5, SecondThickness = 1 }),
        // Dark chronograph: two sub-dials, a minute track and a tachymeter ring.
        new("chrono", "Chrono", new ClockStyle { Kind = DialKind.Chronograph, Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.None, Hands = HandStyle.Baton, BorderWidth = 3, HourThickness = 5, MinuteThickness = 3.5, SecondThickness = 1.2, MinuteTrack = true, Tachymeter = true, DialFill = "#101216" },
            new ClockColors("#8A93A0", "#C9D1DB", "#E6EAF0", "#E6EAF0", "#E6EAF0", "#FF8A3D")),
        // A 24-hour dial: the hour hand goes once round a day, midnight at the top.
        new("hours24", "24 hour", new ClockStyle { Kind = DialKind.TwentyFourHour, Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.All, Hands = HandStyle.Tapered, BorderWidth = 2, HourThickness = 5, MinuteThickness = 3 }),
        // Retro railway dial: every number, a minute track and heavy black hands on ivory.
        new("railway", "Railway", new ClockStyle { Markers = MarkerStyle.Ticks, Numerals = NumeralStyle.All, Hands = HandStyle.Baton, BorderWidth = 4, HourThickness = 7, MinuteThickness = 5, SecondThickness = 1.5, MinuteTrack = true, DialFill = "#F4F1E8" },
            new ClockColors("#1B1B1B", "#1B1B1B", "#1B1B1B", "#1B1B1B", "#1B1B1B", "#C62828")),
        // Pilot dial: luminous markers and sword hands on black.
        new("pilot", "Pilot", new ClockStyle { Markers = MarkerStyle.Lume, Numerals = NumeralStyle.Four, Hands = HandStyle.Sword, BorderWidth = 3, HourThickness = 6, MinuteThickness = 4.5, SecondThickness = 1.5, MinuteTrack = true, DialFill = "#0A0B0D" },
            new ClockColors("#6B7280", "#D8F5A2", "#D8F5A2", "#D8F5A2", "#D8F5A2", "#FF9F43")),
    ];

    // The preset's look on top of the current style; alignment, the seconds switch and the stopwatch display are the user's, not the
    // look's. Colors too, unless the face has a solid dial and so brings colors that read on it.
    public static ClockStyle Apply(string id, ClockStyle current) =>
        All.FirstOrDefault(p => p.Id == id) is { } preset
            ? preset.Style with { Alignment = current.Alignment, ShowSeconds = current.ShowSeconds, Stopwatch = current.Stopwatch, Colors = preset.Colors ?? current.Colors }
            : current;

    // The preset a style matches exactly (ignoring alignment, seconds, the stopwatch display and the colors of a theme face), or null
    // for a custom look.
    public static string? IdOf(ClockStyle style) =>
        All.FirstOrDefault(p => Apply(p.Id, style) == style)?.Id;
}

public sealed record ClockPalette(string Id, string Title, ClockColors Colors);

public static class ClockPalettes
{
    public static IReadOnlyList<ClockPalette> All { get; } =
    [
        new("theme", "Theme", ClockColors.Theme),
        new("mono", "Mono", new ClockColors("#9AA0A6", "#9AA0A6", "#E8EAED", "#E8EAED", "#E8EAED", "#9AA0A6")),
        new("amber", "Amber", new ClockColors("#7A5A2B", "#C98B2E", "#F2C46D", "#F2C46D", "#F2C46D", "#E8702A")),
        new("ocean", "Ocean", new ClockColors("#2B5C7A", "#4FA3C7", "#BFE6F5", "#BFE6F5", "#BFE6F5", "#F2A65A")),
        new("forest", "Forest", new ClockColors("#2F5D3A", "#6FAF7B", "#D4EBD8", "#D4EBD8", "#D4EBD8", "#E0B341")),
    ];

    public static ClockStyle Apply(string id, ClockStyle current) =>
        All.FirstOrDefault(p => p.Id == id) is { } palette ? current with { Colors = palette.Colors } : current;
}

public sealed record ClockSwatch(string Title, string? Hex);

// The colors offered for each part of the clock. The first (no color) means the theme's own color.
public static class ClockSwatches
{
    public static IReadOnlyList<ClockSwatch> All { get; } =
    [
        new("Theme", null), new("White", "#F5F5F5"), new("Gray", "#9AA0A6"), new("Amber", "#F2C46D"), new("Orange", "#E8702A"),
        new("Red", "#E05A5A"), new("Green", "#6FAF7B"), new("Blue", "#4FA3C7"), new("Violet", "#A98BE0"),
    ];

    public static int IndexOf(string? hex) => hex is null ? 0 : ListIndex(hex);

    private static int ListIndex(string hex)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}
