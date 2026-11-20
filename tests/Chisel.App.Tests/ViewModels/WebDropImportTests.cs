using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.Core.Pictures;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// A drop from a browser is several candidate addresses for one picture: they are tried in order, a candidate that is a
// page or an error is skipped, and when none works the most useful message is shown.
public class WebDropImportTests
{
    private sealed class MemoryLog : IPictureDropLog
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private readonly PictureServicesFixture _pictures = new();
    private readonly MemoryLog _log = new();

    private PictureImportService Service() =>
        new(_pictures.Importer(new FakeTimeProvider()), _pictures.Downloader, null, _log);

    private static WebSource Web(params string[] addresses) =>
        new(addresses.Select(a => new PictureCandidate(a, "https://example.com/page")).ToList());

    private static DownloadResult Image(int size = 20) => new(TinyFiles.Png(size, size), FileName: "x.png", Status: 200, ContentType: "image/png");

    private static DownloadResult WebPage() =>
        new(null, DownloadError.WebPage, "That link is a web page, not an image", Status: 200, ContentType: "text/html");

    private static DownloadResult Refused() =>
        new(null, DownloadError.Refused, "The site refused the download (403)", Status: 403);

    [Fact]
    public async Task TheFirstCandidateThatIsAnImage_Wins_AndALinkToAPageIsSkipped()
    {
        _pictures.Downloader.Answers["https://example.com/page"] = WebPage();
        _pictures.Downloader.Answers["https://cdn.example.com/cat.png"] = Image();

        var result = await Service().PrepareAsync(Web("https://example.com/page", "https://cdn.example.com/cat.png"), true);

        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://example.com/page", "https://cdn.example.com/cat.png" }, _pictures.Downloader.Asked);
        Assert.Equal("https://cdn.example.com/cat.png", result.Picture!.SourceUrl);
    }

    [Fact]
    public async Task AnEarlierImage_StopsTheSearch()
    {
        _pictures.Downloader.Answers["https://cdn.example.com/a.png"] = Image(21);
        _pictures.Downloader.Answers["https://cdn.example.com/b.png"] = Image(22);

        var result = await Service().PrepareAsync(Web("https://cdn.example.com/a.png", "https://cdn.example.com/b.png"), true);

        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://cdn.example.com/a.png" }, _pictures.Downloader.Asked);
    }

    [Fact]
    public async Task ARefusedCandidate_IsSkipped_AndTheRefererOfEachCandidateIsPassedOn()
    {
        _pictures.Downloader.Answers["https://cdn.example.com/a.png"] = Refused();
        _pictures.Downloader.Answers["https://cdn.example.com/b.png"] = Image();

        var result = await Service().PrepareAsync(Web("https://cdn.example.com/a.png", "https://cdn.example.com/b.png"), true);

        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://example.com/page", "https://example.com/page" }, _pictures.Downloader.Referers);
    }

    [Fact]
    public async Task WhenEveryCandidateFails_TheMostUsefulMessageIsShown()
    {
        _pictures.Downloader.Answers["https://example.com/page"] = WebPage();
        _pictures.Downloader.Answers["https://cdn.example.com/a.png"] = Refused();

        var result = await Service().PrepareAsync(Web("https://example.com/page", "https://cdn.example.com/a.png"), true);

        Assert.Null(result.Picture);
        Assert.Equal("The site refused the download (403)", result.Error);
    }

    [Fact]
    public async Task AnUnsupportedFormat_BeatsAWebPage_AndANotAnImageBeatsNothing()
    {
        _pictures.Downloader.Answers["https://example.com/page"] = WebPage();
        _pictures.Downloader.Answers["https://cdn.example.com/a.avif"] =
            new DownloadResult(null, DownloadError.Unsupported, "AVIF and other formats are not supported", Status: 200, ContentType: "image/avif");

        var result = await Service().PrepareAsync(Web("https://example.com/page", "https://cdn.example.com/a.avif"), true);
        Assert.Equal("AVIF and other formats are not supported", result.Error);

        var unknown = await Service().PrepareAsync(Web("https://example.com/unanswered"), true);
        Assert.False(string.IsNullOrWhiteSpace(unknown.Error));
    }

    [Fact]
    public async Task ADownloadedFileThatIsTooBigInPixels_IsReportedAsThat_NotAsNotAnImage()
    {
        _pictures.Downloader.Answers["https://cdn.example.com/huge.png"] = new DownloadResult(TinyFiles.Png(13000, 100), FileName: "huge.png", Status: 200, ContentType: "image/png");

        var result = await Service().PrepareAsync(Web("https://cdn.example.com/huge.png"), true);

        Assert.Null(result.Picture);
        Assert.Contains("12000", result.Error);
    }

    [Fact]
    public async Task ADataImageCandidate_NeedsNoDownloadFromTheFakeButIsAskedLikeAnyOther()
    {
        var data = "data:image/png;base64," + Convert.ToBase64String(TinyFiles.Png(30, 30));
        _pictures.Downloader.Answers[data] = Image(30);

        var result = await Service().PrepareAsync(Web(data), true);

        Assert.NotNull(result.Picture);
        // A data address is not a source URL worth keeping.
        Assert.Null(result.Picture!.SourceUrl);
    }

    [Fact]
    public async Task EveryCandidateAndItsResult_IsLogged()
    {
        _pictures.Downloader.Answers["https://example.com/page"] = WebPage();
        _pictures.Downloader.Answers["https://cdn.example.com/cat.png"] = Image();

        await Service().PrepareAsync(Web("https://example.com/page", "https://cdn.example.com/cat.png"), true);

        var text = string.Join("\n", _log.Lines);
        Assert.Contains("candidate 1/2 https://example.com/page", text);
        Assert.Contains("That link is a web page, not an image", text);
        Assert.Contains("candidate 2/2 https://cdn.example.com/cat.png", text);
        Assert.Contains("result=ok", text);
    }

    [Fact]
    public async Task FilesAndBitmaps_StillWorkAsBefore()
    {
        var service = Service();

        var bitmap = await service.PrepareAsync(new BytesSource(TinyFiles.Png(10, 10), "picture.png"), true);
        var notImage = await service.PrepareAsync(new BytesSource("<html><body>hi</body></html>"u8.ToArray(), "page.html"), true);

        Assert.NotNull(bitmap.Picture);
        Assert.Equal("That link is a web page, not an image", notImage.Error);
    }
}
