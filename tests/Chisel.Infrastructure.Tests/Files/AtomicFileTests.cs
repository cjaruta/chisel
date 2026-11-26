using Chisel.Infrastructure.Files;
using Xunit;

namespace Chisel.Infrastructure.Tests.Files;

public class AtomicFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-atomic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Write_CreatesTheFolder_ReplacesTheFile_AndLeavesNoTempFile()
    {
        var path = Path.Combine(_folder, "sub", "data.json");

        AtomicFile.WriteAllText(path, "one");
        AtomicFile.WriteAllText(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.Equal(["data.json"], Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }

    // A crash or failure part way through writes only the temp file, so the saved file is never half written.
    [Fact]
    public void AFailedWrite_LeavesTheOldFileExactlyAsItWas()
    {
        var path = Path.Combine(_folder, "data.json");
        AtomicFile.WriteAllText(path, "old");
        Directory.CreateDirectory(path + ".tmp");

        Assert.ThrowsAny<Exception>(() => AtomicFile.WriteAllText(path, "new"));

        Assert.Equal("old", File.ReadAllText(path));
    }
}
