using Chisel.Core.Schedule;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class MarkerRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void GetAll_OnEmptyDatabase_ReturnsEmpty()
    {
        using var context = _fixture.CreateContext();
        var repository = new MarkerRepository(context);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void ReplaceAll_WithMarkers_PersistsThemAll()
    {
        using var context = _fixture.CreateContext();
        var repository = new MarkerRepository(context);

        repository.ReplaceAll(new[]
        {
            new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder),
            new Marker("All work done", new TimeOnly(18, 0), MarkerKind.Checkpoint)
        });

        using var readContext = _fixture.CreateContext();
        var labels = new MarkerRepository(readContext).GetAll().Select(m => m.Label).ToList();

        Assert.Equal(new[] { "Last coffee", "All work done" }, labels);
    }

    [Fact]
    public void ReplaceAll_CalledAgain_DiscardsThePreviousSet()
    {
        using var context = _fixture.CreateContext();
        var repository = new MarkerRepository(context);
        repository.ReplaceAll(new[] { new Marker("Old", new TimeOnly(9, 0), MarkerKind.Reminder) });

        repository.ReplaceAll(new[] { new Marker("New", new TimeOnly(10, 0), MarkerKind.Checkpoint) });

        using var readContext = _fixture.CreateContext();
        var marker = Assert.Single(new MarkerRepository(readContext).GetAll());
        Assert.Equal("New", marker.Label);
    }

    public void Dispose() => _fixture.Dispose();
}
