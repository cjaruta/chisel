using System;
using System.IO;
using Chisel.App.Startup;
using Chisel.Infrastructure.Startup;

namespace Chisel.App.Tests.Startup;

public class AppStartupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chisel-startup-" + Guid.NewGuid());

    public AppStartupTests() => Directory.CreateDirectory(_dir);

    private string LogsFolder => Path.Combine(_dir, "logs");

    private CrashLog Log() => new(LogsFolder, "1.2.3", profilePath: @"C:\Users\Test User", os: "Windows");

    private string[] Reports() => Directory.Exists(LogsFolder) ? Directory.GetFiles(LogsFolder, "crash-*.log") : [];

    [Fact]
    public void Run_WhenFactorySucceeds_ReturnsValueAndWritesNoLog()
    {
        var result = AppStartup.Run(() => "ok", Log());

        Assert.Equal("ok", result.Value);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(Reports());
    }

    [Fact]
    public void Run_WhenFactoryThrows_WritesAReportAndNamesIt()
    {
        var result = AppStartup.Run<string>(() => throw new InvalidOperationException("boom"), Log());

        Assert.Null(result.Value);
        var report = Assert.Single(Reports());
        Assert.Contains(report, result.ErrorMessage);
        Assert.Contains("System.InvalidOperationException", File.ReadAllText(report));
    }

    [Fact]
    public void Run_WhenLogCannotBeWritten_StillReturnsAMessageInsteadOfThrowing()
    {
        var blocker = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocker, "");

        var result = AppStartup.Run<string>(() => throw new InvalidOperationException("boom"),
            new CrashLog(Path.Combine(blocker, "logs"), "1.2.3"));

        Assert.Null(result.Value);
        Assert.Contains("boom", result.ErrorMessage);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
