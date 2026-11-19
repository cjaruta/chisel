using Chisel.Core.Settings;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class MediaGradientSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Save_ThenGet_PersistsBothGradientChoices()
    {
        using var context = _fixture.CreateContext();
        new UserSettingsRepository(context).Save(
            new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).WithMediaAlbumGradient(false).WithMediaAnimateGradient(false));

        using var readContext = _fixture.CreateContext();
        var settings = new UserSettingsRepository(readContext).Get()!;
        Assert.False(settings.MediaAlbumGradient);
        Assert.False(settings.MediaAnimateGradient);
    }

    public void Dispose() => _fixture.Dispose();
}
