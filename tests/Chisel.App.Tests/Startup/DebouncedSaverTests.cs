using Chisel.App.Startup;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Startup;

public class DebouncedSaverTests
{
    private static (DebouncedSaver<string> Saver, FakeTimeProvider Time, List<string> Saved) Create()
    {
        var time = new FakeTimeProvider();
        var saved = new List<string>();
        return (new DebouncedSaver<string>(time, TimeSpan.FromMilliseconds(500), saved.Add), time, saved);
    }

    [Fact]
    public void ManyChangesInQuickSuccession_AreSavedOnceAfterTheyStop()
    {
        var (saver, time, saved) = Create();

        saver.Note("a");
        time.Advance(TimeSpan.FromMilliseconds(300));
        saver.Note("b");
        time.Advance(TimeSpan.FromMilliseconds(300));
        saver.Note("c");
        Assert.Empty(saved);

        time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal(["c"], saved);
    }

    [Fact]
    public void Flush_SavesAPendingChangeAtOnceAndOnlyOnce()
    {
        var (saver, time, saved) = Create();

        saver.Note("a");
        saver.Flush();
        time.Advance(TimeSpan.FromSeconds(2));
        saver.Flush();

        Assert.Equal(["a"], saved);
    }
}
