using Chisel.Core.Links;
using Xunit;

namespace Chisel.Core.Tests.Links;

public class QuickLinkTests
{
    [Fact]
    public void Constructor_ValidInput_SetsLabelAndUrl()
    {
        var link = new QuickLink("  GitHub  ", "https://github.com");

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal("GitHub", link.Label);
        Assert.Equal("https://github.com", link.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankLabel_ThrowsArgumentException(string label)
    {
        void Act() => new QuickLink(label, "https://github.com");

        Assert.Throws<ArgumentException>(Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com")]
    public void Constructor_InvalidUrl_ThrowsArgumentException(string url)
    {
        void Act() => new QuickLink("Files", url);

        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void Constructor_WithSavedState_RestoresId()
    {
        var id = Guid.NewGuid();

        var link = new QuickLink(id, "GitHub", "https://github.com");

        Assert.Equal(id, link.Id);
    }
}