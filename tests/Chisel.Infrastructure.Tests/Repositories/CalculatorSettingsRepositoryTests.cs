using Chisel.Core.Calculator;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class CalculatorSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaultSettings()
    {
        using var context = _fixture.CreateContext();
        Assert.Equal(CalculatorSettings.Default, new CalculatorSettingsRepository(context).Get());
    }

    [Fact]
    public void Save_RoundTrips_AndReplacesThePreviousSettings()
    {
        var settings = new CalculatorSettings(Advanced: true, Angle: AngleMode.Radians);
        using (var context = _fixture.CreateContext())
        {
            var repository = new CalculatorSettingsRepository(context);
            repository.Save(CalculatorSettings.Default);
            repository.Save(settings);
        }

        using var again = _fixture.CreateContext();
        Assert.Equal(settings, new CalculatorSettingsRepository(again).Get());
        Assert.Single(again.CalculatorSettings);
    }

    public void Dispose() => _fixture.Dispose();
}
