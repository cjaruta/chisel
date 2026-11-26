using System;
using System.IO;
using Chisel.App.Startup;
using Chisel.Infrastructure.Startup;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Startup;

public class CrashHandlerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-crashes-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly List<string> _events = [];

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private CrashHandler Create() => new(new CrashLog(_folder, "1.2.3", _time, @"C:\Users\Test User", "Windows"), _time)
    {
        ShowMessage = (title, _) => _events.Add("show " + title),
        Save = () => _events.Add("save"),
        CleanUp = () => _events.Add("clean up"),
    };

    private int Reports() => Directory.Exists(_folder) ? Directory.GetFiles(_folder, "crash-*.log").Length : 0;

    [Fact]
    public void AUiError_IsReportedSavedAndExplained_AndChiselKeepsRunning()
    {
        var handled = Create().OnUiThread(new InvalidOperationException("Test User's note"));

        Assert.True(handled);
        Assert.Equal(["save", "show Something went wrong"], _events);
        Assert.Equal(1, Reports());
    }

    // An error that repeats (in a timer or every frame) would flood the screen, so after a few in a short time Chisel saves,
    // removes its tray icon and lets the error end the app.
    [Fact]
    public void RepeatedUiErrors_SaveCleanUpAndStopHandling()
    {
        var handler = Create();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(handler.OnUiThread(new InvalidOperationException()));
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        _events.Clear();
        Assert.False(handler.OnUiThread(new InvalidOperationException()));
        Assert.Equal(["save", "clean up"], _events);
    }

    [Fact]
    public void AFatalError_IsReportedThenSavesAndRemovesTheTrayIconOnce()
    {
        var handler = Create();

        handler.OnFatal(new OutOfMemoryException());
        handler.OnFatal(new OutOfMemoryException());

        Assert.Equal(["save", "clean up"], _events);
        Assert.Equal(2, Reports());
    }

    [Fact]
    public void AnUnobservedTask_IsOnlyReported()
    {
        Create().OnUnobservedTask(new AggregateException(new IOException()));

        Assert.Empty(_events);
        Assert.Equal(1, Reports());
    }

    [Fact]
    public void AFailingSave_StillCleansUp()
    {
        var handler = Create();
        handler.Save = () => throw new IOException();

        handler.OnFatal(new InvalidOperationException());

        Assert.Equal(["clean up"], _events);
    }
}
