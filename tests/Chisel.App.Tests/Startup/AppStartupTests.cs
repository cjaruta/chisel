using System;
using System.IO;
using Chisel.App.Startup;

namespace Chisel.App.Tests.Startup;

public class AppStartupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chisel-startup-" + Guid.NewGuid());

    public AppStartupTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void Run_WhenFactorySucceeds_ReturnsValueAndWritesNoLog()
    {
        var logPath = Path.Combine(_dir, "error.log");

        var result = AppStartup.Run(() => "ok", logPath);

        Assert.Equal("ok", result.Value);
        Assert.Null(result.ErrorMessage);
        Assert.False(File.Exists(logPath));
    }

    [Fact]
    public void Run_WhenFactoryThrows_LogsAndReturnsMessageNamingTheLogFile()
    {
        var logPath = Path.Combine(_dir, "error.log");

        var result = AppStartup.Run<string>(() => throw new InvalidOperationException("boom"), logPath);

        Assert.Null(result.Value);
        Assert.Contains(logPath, result.ErrorMessage);
        Assert.Contains("boom", File.ReadAllText(logPath));
    }

    [Fact]
    public void Run_WhenLogCannotBeWritten_StillReturnsAMessageInsteadOfThrowing()
    {
        var blocker = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocker, "");
        var logPath = Path.Combine(blocker, "error.log");

        var result = AppStartup.Run<string>(() => throw new InvalidOperationException("boom"), logPath);

        Assert.Null(result.Value);
        Assert.Contains("boom", result.ErrorMessage);
    }

    [Fact]
    public void Run_WithCorruptDatabaseFile_ReportsAnErrorAndLogsNextToTheDatabase()
    {
        var dbPath = Path.Combine(_dir, "chisel.db");
        File.WriteAllText(dbPath, "this is not a sqlite database, just some text padding it out a bit more");
        var logPath = Path.Combine(_dir, "error.log");

        var result = AppStartup.Run(() => new AppServices(dbPath), logPath);

        Assert.Null(result.Value);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(File.Exists(logPath));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
