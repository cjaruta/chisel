namespace Chisel.Core.Schedule;

// A block's color is stored as one of these short palette keys, not as a color value,
// so the actual colors can change later. The app decides what each key looks like.
public static class BlockColor
{
    public const string Slate = "slate";
    public const string Sage = "sage";
    public const string Teal = "teal";
    public const string Amber = "amber";
    public const string Terracotta = "terracotta";
    public const string Rose = "rose";
    public const string Lavender = "lavender";
    public const string Gray = "gray";

    // Neutral color for blocks saved before colors existed, and for any key that isn't known.
    public const string Default = Gray;

    public static IReadOnlyList<string> Keys { get; } =
        new[] { Slate, Sage, Teal, Amber, Terracotta, Rose, Lavender, Gray };

    public static string Normalize(string? key)
    {
        var trimmed = key?.Trim().ToLowerInvariant();
        return trimmed is not null && Keys.Contains(trimmed) ? trimmed : Default;
    }
}
