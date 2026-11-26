using Chisel.Core.SystemMonitor;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class SystemMonitorSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaults_EveryStatEverySecond()
    {
        using var context = _fixture.CreateContext();
        var settings = new SystemMonitorSettingsRepository(context).Get();

        Assert.Equal(SystemStats.All, settings.Shown);
        Assert.Equal(1, settings.RefreshSeconds);
    }

    [Fact]
    public void Save_RoundTrips_AndReplacesThePreviousSettings()
    {
        using (var context = _fixture.CreateContext())
        {
            var repository = new SystemMonitorSettingsRepository(context);
            repository.Save(SystemMonitorSettings.Default);
            repository.Save(new SystemMonitorSettings(SystemStats.Cpu | SystemStats.Network, RefreshSeconds: 5));
        }

        using var again = _fixture.CreateContext();
        var settings = new SystemMonitorSettingsRepository(again).Get();
        Assert.Equal(SystemStats.Cpu | SystemStats.Network, settings.Shown);
        Assert.Equal(5, settings.RefreshSeconds);
        Assert.Single(again.SystemMonitorSettings);
    }

    [Fact]
    public void ARateThatIsNotOffered_OrUnknownStatBits_AreDropped()
    {
        using (var context = _fixture.CreateContext())
        {
            context.SystemMonitorSettings.Add(new SystemMonitorSettingsEntity { Shown = (int)SystemStats.Ram | 1 << 20, RefreshSeconds = 3 });
            context.SaveChanges();
        }

        using var again = _fixture.CreateContext();
        var settings = new SystemMonitorSettingsRepository(again).Get();
        Assert.Equal(SystemStats.Ram, settings.Shown);
        Assert.Equal(1, settings.RefreshSeconds);
    }

    public void Dispose() => _fixture.Dispose();
}
