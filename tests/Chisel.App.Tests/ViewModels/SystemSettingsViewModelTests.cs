using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class SystemSettingsViewModelTests
{
    [Fact]
    public void CloseChoice_LoadsAndSaves()
    {
        var repository = new MemorySystemSettings();
        var vm = new SystemSettingsViewModel(repository);
        Assert.True(vm.QuitOnClose);
        Assert.False(vm.CloseToTray);

        vm.CloseToTray = true;
        vm.StartMinimized = true;

        Assert.False(vm.QuitOnClose);
        Assert.Equal(new SystemSettings(true, true, SystemSettings.DefaultHotkey), repository.Saved);
        Assert.Equal(repository.Saved, vm.Current);
        Assert.True(new SystemSettingsViewModel(repository).CloseToTray);
    }
}

public class WelcomeHintTests
{
    [Fact]
    public void ExistingUsers_DoNotSeeTheWelcome()
    {
        Assert.False(new SystemSettingsViewModel(new MemorySystemSettings()).ShowWelcome);
    }

    [Fact]
    public void FirstRun_ShowsTheWelcome_UntilItIsDismissedForGood()
    {
        var repository = new MemorySystemSettings();
        var vm = new SystemSettingsViewModel(repository);

        vm.StartWelcome();
        Assert.True(vm.ShowWelcome);
        Assert.True(new SystemSettingsViewModel(repository).ShowWelcome);

        vm.DismissWelcomeCommand.Execute(null);

        Assert.False(vm.ShowWelcome);
        Assert.False(new SystemSettingsViewModel(repository).ShowWelcome);
    }

    [Fact]
    public void TheWelcome_CanBeReopenedFromSettings()
    {
        var repository = new MemorySystemSettings();
        var vm = new SystemSettingsViewModel(repository);

        vm.ReopenWelcomeCommand.Execute(null);

        Assert.True(vm.ShowWelcome);
        Assert.True(repository.Saved!.ShowWelcome);
    }

    [Fact]
    public void ChangingAnotherSetting_KeepsTheWelcomeState()
    {
        var repository = new MemorySystemSettings();
        var vm = new SystemSettingsViewModel(repository);
        vm.StartWelcome();

        vm.CloseToTray = true;

        Assert.True(repository.Saved!.ShowWelcome);
    }
}

public class StartWithWindowsSettingTests
{
    private readonly FakeStartupRegistry _registry = new();

    private SystemSettingsViewModel Vm() =>
        new(new MemorySystemSettings(), new Chisel.App.Startup.StartWithWindows(_registry, @"C:\Apps\Chisel.App.exe"));

    [Fact]
    public void TheToggle_ShowsTheRegistry_AndChangesIt()
    {
        _registry.Values["Chisel"] = @"""C:\Apps\Chisel.App.exe"" --minimized";
        var vm = Vm();
        Assert.True(vm.StartWithWindows);
        Assert.True(vm.CanStartWithWindows);

        vm.StartWithWindows = false;
        Assert.Empty(_registry.Values);
        vm.StartWithWindows = true;
        Assert.Single(_registry.Values);
        Assert.Null(vm.StartupError);
    }

    [Fact]
    public void WhenWindowsRefuses_TheToggleGoesBack_AndTheReasonIsShown()
    {
        var vm = Vm();
        _registry.Blocked = true;
        vm.StartWithWindows = true;
        Assert.False(vm.StartWithWindows);
        Assert.Contains("policy", vm.StartupError);
    }
}
