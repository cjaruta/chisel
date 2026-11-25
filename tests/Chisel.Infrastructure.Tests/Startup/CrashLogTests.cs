using Chisel.Infrastructure.Startup;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.Infrastructure.Tests.Startup;

public class CrashLogTests : IDisposable
{
    private const string Profile = @"C:\Users\Test User";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-crash-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 12, 30, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    // An exception whose stack trace is given, so the test controls the paths in it.
    private sealed class StackException(string message, string stack, Exception? inner = null) : Exception(message, inner)
    {
        public override string StackTrace => stack;
    }

    private CrashLog Log(int keep = 5, int maxBytes = 64 * 1024) =>
        new(_folder, "1.2.3", _time, Profile, "Windows 10.0.26100", keep, maxBytes);

    [Fact]
    public void Format_HasTimeVersionOsTypeAndStack()
    {
        var text = CrashLog.Format(new StackException("x", "   at Chisel.App.Thing.Run()"), "UI thread", "1.2.3", "Windows 10.0.26100",
            _time.GetUtcNow(), Profile);

        Assert.Contains("2000-01-01 12:30:00 UTC", text);
        Assert.Contains("Chisel 1.2.3", text);
        Assert.Contains("Windows 10.0.26100", text);
        Assert.Contains("UI thread", text);
        Assert.Contains(typeof(StackException).FullName!, text);
        Assert.Contains("at Chisel.App.Thing.Run()", text);
    }

    // Exception messages can quote what the user typed (a note, a task, a pasted value), so they are never written.
    [Fact]
    public void Format_NeverWritesExceptionMessages()
    {
        var error = new StackException("Buy milk for Test User", "   at A.B()", new FormatException("The input 'my secret note' was bad"));

        var text = CrashLog.Format(error, "UI thread", "1.2.3", "Windows", _time.GetUtcNow(), Profile);

        Assert.DoesNotContain("milk", text);
        Assert.DoesNotContain("secret", text);
        Assert.Contains("System.FormatException", text);
    }

    [Fact]
    public void Format_RemovesTheProfilePathAndSourceFolders()
    {
        var stack = @"   at A.B() in C:\Users\Test User\source\repos\Chisel\src\A.cs:line 12" + "\n"
            + @"   at C.D() in D:\build\agent\work\C.cs:line 3" + "\n"
            + @"   at E.F(String path = C:\Users\test user\Documents\x.txt)" + "\n"
            + @"   at G.H() C:\Users\Someone Else\AppData\x";

        var text = CrashLog.Format(new StackException("x", stack), "UI thread", "1.2.3", "Windows", _time.GetUtcNow(), Profile);

        Assert.Contains("at A.B() in A.cs:line 12", text);
        Assert.Contains("at C.D() in C.cs:line 3", text);
        Assert.Contains(@"%USERPROFILE%\Documents\x.txt", text);
        Assert.DoesNotContain("Test User", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Someone Else", text);
        Assert.DoesNotContain("source", text);
    }

    [Fact]
    public void Write_KeepsOnlyTheNewestLogs()
    {
        var log = Log(keep: 3);

        for (var i = 0; i < 6; i++)
        {
            log.Write(new StackException("x", "   at A.B()"), "task");
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(["crash-20000101-123003.log", "crash-20000101-123004.log", "crash-20000101-123005.log"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Write_CapsTheFileSize()
    {
        var log = Log(maxBytes: 4096);
        var stack = string.Concat(Enumerable.Repeat("   at Some.Deep.Recursion.Frame()\n", 2000));

        var path = log.Write(new StackException("x", stack), "UI thread");

        Assert.InRange(new FileInfo(path!).Length, 1, 4096);
    }

    [Fact]
    public void Write_NeverThrows_WhenTheFolderCannotBeMade()
    {
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "chisel-crash-blocker.txt"), "");
        var log = new CrashLog(Path.Combine(Path.GetTempPath(), "chisel-crash-blocker.txt", "logs"), "1.2.3", _time, Profile, "Windows");

        Assert.Null(log.Write(new InvalidOperationException(), "UI thread"));
    }
}
