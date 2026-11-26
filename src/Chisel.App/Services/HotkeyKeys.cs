using Avalonia.Input;
using Chisel.Core.Settings;

namespace Chisel.App.Services;

// Translates between Avalonia keys, shortcut key names (Hotkey.Key) and Windows virtual-key codes.
public static class HotkeyKeys
{
    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    public static HotkeyModifiers Modifiers(KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Control) ? HotkeyModifiers.Ctrl : 0)
        | (modifiers.HasFlag(KeyModifiers.Alt) ? HotkeyModifiers.Alt : 0)
        | (modifiers.HasFlag(KeyModifiers.Shift) ? HotkeyModifiers.Shift : 0)
        | (modifiers.HasFlag(KeyModifiers.Meta) ? HotkeyModifiers.Win : 0);

    // The shortcut name of a key; keys a shortcut can't use come back as their own name so the error can mention them.
    public static string NameOf(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F24 => $"F{key - Key.F1 + 1}",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        _ => key.ToString(),
    };

    public static uint? VirtualKey(string name)
    {
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            return char.ToUpperInvariant(name[0]);
        }
        if (name.Length > 1 && name[0] is 'F' or 'f' && int.TryParse(name[1..], out var f) && f is >= 1 and <= 24)
        {
            return (uint)(0x70 + f - 1);
        }
        return name.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "end" => 0x23,
            "home" => 0x24,
            "left" => 0x25,
            "up" => 0x26,
            "right" => 0x27,
            "down" => 0x28,
            "insert" => 0x2D,
            "delete" => 0x2E,
            "pause" => 0x13,
            _ => null,
        };
    }
}
