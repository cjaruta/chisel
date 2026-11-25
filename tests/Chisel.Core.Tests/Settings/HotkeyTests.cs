using Chisel.Core.Settings;
using Xunit;

namespace Chisel.Core.Tests.Settings;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+C", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "C")]
    [InlineData("alt + control + c", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "C")]
    [InlineData("Win+Shift+F12", HotkeyModifiers.Win | HotkeyModifiers.Shift, "F12")]
    [InlineData("Ctrl+Shift+Space", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space")]
    [InlineData("Ctrl+Alt+7", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "7")]
    public void Parse_ReadsModifiersInAnyOrderAndCase(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.Equal(new Hotkey(modifiers, key), Hotkey.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    [InlineData("C")]
    [InlineData("Shift+C")]
    [InlineData("Ctrl+Alt+C+D")]
    [InlineData("Ctrl+Alt+Escape")]
    [InlineData("Ctrl+Ctrl+C")]
    public void Parse_RejectsAnythingThatIsNotOneValidShortcut(string? text)
    {
        Assert.Null(Hotkey.Parse(text));
    }

    [Fact]
    public void ToString_IsTheCanonicalForm_SoItRoundTrips()
    {
        var hotkey = Hotkey.Parse("shift+win+alt+ctrl+k")!;
        Assert.Equal("Ctrl+Alt+Shift+Win+K", hotkey.ToString());
        Assert.Equal(hotkey, Hotkey.Parse(hotkey.ToString()));
        Assert.Equal(SystemSettings.DefaultHotkey, Hotkey.Parse(SystemSettings.DefaultHotkey)!.ToString());
    }

    [Theory]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, null, "Press a key")]
    [InlineData(HotkeyModifiers.None, "C", "Ctrl, Alt or Win")]
    [InlineData(HotkeyModifiers.Shift, "C", "Ctrl, Alt or Win")]
    [InlineData(HotkeyModifiers.Ctrl, "Escape", "can't be used")]
    public void Validate_ExplainsWhatIsMissing(HotkeyModifiers modifiers, string? key, string error)
    {
        var result = Hotkey.Validate(modifiers, key);
        Assert.Null(result.Hotkey);
        Assert.Contains(error, result.Error);
    }

    [Fact]
    public void Validate_AcceptsAModifierAndAKey()
    {
        var result = Hotkey.Validate(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "f5");
        Assert.Null(result.Error);
        Assert.Equal("Ctrl+Shift+F5", result.Hotkey!.ToString());
    }
}
