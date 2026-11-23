using Chisel.App.Services;

namespace Chisel.App.Tests.Services;

public class TrackArtLoaderTests
{
    private static readonly byte[] ArtA = [1, 2, 3];
    private static readonly byte[] ArtB = [4, 5, 6];

    private static TrackIdentity Track(string title, string album = "Album") => new("app", title, "Artist", album);

    // Delays complete only when the test releases them, so retries are driven by hand.
    private sealed class Clock
    {
        public readonly List<(TimeSpan Delay, TaskCompletionSource Gate)> Waiting = [];
        public Task Delay(TimeSpan delay, CancellationToken ct)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() => gate.TrySetCanceled(ct));
            Waiting.Add((delay, gate));
            return gate.Task;
        }
        public async Task Tick()
        {
            var next = Waiting.First(w => !w.Gate.Task.IsCompleted);
            next.Gate.SetResult();
            await Task.Yield();
            await Task.Delay(20);
        }
    }

    private static async Task Settle() => await Task.Delay(30);

    [Fact]
    public async Task FirstTrackWithDelayedThumbnail_EventuallyShowsItsArt()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        byte[]? thumbnail = null;
        loader.Observe(Track("One"), _ => Task.FromResult(thumbnail), propertiesChanged: true);
        await Settle();
        Assert.Null(loader.ArtFor(Track("One")));

        thumbnail = ArtA;
        await clock.Tick();

        Assert.Equal(ArtA, loader.ArtFor(Track("One")));
    }

    [Fact]
    public async Task TrackChange_NeverShowsTheOldTracksArt()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        loader.Observe(Track("One", "Album 1"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();
        Assert.Equal(ArtA, loader.ArtFor(Track("One", "Album 1")));

        // The new track still reports the old thumbnail.
        loader.Observe(Track("Two", "Album 2"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();

        Assert.Null(loader.ArtFor(Track("One", "Album 1")));
        Assert.Null(loader.ArtFor(Track("Two", "Album 2")));

        // The real art shows up later.
        await clock.Tick();
        Assert.Null(loader.ArtFor(Track("Two", "Album 2")));
    }

    [Fact]
    public async Task TrackChange_ShowsNewArtOnceItArrives()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        loader.Observe(Track("One", "Album 1"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();
        byte[] next = ArtA;
        loader.Observe(Track("Two", "Album 2"), _ => Task.FromResult<byte[]?>(next), true);
        await Settle();
        next = ArtB;
        await clock.Tick();

        Assert.Equal(ArtB, loader.ArtFor(Track("Two", "Album 2")));
    }

    [Fact]
    public async Task StaleResult_ForAnEarlierTrack_IsIgnored()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        var slow = new TaskCompletionSource<byte[]?>();
        loader.Observe(Track("One", "Album 1"), _ => slow.Task, true);
        loader.Observe(Track("Two", "Album 2"), _ => Task.FromResult<byte[]?>(ArtB), true);
        await Settle();

        slow.SetResult(ArtA); // the first track's load finishes after the track changed
        await Settle();

        Assert.Equal(ArtB, loader.ArtFor(Track("Two", "Album 2")));
        Assert.Null(loader.ArtFor(Track("One", "Album 1")));
    }

    [Fact]
    public async Task NullThumbnail_RetriesThenFallsBackToNoArt()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        var calls = 0;
        loader.Observe(Track("One"), _ => { calls++; return Task.FromResult<byte[]?>(null); }, true);
        await Settle();

        for (var i = 0; i < 4; i++)
        {
            await clock.Tick();
        }
        await Settle();

        Assert.Equal(5, calls); // the first try plus four retries
        Assert.Equal(
            new[] { 250, 500, 1000, 2000 },
            clock.Waiting.Select(w => (int)w.Delay.TotalMilliseconds).ToArray());
        Assert.Null(loader.ArtFor(Track("One")));
    }

    [Fact]
    public async Task SameAlbumNextTrack_KeepsTheSharedArtWithoutRetrying()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        loader.Observe(Track("One"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();
        loader.Observe(Track("Two"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();

        Assert.Equal(ArtA, loader.ArtFor(Track("Two")));
        Assert.Empty(clock.Waiting);
    }

    [Fact]
    public async Task LaterPropertyChange_ForTheSameTrack_PicksUpLateArt()
    {
        var clock = new Clock();
        using var loader = new TrackArtLoader(clock.Delay);
        loader.Observe(Track("One"), _ => Task.FromResult<byte[]?>(ArtA), true);
        await Settle();

        loader.Observe(Track("One"), _ => Task.FromResult<byte[]?>(ArtB), propertiesChanged: true);
        await Settle();

        Assert.Equal(ArtB, loader.ArtFor(Track("One")));
    }
}
