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
