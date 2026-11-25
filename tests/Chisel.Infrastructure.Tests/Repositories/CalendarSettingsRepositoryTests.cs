using Chisel.Core.Calendar;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class CalendarSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaults_FollowingTheSystemsFirstDay()
    {
        using var context = _fixture.CreateContext();
        var settings = new CalendarSettingsRepository(context).Get();

        Assert.Null(settings.FirstDay);
        Assert.False(settings.ShowWeekNumbers);
        Assert.Empty(settings.MarkedDays);
    }

    [Fact]
    public void Save_RoundTrips_AndReplacesThePreviousSettings()
    {
        var marked = new[] { new DateOnly(2000, 1, 1), new DateOnly(2000, 2, 29) };
        using (var context = _fixture.CreateContext())
        {
            var repository = new CalendarSettingsRepository(context);
            repository.Save(CalendarSettings.Default);
            repository.Save(new CalendarSettings(DayOfWeek.Monday, ShowWeekNumbers: true, marked));
        }

        using var again = _fixture.CreateContext();
        var settings = new CalendarSettingsRepository(again).Get();
        Assert.Equal(DayOfWeek.Monday, settings.FirstDay);
        Assert.True(settings.ShowWeekNumbers);
        Assert.Equal(marked, settings.MarkedDays);
        Assert.Single(again.CalendarSettings);
    }

    public void Dispose() => _fixture.Dispose();
}
