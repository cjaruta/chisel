using System.Text;
using Avalonia.Input;
using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.Core.Pictures;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// A drag from Chromium on Windows: a virtual file first, then the HTML (CF_HTML) with its img src, then addresses unwrapped from
// image-search links, then the links themselves. Built from fake payloads that carry the same formats.
public class BrowserDropTests
{
    private sealed class MemoryLog : IPictureDropLog
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private sealed class FakeVirtualFiles(VirtualFile? file) : IVirtualFiles
    {
        public Func<CancellationToken, Task<VirtualFile?>>? Open(IDataTransfer data, IPictureDropLog? log) => _ => Task.FromResult(file);
    }

    private readonly PictureServicesFixture _pictures = new();
    private readonly MemoryLog _log = new();

    private PictureImportService Service() => new(_pictures.Importer(new FakeTimeProvider()), _pictures.Downloader, null, _log);

    private static DataTransfer Drop(params (string Format, byte[] Bytes)[] formats)
    {
        var data = new DataTransfer();
        foreach (var (format, bytes) in formats)
        {
            var item = new DataTransferItem();
            item.Set(DataFormat.CreateBytesPlatformFormat(format), bytes);
            data.Add(item);
        }
        return data;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text + "\0");

    private static byte[] CfHtml(string sourceUrl, string fragment) =>
        Utf8($"Version:0.9\r\nStartHTML:0000000000\r\nEndHTML:0000000000\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\nSourceURL:{sourceUrl}\r\n<html><body><!--StartFragment-->{fragment}<!--EndFragment--></body></html>");

    private static byte[] Descriptor(string name)
    {
        var bytes = new byte[4 + 592];
        BitConverter.GetBytes(1).CopyTo(bytes, 0);
        Encoding.Unicode.GetBytes(name).CopyTo(bytes, 4 + 72);
        return bytes;
    }

    private static string[] Addresses(IReadOnlyList<PictureSource> sources) =>
        sources.Cast<WebSource>().Single().Candidates.Select(c => c.Address).ToArray();

    private static DownloadResult Image(int size = 20) => new(TinyFiles.Png(size, size), FileName: "x.png", Status: 200, ContentType: "image/png");

    private static DownloadResult WebPage() => new(null, DownloadError.WebPage, "That link is a web page, not an image", Status: 200, ContentType: "text/html");

    [Fact]
    public async Task AVirtualFile_Wins_AndNothingIsDownloaded()
    {
        var data = Drop(("FileGroupDescriptorW", Descriptor("cat.png")), ("UniformResourceLocatorW", Utf16("https://example.com/article")));
        var files = new FakeVirtualFiles(new VirtualFile(TinyFiles.Png(30, 30), "cat.png"));

        var source = Assert.IsType<DeferredFileSource>(Assert.Single(PictureDropPayload.Read(data, _log, files)));
        var result = await Service().PrepareAsync(source, true);

        Assert.NotNull(result.Picture);
        Assert.Empty(_pictures.Downloader.Asked);
    }

    [Fact]
    public async Task AVirtualFileThatIsNotAPicture_FallsBackToTheAddresses()
    {
        var data = Drop(("FileGroupDescriptorW", Descriptor("link.url")), ("HTML Format", CfHtml("https://example.com/p", "<img src=\"https://cdn.example.com/cat.png\">")));
        _pictures.Downloader.Answers["https://cdn.example.com/cat.png"] = Image();
        var files = new FakeVirtualFiles(new VirtualFile(Utf8("[InternetShortcut]"), "link.url"));

        var result = await Service().PrepareAsync(Assert.Single(PictureDropPayload.Read(data, _log, files)), true);

        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://cdn.example.com/cat.png" }, _pictures.Downloader.Asked);
    }

    [Fact]
    public void HtmlFormat_GivesItsImgSrc()
    {
        var data = Drop(("HTML Format", CfHtml("https://example.com/p", "<img src=\"https://cdn.example.com/cat.png\">")));

        Assert.Equal(new[] { "https://cdn.example.com/cat.png" }, Addresses(PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null))));
    }

    [Fact]
    public void ARelativeSrc_IsResolvedAgainstTheSourceUrl()
    {
        var data = Drop(("HTML Format", CfHtml("https://example.com/gallery/index.html", "<img src=\"../img/cat.jpg\">")));

        var source = Assert.IsType<WebSource>(Assert.Single(PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null))));

        Assert.Equal("https://example.com/img/cat.jpg", source.Candidates[0].Address);
        Assert.Equal("https://example.com/gallery/index.html", source.Candidates[0].Referer);
    }

    [Fact]
    public async Task AGoogleImgresLink_TriesItsImgurlFirst_AndNeverFetchesTheLinkWhenThatWins()
    {
        var link = "https://www.google.com/imgres?imgurl=https%3A%2F%2Fcdn.example.com%2Fcat.jpg&imgrefurl=https%3A%2F%2Fexample.com%2Farticle";
        var data = Drop(("UniformResourceLocatorW", Utf16(link)), ("UniformResourceLocator", Utf8(link)));
        _pictures.Downloader.Answers["https://cdn.example.com/cat.jpg"] = Image();

        var sources = PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null));
        var result = await Service().PrepareAsync(Assert.Single(sources), true);

        Assert.Equal(new[] { "https://cdn.example.com/cat.jpg", link }, Addresses(sources));
        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://cdn.example.com/cat.jpg" }, _pictures.Downloader.Asked);
    }

    [Fact]
    public async Task AnArticleLinkAndAnHtmlImg_TheImgWins()
    {
        var data = Drop(("UniformResourceLocatorW", Utf16("https://example.com/article")),
            ("HTML Format", CfHtml("https://example.com/article", "<a href=\"https://example.com/article\"><img src=\"https://cdn.example.com/cat.png\"></a>")));
        _pictures.Downloader.Answers["https://example.com/article"] = WebPage();
        _pictures.Downloader.Answers["https://cdn.example.com/cat.png"] = Image();

        var result = await Service().PrepareAsync(Assert.Single(PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null))), true);

        Assert.NotNull(result.Picture);
        Assert.Equal(new[] { "https://cdn.example.com/cat.png" }, _pictures.Downloader.Asked);
    }

    [Fact]
    public async Task OnlyAPageLink_IsACleanSkipWithAClearMessage()
    {
        var data = Drop(("UniformResourceLocatorW", Utf16("https://example.com/article")));
        _pictures.Downloader.Answers["https://example.com/article"] = WebPage();

        var result = await Service().PrepareAsync(Assert.Single(PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null))), true);

        Assert.Null(result.Picture);
        Assert.Contains("web page", result.Error);
    }

    [Fact]
    public void ADropWithNothingUsable_GivesNothing()
    {
        var data = Drop(("DragContext", [1, 2, 3]), ("Unknown_Format_1", [4]));

        Assert.Empty(PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null)));
    }

    [Fact]
    public void TheLog_SaysWhatEachFormatGave_WithoutImageBytes()
    {
        var data = Drop(("FileGroupDescriptorW", Descriptor("cat.png")), ("HTML Format", CfHtml("https://example.com/p", "<img src=\"https://cdn.example.com/cat.png\">")),
            ("DragImageBits", new byte[5000]));

        PictureDropPayload.Read(data, _log, new FakeVirtualFiles(null));

        var text = string.Join("\n", _log.Lines);
        Assert.Contains("FileGroupDescriptorW", text);
        Assert.Contains("cat.png", text);
        Assert.Contains("HTML Format: read", text);
        Assert.Contains("https://cdn.example.com/cat.png", text);
        Assert.Contains("DragImageBits: not read", text);
        Assert.DoesNotContain("\r", text);
    }
}
