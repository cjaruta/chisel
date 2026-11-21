using Chisel.Core.Notes;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class NotepadSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaults_WrapOnAndProportionalFont()
    {
        using var context = _fixture.CreateContext();

        var settings = new NotepadSettingsRepository(context).Get();

        Assert.Equal(NotepadSettings.Default, settings);
        Assert.Null(settings.LastPadId);
        Assert.True(settings.WordWrap);
        Assert.False(settings.Monospace);
    }

    [Fact]
    public void Save_RoundTrips_AndReplacesThePreviousSettings()
    {
        var settings = new NotepadSettings(Guid.NewGuid(), Monospace: true, WordWrap: false);
        using (var context = _fixture.CreateContext())
        {
            var repository = new NotepadSettingsRepository(context);
            repository.Save(NotepadSettings.Default);
            repository.Save(settings);
        }

        using var again = _fixture.CreateContext();
        Assert.Equal(settings, new NotepadSettingsRepository(again).Get());
        Assert.Single(again.NotepadSettings);
    }

    public void Dispose() => _fixture.Dispose();
}
