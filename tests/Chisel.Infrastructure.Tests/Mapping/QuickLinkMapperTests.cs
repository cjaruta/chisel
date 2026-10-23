using Chisel.Core.Links;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class QuickLinkMapperTests
{
    [Fact]
    public void ToEntity_CopiesIdLabelAndUrl()
    {
        var link = new QuickLink(Guid.NewGuid(), "GitHub", "https://github.com");

        var entity = link.ToEntity();

        Assert.Equal(link.Id, entity.Id);
        Assert.Equal("GitHub", entity.Label);
        Assert.Equal("https://github.com", entity.Url);
    }

    [Fact]
    public void ToDomain_CopiesIdLabelAndUrl()
    {
        var entity = new QuickLinkEntity { Id = Guid.NewGuid(), Label = "GitHub", Url = "https://github.com" };

        var link = entity.ToDomain();

        Assert.Equal(entity.Id, link.Id);
        Assert.Equal("GitHub", link.Label);
        Assert.Equal("https://github.com", link.Url);
    }
}