using System;
using System.IO;
using Chisel.Infrastructure.Startup;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.Infrastructure.Tests.Startup;

public class ErrorLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ChiselErrorLoggerTests_" + Guid.NewGuid());

    [Fact]
    public void Log_WritesTimestampAndExceptionDetailsToTheFile()
    {
        var path = Path.Combine(_directory, "error.log");
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero));
        var exception = new InvalidOperationException("Database is locked.");

        ErrorLogger.Log(path, exception, time);

        var contents = File.ReadAllText(path);
        Assert.Contains("2026-11-10", contents);
        Assert.Contains("Database is locked.", contents);
    }

    [Fact]
    public void Log_CalledTwice_AppendsRatherThanOverwriting()
    {
        var path = Path.Combine(_directory, "error.log");
        var time = new FakeTimeProvider();

        ErrorLogger.Log(path, new Exception("First"), time);
        ErrorLogger.Log(path, new Exception("Second"), time);

        var contents = File.ReadAllText(path);
        Assert.Contains("First", contents);
        Assert.Contains("Second", contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
