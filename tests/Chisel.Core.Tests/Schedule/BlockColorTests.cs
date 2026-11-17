using Chisel.Core.Schedule;
using Xunit;

namespace Chisel.Core.Tests.Schedule;

public class BlockColorTests
{
    [Fact]
    public void Keys_AreEightUniqueShortNames()
    {
        Assert.Equal(8, BlockColor.Keys.Count);
        Assert.Equal(BlockColor.Keys.Count, BlockColor.Keys.Distinct().Count());
        Assert.All(BlockColor.Keys, key => Assert.Matches("^[a-z]+$", key));
    }

    [Fact]
    public void Default_IsOneOfTheKeys()
    {
        Assert.Contains(BlockColor.Default, BlockColor.Keys);
    }

    [Theory]
    [InlineData("sage", "sage")]
    [InlineData(" Sage ", "sage")]
    [InlineData("lavender", "lavender")]
    public void Normalize_KnownKey_IsKept(string key, string expected)
    {
        Assert.Equal(expected, BlockColor.Normalize(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("#ff0000")]
    [InlineData("neon")]
    public void Normalize_MissingOrUnknownKey_IsTheDefault(string? key)
    {
        Assert.Equal(BlockColor.Default, BlockColor.Normalize(key));
    }
}
