using Chisel.App.Startup;

namespace Chisel.App.Tests.Startup;

public class StartupArgumentsTests
{
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--minimized" }, true)]
    [InlineData(new[] { "--capture", "media", "--MINIMIZED" }, true)]
    [InlineData(new[] { "--minimize" }, false)]
    [InlineData(new[] { "minimized" }, false)]
    public void Minimized_IsReadFromTheSwitch(string[] args, bool expected)
    {
        Assert.Equal(expected, StartupArguments.Parse(args).Minimized);
    }

    [Fact]
    public void NoArguments_MeansANormalStart()
    {
        Assert.False(StartupArguments.Parse(null).Minimized);
    }
}
