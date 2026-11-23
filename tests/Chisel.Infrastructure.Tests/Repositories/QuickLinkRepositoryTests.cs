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

    [Fact]
    public void Update_ChangesTheLinkInPlace_WithoutMovingIt()
    {
        using var context = _fixture.CreateContext();
        var repository = new QuickLinkRepository(context);
        var docs = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(docs);
        repository.Add(new QuickLink("Mail", "https://mail.example.com"));

        repository.Update(new QuickLink(docs.Id, "Documentation", @"C:\Docs"));

        using var readContext = _fixture.CreateContext();
        var links = new QuickLinkRepository(readContext).GetAll();
        Assert.Equal(new[] { "Documentation", "Mail" }, links.Select(l => l.Label).ToArray());
        Assert.Equal(@"C:\Docs", links[0].Url);
        Assert.Equal(docs.Id, links[0].Id);
    }

    [Fact]
    public void Reorder_SavesTheNewOrder_AndNewLinksGoAfterIt()
    {
        using var context = _fixture.CreateContext();
        var repository = new QuickLinkRepository(context);
        var a = new QuickLink("A", "https://a.example.com");
        var b = new QuickLink("B", "https://b.example.com");
        var c = new QuickLink("C", "https://c.example.com");
        repository.Add(a);
        repository.Add(b);
        repository.Add(c);

        repository.Reorder(new[] { c.Id, a.Id, b.Id });
        repository.Add(new QuickLink("D", "https://d.example.com"));

        using var readContext = _fixture.CreateContext();
        Assert.Equal(new[] { "C", "A", "B", "D" }, new QuickLinkRepository(readContext).GetAll().Select(l => l.Label).ToArray());
    }

    [Fact]
    public void Update_OfAMissingLink_Throws()
    {
        using var context = _fixture.CreateContext();

        Assert.Throws<InvalidOperationException>(() => new QuickLinkRepository(context).Update(new QuickLink("X", "https://x.example.com")));
    }
}
