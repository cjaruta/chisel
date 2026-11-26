using Avalonia.Input;
using Chisel.App.Services;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.Services;

public class HotkeyKeysTests
{
    [Theory]
    [InlineData(Key.K, "K", 0x4Bu)]
    [InlineData(Key.D7, "7", 0x37u)]
    [InlineData(Key.F12, "F12", 0x7Bu)]
    [InlineData(Key.PageDown, "PageDown", 0x22u)]
    [InlineData(Key.Space, "Space", 0x20u)]
    public void AKey_HasAShortcutName_AndAWindowsCode(Key key, string name, uint virtualKey)
    {
        Assert.Equal(name, HotkeyKeys.NameOf(key));
        Assert.Equal(virtualKey, HotkeyKeys.VirtualKey(name));
    }

    [Fact]
    public void EveryKeyAShortcutAccepts_HasAWindowsCode()
    {
        foreach (var name in new[] { "A", "Z", "0", "9", "F1", "F24", "Home", "End", "Insert", "Delete", "PageUp", "Up", "Down", "Left", "Right", "Pause" })
        {
            Assert.NotNull(Hotkey.Validate(HotkeyModifiers.Ctrl, name).Hotkey);
            Assert.NotNull(HotkeyKeys.VirtualKey(name));
        }
    }

    [Fact]
    public void ModifierKeys_AreRecognized_AndMapped()
    {
        Assert.True(HotkeyKeys.IsModifier(Key.LeftCtrl));
        Assert.False(HotkeyKeys.IsModifier(Key.C));
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win,
            HotkeyKeys.Modifiers(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta));
    }
}
