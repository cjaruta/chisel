using Chisel.App.Startup;
using Chisel.App.Tests.Support;

namespace Chisel.App.Tests.Startup;

public class StartWithWindowsTests
{
    private const string Exe = @"C:\Program Files\Chisel\Chisel.App.exe";
    private const string Expected = "\"" + Exe + "\" --minimized";
    private readonly FakeStartupRegistry _registry = new();

    private StartWithWindows Startup(string? exe = Exe) => new(_registry, exe);

    [Fact]
    public void TurningOn_WritesTheQuotedProgramPathWithTheMinimizedSwitch_AndOffRemovesIt()
    {
        var startup = Startup();
        Assert.Null(startup.SetEnabled(true));
        Assert.Equal(Expected, _registry.Values[StartWithWindows.ValueName]);
        Assert.True(startup.Read().IsOn);

        Assert.Null(startup.SetEnabled(false));
        Assert.Empty(_registry.Values);
        Assert.False(startup.Read().IsOn);
    }

    [Fact]
    public void Read_ReflectsTheRegistry_NotWhatWasLastSet()
    {
        _registry.Values[StartWithWindows.ValueName] = @"""D:\old\Chisel.App.exe"" --minimized";
        Assert.True(Startup().Read().IsOn);
    }

    [Fact]
    public void Revalidate_PointsTheEntryAtTheCurrentProgram_WhenItMoved()
    {
        _registry.Values[StartWithWindows.ValueName] = @"""D:\old\Chisel.App.exe"" --minimized";
        Startup().Revalidate();
        Assert.Equal(Expected, _registry.Values[StartWithWindows.ValueName]);
    }

    [Fact]
    public void Revalidate_LeavesThingsAlone_WhenOffOrAlreadyRight()
    {
        Startup().Revalidate();
        Assert.Empty(_registry.Values);

        _registry.Values[StartWithWindows.ValueName] = Expected.ToUpperInvariant();
        Startup().Revalidate();
        Assert.Equal(0, _registry.Writes);
    }

    [Fact]
    public void ABlockedRegistry_GivesAClearMessage_InsteadOfThrowing()
    {
        _registry.Blocked = true;
        var startup = Startup();
        Assert.Contains("policy", startup.SetEnabled(true));
        var state = startup.Read();
        Assert.False(state.IsOn);
        Assert.Contains("policy", state.Error);
        Assert.Contains("policy", startup.Revalidate());
    }

    [Fact]
    public void WithoutAKnownProgramPath_ItCannotBeTurnedOn()
    {
        Assert.NotNull(Startup(exe: null).SetEnabled(true));
        Assert.Empty(_registry.Values);
    }
}
