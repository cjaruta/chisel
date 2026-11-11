using Chisel.Core.Settings;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class UserSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Get_OnEmptyDatabase_ReturnsNull()
    {
        using var context = _fixture.CreateContext();
        var repository = new UserSettingsRepository(context);

        Assert.Null(repository.Get());
    }

    [Fact]
    public void Save_ThenGet_ReturnsTheSavedSettings()
    {
        using var context = _fixture.CreateContext();
        var repository = new UserSettingsRepository(context);

        repository.Save(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)));

        using var readContext = _fixture.CreateContext();
        var settings = new UserSettingsRepository(readContext).Get();

        Assert.NotNull(settings);
        Assert.Equal("Test User", settings!.Name);
        Assert.Equal(new TimeOnly(6, 30), settings.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), settings.BedTime);
    }

    [Fact]
    public void Save_CalledAgain_ReplacesThePreviousSettings()
    {
        using var context = _fixture.CreateContext();
        var repository = new UserSettingsRepository(context);
        repository.Save(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)));

        repository.Save(new UserSettings("Alex", new TimeOnly(7, 0), new TimeOnly(22, 0)));

        using var readContext = _fixture.CreateContext();
        var settings = new UserSettingsRepository(readContext).Get();

        Assert.Equal("Alex", settings!.Name);
        Assert.Single(readContext.UserSettings);
    }

    [Fact]
    public void Save_ThenGet_PersistsTheClockFormat()
    {
        using var context = _fixture.CreateContext();
        var repository = new UserSettingsRepository(context);

        repository.Save(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour));

        using var readContext = _fixture.CreateContext();
        Assert.Equal(ClockFormat.TwelveHour, new UserSettingsRepository(readContext).Get()!.ClockFormat);
    }

    [Fact]
    public void ClockFormat_DefaultsToTwentyFourHour()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

        Assert.Equal(ClockFormat.TwentyFourHour, settings.ClockFormat);
        Assert.Equal(ClockFormat.TwelveHour, settings.WithClockFormat(ClockFormat.TwelveHour).ClockFormat);
    }

    public void Dispose() => _fixture.Dispose();
}
