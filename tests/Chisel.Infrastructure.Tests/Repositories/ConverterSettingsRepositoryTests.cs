using Chisel.Core.Converter;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ConverterSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaultSettings()
    {
        using var context = _fixture.CreateContext();
        Assert.Equal(ConverterSettings.Default, new ConverterSettingsRepository(context).Get());
    }

    [Fact]
    public void Save_RoundTrips_AndReplacesThePreviousSettings()
    {
        var settings = new ConverterSettings("data", "GiB", "GB");
        using (var context = _fixture.CreateContext())
        {
            var repository = new ConverterSettingsRepository(context);
            repository.Save(ConverterSettings.Default);
            repository.Save(settings);
        }

        using var again = _fixture.CreateContext();
        Assert.Equal(settings, new ConverterSettingsRepository(again).Get());
        Assert.Single(again.ConverterSettings);
    }

    [Fact]
    public void Get_PutsUnknownUnitsBackToDefaults()
    {
        using (var context = _fixture.CreateContext())
        {
            context.ConverterSettings.Add(new ConverterSettingsEntity { Category = "mass", From = "furlong", To = "lb" });
            context.SaveChanges();
        }

        using var again = _fixture.CreateContext();
        Assert.Equal(new ConverterSettings("mass", "kg", "lb"), new ConverterSettingsRepository(again).Get());
    }

    public void Dispose() => _fixture.Dispose();
}
