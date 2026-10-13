using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class QuickLinkEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void QuickLinks_AddAndSave_CanBeReadBack()
    {
        using var context = _fixture.CreateContext();
        var link = new QuickLinkEntity
        {
            Id = Guid.NewGuid(),
            Label = "GitHub",
            Url = "https://github.com"
        };

        context.QuickLinks.Add(link);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var saved = readContext.QuickLinks.First(l => l.Id == link.Id);

        Assert.Equal("GitHub", saved.Label);
        Assert.Equal("https://github.com", saved.Url);
    }

    public void Dispose() => _fixture.Dispose();
}