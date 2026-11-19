using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// What happens after pictures are dropped: frames fill in as each picture is read, checked and stored, a failure shows
// a short message and removes a frame that has nothing, and the options for several pictures and original files apply.
public class PictureDropImportTests : IDisposable
{
    private sealed class FakeSettings : IUserSettingsRepository
    {
        public UserSettings Settings { get; set; } = new("Test User", new TimeOnly(7, 0), new TimeOnly(23, 0));
        public UserSettings? Get() => Settings;
        public void Save(UserSettings settings) => Settings = settings;
    }

    private readonly PictureServicesFixture _pictures = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 9, 30, 0, TimeSpan.Zero));
    private readonly LayoutViewModel _layout;
    private readonly PictureFramesViewModel _frames;
    private readonly List<string> _temp = [];

    public PictureDropImportTests()
    {
        _layout = new LayoutViewModel(null, _pictures.Catalog);
        _layout.EnterEditCommand.Execute(null);
        _frames = new PictureFramesViewModel(_layout, _time,
            import: new PictureImportService(_pictures.Importer(_time), _pictures.Downloader, _settings));
    }

    public void Dispose()
    {
        foreach (var file in _temp.Where(File.Exists))
        {
            File.Delete(file);
        }
    }

    private static PictureSource Bytes(int width, int height = 10) => new BytesSource(TinyFiles.Png(width, height), $"p{width}.png");

    private PictureDropPlacement DropIntoGap(int sources)
    {
        _layout.BeginPictureDrag();
        return _layout.DropPictures(PictureDropKind.Gap, 1, 1, _frames.FramesFor(sources))!;
    }

    private IReadOnlyList<Guid> Assets(string frame) => _layout.Pictures.Get(frame).Pictures.Select(p => p.AssetId).ToList();

    [Fact]
    public async Task OnePicture_MakesOneFrameThatFillsIn()
    {
        var placement = DropIntoGap(1);

        await _frames.DropAsync(placement, [Bytes(20)]);

        Assert.Single(Assets(placement.FrameIds[0]));
        Assert.Equal(0, _frames.For(placement.FrameIds[0]).Pending);
        Assert.Null(_frames.Notice);
    }

    [Fact]
    public async Task TheFrameShowsAsLoading_UntilThePictureArrives()
    {
        var gate = new TaskCompletionSource<DownloadResult>();
        var downloader = new GatedDownloader(gate.Task);
        var frames = new PictureFramesViewModel(_layout, _time, import: new PictureImportService(_pictures.Importer(_time), downloader, _settings));
        _layout.BeginPictureDrag();
        var placement = _layout.DropPictures(PictureDropKind.Gap, 1, 1, 1)!;

        var running = frames.DropAsync(placement, [new UrlSource("https://example.com/a.png")]);

        var frame = frames.For(placement.FrameIds[0]);
        Assert.Equal(1, frame.Pending);
        Assert.Contains(placement.FrameIds[0], _layout.Layout.WidgetIds);
        gate.SetResult(new DownloadResult(TinyFiles.Png(30, 30), FileName: "a.png"));
        await running;
        Assert.Equal(0, frame.Pending);
        Assert.Single(Assets(placement.FrameIds[0]));
    }

    private sealed class GatedDownloader(Task<DownloadResult> gate) : IImageDownloader
    {
        public Task<DownloadResult> DownloadAsync(string url, CancellationToken cancellation = default) => gate;
    }

    [Fact]
    public async Task SeveralPictures_ByDefault_MakeOneFrameWithASlideshow()
    {
        var placement = DropIntoGap(4);

        await _frames.DropAsync(placement, [Bytes(20), Bytes(21), Bytes(22), Bytes(23)]);

        Assert.Single(placement.FrameIds);
        Assert.Equal(4, Assets(placement.FrameIds[0]).Count);
    }

    [Fact]
    public async Task SeveralPictures_CanMakeOneFrameEach()
    {
        _settings.Settings = _settings.Settings.WithPictureMultiDrop(MultiDropMode.OneFramePerPicture);
        var placement = DropIntoGap(3);

        await _frames.DropAsync(placement, [Bytes(20), Bytes(21), Bytes(22)]);

        Assert.Equal(3, placement.FrameIds.Count);
        Assert.All(placement.FrameIds, id => Assert.Single(Assets(id)));
        Assert.Equal(placement.FrameIds, _layout.Layout.Columns[1].Widgets.Skip(1).Take(3).Select(w => w.WidgetId));
    }

    [Fact]
    public async Task MoreThanEightPictures_MakeEightFrames_AndSaySo()
    {
        _settings.Settings = _settings.Settings.WithPictureMultiDrop(MultiDropMode.OneFramePerPicture);
        var sources = Enumerable.Range(20, 11).Select(i => Bytes(i)).ToList();
        var placement = DropIntoGap(sources.Count);

        await _frames.DropAsync(placement, sources);

        Assert.Equal(8, placement.FrameIds.Count);
        Assert.Contains("8", _frames.Notice);
        Assert.Equal(8, _pictures.Repository.Stored.Count);
    }

    [Fact]
    public async Task ADropIntoAnExistingFrame_AddsToItsSlideshow()
    {
        var first = DropIntoGap(1);
        await _frames.DropAsync(first, [Bytes(20)]);
        _layout.BeginPictureDrag();
        var (column, index) = _layout.Layout.Find(first.FrameIds[0])!.Value;
        var placement = _layout.DropPictures(PictureDropKind.Card, column, index, _frames.FramesFor(2))!;

        await _frames.DropAsync(placement, [Bytes(21), Bytes(22)]);

        Assert.False(placement.CreatedFrames);
        Assert.Equal(3, Assets(first.FrameIds[0]).Count);
    }

    [Fact]
    public async Task APictureThatFails_ShowsAMessage_AndAFrameLeftEmptyGoesAway()
    {
        var placement = DropIntoGap(1);
        var before = _layout.Layout.WidgetIds.Count();

        await _frames.DropAsync(placement, [new UrlSource("https://example.com/missing.png")]);

        Assert.Contains("Couldn't download", _frames.Notice);
        Assert.DoesNotContain(placement.FrameIds[0], _layout.Layout.WidgetIds);
        Assert.Equal(before - 1, _layout.Layout.WidgetIds.Count());
        Assert.Empty(_pictures.Repository.Stored);
    }

    [Fact]
    public async Task OneBadPictureAmongGoodOnes_KeepsTheFrame_AndSaysWhichFailed()
    {
        var placement = DropIntoGap(2);

        await _frames.DropAsync(placement, [Bytes(20), new BytesSource("<svg/>"u8.ToArray(), "logo.svg")]);

        Assert.Single(Assets(placement.FrameIds[0]));
        Assert.Contains("logo.svg", _frames.Notice);
        Assert.Contains("PNG, JPEG, GIF or WebP", _frames.Notice);
        Assert.Contains(placement.FrameIds[0], _layout.Layout.WidgetIds);
    }

    [Fact]
    public async Task TheSamePictureTwice_IsStoredOnce_AndListedOnce()
    {
        var placement = DropIntoGap(2);

        await _frames.DropAsync(placement, [Bytes(20), Bytes(20)]);

        Assert.Single(_pictures.Repository.Stored);
        Assert.Single(Assets(placement.FrameIds[0]));
    }

    [Fact]
    public async Task AnAddressIsDownloaded_AndAFileIsRead()
    {
        _pictures.Downloader.Answers["https://example.com/a.png"] = new DownloadResult(TinyFiles.Png(31, 31), FileName: "a.png");
        var path = Path.Combine(Path.GetTempPath(), "chisel-drop-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, TinyFiles.Png(32, 32));
        _temp.Add(path);
        var placement = DropIntoGap(2);

        await _frames.DropAsync(placement, [new UrlSource("https://example.com/a.png"), new FileSource(path)]);

        Assert.Equal(new[] { "https://example.com/a.png" }, _pictures.Downloader.Asked);
        Assert.Equal(2, Assets(placement.FrameIds[0]).Count);
        Assert.Equal("https://example.com/a.png", _pictures.Repository.Stored.First().SourceUrl);
    }

    [Fact]
    public async Task AFileThatIsMissingOrTooBig_FailsWithAMessage()
    {
        var placement = DropIntoGap(1);

        await _frames.DropAsync(placement, [new FileSource(Path.Combine(Path.GetTempPath(), "chisel-not-here.png"))]);

        Assert.NotNull(_frames.Notice);
        Assert.Empty(_pictures.Repository.Stored);
    }

    [Fact]
    public async Task KeepOriginalsOff_StoresADisplaySizeCopyOfABigPicture()
    {
        _settings.Settings = _settings.Settings.WithPictureKeepOriginals(false);
        var placement = DropIntoGap(1);

        await _frames.DropAsync(placement, [new BytesSource(TinyFiles.Png(6000, 4000), "big.png")]);

        var asset = _pictures.Repository.Stored.Single();
        Assert.Equal((2048, 1365), (asset.Width, asset.Height));
    }

    [Fact]
    public async Task TheMessage_GoesAwayByItself()
    {
        var placement = DropIntoGap(1);
        await _frames.DropAsync(placement, [new UrlSource("https://example.com/missing.png")]);
        Assert.NotNull(_frames.Notice);

        _time.Advance(TimeSpan.FromSeconds(3));
        _frames.Tick();
        Assert.NotNull(_frames.Notice);
        _time.Advance(TimeSpan.FromSeconds(6));
        _frames.Tick();

        Assert.Null(_frames.Notice);
    }

    [Fact]
    public async Task APictureThatArrivesAfterTheFrameWasCancelled_IsNotAdded()
    {
        var gate = new TaskCompletionSource<DownloadResult>();
        var frames = new PictureFramesViewModel(_layout, _time, import: new PictureImportService(_pictures.Importer(_time), new GatedDownloader(gate.Task), _settings));
        _layout.BeginPictureDrag();
        var placement = _layout.DropPictures(PictureDropKind.Gap, 1, 1, 1)!;
        var running = frames.DropAsync(placement, [new UrlSource("https://example.com/a.png")]);

        _layout.CancelCommand.Execute(null);
        gate.SetResult(new DownloadResult(TinyFiles.Png(30, 30), FileName: "a.png"));
        await running;

        Assert.False(_layout.Pictures.Has(placement.FrameIds[0]));
    }
}
