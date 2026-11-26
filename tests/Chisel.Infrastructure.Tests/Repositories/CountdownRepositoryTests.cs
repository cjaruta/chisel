using Chisel.Core.Countdown;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class CountdownRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private CountdownSettings Load()
    {
        using var context = _fixture.CreateContext();
        return new CountdownRepository(context).Get();
    }

    private void Store(int version, string json)
    {
        using var context = _fixture.CreateContext();
        context.CountdownSettings.Add(new CountdownSettingsEntity { Version = version, Json = json });
        context.SaveChanges();
    }

    [Fact]
    public void Empty_ReturnsTheDefaults()
    {
        var settings = Load();
        Assert.Empty(settings.Events);
        Assert.Equal(PastEventPolicy.ShowAgo, settings.PastEvents);
        Assert.True(settings.SortByDate);
        Assert.True(settings.Notify);
    }

    [Fact]
    public void Save_RoundTripsEveryField_AndReplacesThePreviousSettings()
    {
        var trip = new CountdownEntry(Guid.NewGuid(), "Trip", new DateOnly(2000, 6, 1), new TimeOnly(16, 30), CountdownRepeat.None, "🎉",
            CountdownColor.Blue, new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5)), Archived: true, NotifiedOn: new DateOnly(2000, 6, 1));
        var birthday = new CountdownEntry(Guid.NewGuid(), "Birthday", new DateOnly(2000, 2, 29), Repeat: CountdownRepeat.Yearly);
        using (var context = _fixture.CreateContext())
        {
            var repository = new CountdownRepository(context);
            repository.Save(CountdownSettings.Default);
            repository.Save(new CountdownSettings([trip, birthday], PastEventPolicy.Archive, SortByDate: false, Notify: false));
        }

        var settings = Load();
        Assert.Equal(new[] { trip, birthday }, settings.Events);
        Assert.Equal(PastEventPolicy.Archive, settings.PastEvents);
        Assert.False(settings.SortByDate);
        Assert.False(settings.Notify);
        using var again = _fixture.CreateContext();
        var row = Assert.Single(again.CountdownSettings);
        Assert.Equal(CountdownRepository.CurrentVersion, row.Version);
    }

    [Fact]
    public void JsonThatDoesNotParse_GivesTheDefaults()
    {
        Store(1, "{ not json");
        Assert.Empty(Load().Events);
    }

    [Fact]
    public void ABadEvent_IsSkipped_AndTheRestKept()
    {
        Store(1, """
            {"pastEvents":"Delete","events":[
              {"id":"not-a-guid","name":"Kept","date":"2000-06-01","time":"25:99","repeat":"Weekly","color":"Pink"},
              {"name":"   ","date":"2000-06-01"},
              {"name":"No date"},
              {"name":"Bad date","date":"2000-13-40"},
              42
            ]}
            """);

        var settings = Load();
        var kept = Assert.Single(settings.Events);
        Assert.Equal("Kept", kept.Name);
        Assert.Equal(new DateOnly(2000, 6, 1), kept.Date);
        Assert.Null(kept.Time);
        Assert.Equal(CountdownRepeat.None, kept.Repeat);
        Assert.Equal(CountdownColor.None, kept.Color);
        Assert.NotEqual(Guid.Empty, kept.Id);
        Assert.Equal(PastEventPolicy.Delete, settings.PastEvents);
    }

    [Fact]
    public void ANewerVersion_IsReadForTheFieldsItKnows()
    {
        Store(CountdownRepository.CurrentVersion + 1, """{"events":[{"name":"Trip","date":"2000-06-01","somethingNew":true}],"alsoNew":1}""");
        Assert.Equal("Trip", Assert.Single(Load().Events).Name);
    }

    [Fact]
    public void TooManyEvents_AreCutToTheLimit()
    {
        var events = string.Join(",", Enumerable.Range(0, CountdownValidation.MaxEvents + 5).Select(i => $$"""{"name":"E{{i}}","date":"2000-06-01"}"""));
        Store(1, $$"""{"events":[{{events}}]}""");
        Assert.Equal(CountdownValidation.MaxEvents, Load().Events.Count);
    }

    public void Dispose() => _fixture.Dispose();
}
