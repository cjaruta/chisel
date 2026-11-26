using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class HotkeySettingTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;
    private readonly MemorySystemSettings _repository = new();
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly List<string> _failures = new();

    private SystemSettingsViewModel Attached()
    {
        var vm = new SystemSettingsViewModel(_repository);
        vm.AttachHotkeys(_hotkeys, _failures.Add);
        return vm;
    }

    [Fact]
    public void TheSavedShortcut_IsRegisteredAtLaunch()
    {
        var vm = Attached();
        Assert.Equal("Ctrl+Alt+C", _hotkeys.Registered?.ToString());
        Assert.Equal("Ctrl+Alt+C", vm.HotkeyText);
        Assert.True(vm.HotkeyOn);
        Assert.Null(vm.HotkeyError);
    }

    [Fact]
    public void ASavedShortcutTakenByAnotherApp_IsReported_NotSilentlyDropped()
    {
        _hotkeys.Taken.Add("Ctrl+Alt+C");
        var vm = Attached();
        Assert.Null(_hotkeys.Registered);
        Assert.Contains("another app", vm.HotkeyError);
        Assert.Equal(vm.HotkeyError, Assert.Single(_failures));
    }

    [Fact]
    public void CapturingANewShortcut_SwapsTheRegistration_AndSavesIt()
    {
        var vm = Attached();
        vm.BeginHotkeyCapture();
        Assert.Null(_hotkeys.Registered);
        Assert.True(vm.CaptureHotkey(CtrlAlt, "K"));
        vm.EndHotkeyCapture();

        Assert.Equal("Ctrl+Alt+K", _hotkeys.Registered?.ToString());
        Assert.Equal("Ctrl+Alt+K", _repository.Saved.Hotkey);
        Assert.Equal("Ctrl+Alt+K", vm.HotkeyText);
    }

    [Fact]
    public void AnInvalidShortcut_ShowsWhy_AndKeepsTheOldOne()
    {
        var vm = Attached();
        vm.BeginHotkeyCapture();
        Assert.False(vm.CaptureHotkey(HotkeyModifiers.Shift, "K"));
        Assert.Contains("Ctrl, Alt or Win", vm.HotkeyError);
        vm.EndHotkeyCapture();

        Assert.Equal("Ctrl+Alt+C", _hotkeys.Registered?.ToString());
        Assert.Equal("Ctrl+Alt+C", _repository.Saved.Hotkey);
    }

    [Fact]
    public void AShortcutUsedByAnotherApp_ShowsWhy_AndKeepsTheOldOne()
    {
        _hotkeys.Taken.Add("Ctrl+Alt+K");
        var vm = Attached();
        vm.BeginHotkeyCapture();
        Assert.False(vm.CaptureHotkey(CtrlAlt, "K"));
        Assert.Contains("Ctrl+Alt+K is already used by another app", vm.HotkeyError);
        vm.EndHotkeyCapture();

        Assert.Equal("Ctrl+Alt+C", _hotkeys.Registered?.ToString());
        Assert.Equal("Ctrl+Alt+C", _repository.Saved.Hotkey);
    }

    [Fact]
    public void TurningItOff_Unregisters_AndASavedOffStaysOff()
    {
        var vm = Attached();
        vm.TurnOffHotkeyCommand.Execute(null);
        Assert.Null(_hotkeys.Registered);
        Assert.Equal("", _repository.Saved.Hotkey);
        Assert.False(vm.HotkeyOn);
        Assert.Equal("Off", vm.HotkeyText);

        var again = new FakeHotkeyService();
        new SystemSettingsViewModel(_repository).AttachHotkeys(again);
        Assert.Null(again.Registered);
    }

    [Fact]
    public void Exit_Unregisters()
    {
        var vm = Attached();
        vm.ReleaseHotkey();
        Assert.Null(_hotkeys.Registered);
        Assert.Equal(1, _hotkeys.Unregisters);
    }

    [Fact]
    public void AnUnreadableSavedShortcut_IsReported()
    {
        _repository.Save(SystemSettings.Default with { Hotkey = "Ctrl+Nope" });
        var vm = Attached();
        Assert.Null(_hotkeys.Registered);
        Assert.NotNull(vm.HotkeyError);
    }
}
