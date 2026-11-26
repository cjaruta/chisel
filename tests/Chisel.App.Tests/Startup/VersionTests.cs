using System.Reflection;

namespace Chisel.App.Tests.Startup;

// The version is set once (Directory.Build.props); the assembly, file and product versions all come from it, with no
// build hash appended, so Windows file properties, Settings > Help and crash reports all show the same number.
public class VersionTests
{
    [Fact]
    public void AssemblyFileAndProductVersions_AllMatch()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly.GetName().Version!;

        Assert.Equal(version.ToString(4), assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        Assert.Equal(version.ToString(3), assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
    }
}
