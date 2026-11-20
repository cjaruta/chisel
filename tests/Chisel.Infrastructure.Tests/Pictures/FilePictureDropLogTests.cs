using Chisel.Infrastructure.Pictures;
using Xunit;

namespace Chisel.Infrastructure.Tests.Pictures;

public class FilePictureDropLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-droplog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Fact]
    public void Lines_AreAppended_WithATimestamp_AndTheFolderIsCreated()
    {
        var path = Path.Combine(_folder, "logs", "picture-drop.log");
        var log = new FilePictureDropLog(path);

        log.Write("first line");
        log.Write("second line");

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} first line$", lines[0]);
        Assert.EndsWith("second line", lines[1]);
    }

    [Fact]
    public void ABigLog_IsTrimmedFromTheOldestLines_AndKeepsTheNewest()
    {
        var path = Path.Combine(_folder, "picture-drop.log");
        var log = new FilePictureDropLog(path, maxBytes: 4000);

        for (var i = 0; i < 200; i++)
        {
            log.Write($"line {i:000} " + new string('x', 40));
        }

        var info = new FileInfo(path);
        Assert.True(info.Length <= 4000, $"{info.Length} bytes");
        var lines = File.ReadAllLines(path);
        Assert.EndsWith("line 199 " + new string('x', 40), lines[^1]);
        Assert.DoesNotContain(lines, l => l.Contains("line 000 "));
        // Whole lines only: the first line kept is a complete one.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} ", lines[0]);
    }

    [Fact]
    public void AMultiLineValue_IsKeptOnOneLine_AndAFailureToWriteNeverThrows()
    {
        var path = Path.Combine(_folder, "picture-drop.log");
        new FilePictureDropLog(path).Write("a\r\nb");
        Assert.Single(File.ReadAllLines(path));

        // A path that cannot be written (a folder where the file should be).
        var blocked = Path.Combine(_folder, "blocked.log");
        Directory.CreateDirectory(blocked);
        new FilePictureDropLog(blocked).Write("nothing happens");
    }
}
