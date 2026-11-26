namespace Chisel.Core.Settings;

// The values match Windows' MOD_ALT, MOD_CONTROL, MOD_SHIFT and MOD_WIN.
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

public sealed record HotkeyValidation(Hotkey? Hotkey, string? Error);

// A show/hide shortcut such as Ctrl+Alt+C: modifiers plus one key, saved as text in a fixed order.
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    // Keys a shortcut can end with: letters, digits, F1-F24 and a few navigation keys.
    private static readonly HashSet<string> KeySet = new(
        Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
            .Concat(Enumerable.Range(0, 10).Select(d => d.ToString()))
            .Concat(Enumerable.Range(1, 24).Select(f => $"F{f}"))
            .Concat(["Space", "Home", "End", "Insert", "Delete", "PageUp", "PageDown", "Up", "Down", "Left", "Right", "Pause"]),
        StringComparer.OrdinalIgnoreCase);

    private static readonly (HotkeyModifiers Modifier, string Name)[] Order =
        [(HotkeyModifiers.Ctrl, "Ctrl"), (HotkeyModifiers.Alt, "Alt"), (HotkeyModifiers.Shift, "Shift"), (HotkeyModifiers.Win, "Win")];

    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries))
        {
            var modifier = ModifierNamed(part);
            if (modifier != HotkeyModifiers.None)
            {
                if (modifiers.HasFlag(modifier))
                {
                    return null;
                }
                modifiers |= modifier;
            }
            else if (key is null)
            {
                key = part;
            }
            else
            {
                return null;
            }
        }
        return Validate(modifiers, key).Hotkey;
    }

    // A shortcut needs Ctrl, Alt or Win (Shift alone would take over normal typing) and one supported key.
    public static HotkeyValidation Validate(HotkeyModifiers modifiers, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new(null, "Press a key along with Ctrl, Alt or Win.");
        }
        if ((modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) == 0)
        {
            return new(null, "Add Ctrl, Alt or Win so the shortcut doesn't get in the way of normal typing.");
        }
        if (!KeySet.TryGetValue(key, out var canonical))
        {
            return new(null, $"{key} can't be used in a shortcut. Try a letter, a number or a function key.");
        }
        return new(new Hotkey(modifiers, canonical), null);
    }

    public override string ToString() =>
        string.Join("+", Order.Where(o => Modifiers.HasFlag(o.Modifier)).Select(o => o.Name).Append(Key));

    private static HotkeyModifiers ModifierNamed(string name) => name.ToLowerInvariant() switch
    {
        "ctrl" or "control" => HotkeyModifiers.Ctrl,
        "alt" => HotkeyModifiers.Alt,
        "shift" => HotkeyModifiers.Shift,
        "win" or "windows" or "meta" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };
}
