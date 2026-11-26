using Chisel.Core.Settings;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WidgetSpacingRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void TheSpacing_IsSavedAndComesBack()
    {
        using (var context = _fixture.CreateContext())
        {
            new UserSettingsRepository(context).Save(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), widgetSpacing: 5));
        }

        using var readContext = _fixture.CreateContext();
        Assert.Equal(5, new UserSettingsRepository(readContext).Get()!.WidgetSpacing);
    }

    public void Dispose() => _fixture.Dispose();
}
