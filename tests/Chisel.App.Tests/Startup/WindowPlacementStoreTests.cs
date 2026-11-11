using System;
using System.IO;
using Chisel.App.Startup;
using Xunit;

namespace Chisel.App.Tests.Startup;

public class WindowPlacementStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ChiselWindowPlacementTests_" + Guid.NewGuid());

    [Fact]
    public void Load_OnMissingFile_ReturnsNull()
    {
        var path = Path.Combine(_directory, "window.json");

        var placement = WindowPlacementStore.Load(path);

        Assert.Null(placement);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheValues()
    {
        var path = Path.Combine(_directory, "window.json");
        var saved = new WindowPlacement(1024, 768, 100, 50);

        WindowPlacementStore.Save(path, saved);
        var loaded = WindowPlacementStore.Load(path);

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void Load_OnCorruptFile_ReturnsNullInsteadOfThrowing()
    {
        var path = Path.Combine(_directory, "window.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ not valid json");

        var placement = WindowPlacementStore.Load(path);

        Assert.Null(placement);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
