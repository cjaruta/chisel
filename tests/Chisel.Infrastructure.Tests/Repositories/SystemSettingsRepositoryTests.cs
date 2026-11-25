using Chisel.Core.Settings;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class SystemSettingsRepositoryTests
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Get_WithNothingSaved_ReturnsSafeDefaults()
    {
        using var context = _fixture.CreateContext();
        var settings = new SystemSettingsRepository(context).Get();
        Assert.Equal(SystemSettings.Default, settings);
        Assert.False(settings.CloseToTray);
        Assert.False(settings.StartMinimized);
        Assert.Equal("Ctrl+Alt+C", settings.Hotkey);
    }

    [Fact]
    public void Save_ReplacesTheOneRow_AndRoundTrips()
    {
        var settings = new SystemSettings(CloseToTray: true, StartMinimized: true, Hotkey: "");
        using (var context = _fixture.CreateContext())
        {
            var repository = new SystemSettingsRepository(context);
            repository.Save(SystemSettings.Default);
            repository.Save(settings);
        }
        using var again = _fixture.CreateContext();
        Assert.Equal(settings, new SystemSettingsRepository(again).Get());
        Assert.Single(again.SystemSettings);
    }
}
