namespace Chisel.Core.Clipboard;

// The clipboard history's choices. Everything starts off or small: history is kept in memory only unless KeepHistory is turned on.
// Recording itself is never saved: it starts off every time the app opens.
public sealed record ClipboardHistorySettings(bool KeepHistory, int MaxItems, int MaxLength)
{
    public const int DefaultMaxItems = 25;
    public const int MaxItemsLimit = 100;
    public const int DefaultMaxLength = 10_000;
    public const int MinLength = 100;
    public const int MaxLengthLimit = 100_000;

    public static ClipboardHistorySettings Default { get; } = new(false, DefaultMaxItems, DefaultMaxLength);

    // These settings with out-of-range numbers pulled back in (a hand-edited database).
    public ClipboardHistorySettings Valid() => this with
    {
        MaxItems = Math.Clamp(MaxItems, 1, MaxItemsLimit),
        MaxLength = Math.Clamp(MaxLength, MinLength, MaxLengthLimit),
    };
}

public interface IClipboardHistorySettingsRepository
{
    ClipboardHistorySettings Get();
    void Save(ClipboardHistorySettings settings);
}
