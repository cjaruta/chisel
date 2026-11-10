using Chisel.Core.Links;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class QuickLinkRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void GetAll_AfterAddingLinks_ReturnsThemAll()
    {
        using var context = _fixture.CreateContext();
        var repository = new QuickLinkRepository(context);

        repository.Add(new QuickLink("Docs", "https://docs.example.com"));
        repository.Add(new QuickLink("Mail", "https://mail.example.com"));

        using var readContext = _fixture.CreateContext();
        var labels = new QuickLinkRepository(readContext).GetAll().Select(l => l.Label).ToList();

        Assert.Equal(new[] { "Docs", "Mail" }, labels);
    }

    [Fact]
    public void Remove_ExistingLink_DeletesItFromTheDatabase()
    {
        using var context = _fixture.CreateContext();
        var repository = new QuickLinkRepository(context);
        var keep = new QuickLink("Keep", "https://keep.example.com");
        var drop = new QuickLink("Drop", "https://drop.example.com");
        repository.Add(keep);
        repository.Add(drop);

        repository.Remove(drop.Id);

        using var readContext = _fixture.CreateContext();
        var remaining = Assert.Single(new QuickLinkRepository(readContext).GetAll());
        Assert.Equal(keep.Id, remaining.Id);
    }

    [Fact]
    public void Remove_UnknownId_DoesNothing()
    {
        using var context = _fixture.CreateContext();
        var repository = new QuickLinkRepository(context);
        repository.Add(new QuickLink("Keep", "https://keep.example.com"));

        repository.Remove(Guid.NewGuid());

        Assert.Single(repository.GetAll());
    }

    public void Dispose() => _fixture.Dispose();
}
