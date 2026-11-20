using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Chisel.Core.Pictures;
using Chisel.Infrastructure.Pictures;
using Xunit;

namespace Chisel.Infrastructure.Tests.Pictures;

// Sites that need a browser-like request: the Referer, the Accept list, a retry after 401 or 403, and the specific messages.
public class WebDownloaderTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return respond(request);
        }
    }

    private sealed class MemoryLog : IPictureDropLog
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private static byte[] Png()
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 4);
        return bytes;
    }

    private static HttpResponseMessage Ok(byte[] body, string? contentType = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        if (contentType is not null)
        {
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        return response;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static HttpResponseMessage Redirect(string to)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(to);
        return response;
    }

    private static HttpImageDownloader Downloader(FakeHandler handler, IPictureDropLog? log = null) =>
        new(handler, TimeSpan.FromSeconds(15), log);

    [Fact]
    public async Task TheRequest_LooksLikeABrowsers_AndNeverAsksForAvif()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Ok(Png())));

        await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png", "https://example.com/page");

        var seen = handler.Requests.Single();
        Assert.Contains("Mozilla/5.0", seen.Headers.UserAgent.ToString());
        Assert.Equal("image/png,image/jpeg,image/webp,image/gif,image/*;q=0.8", seen.Headers.Accept.ToString().Replace(", ", ","));
        Assert.DoesNotContain("avif", seen.Headers.Accept.ToString());
        Assert.Equal(new Uri("https://example.com/page"), seen.Headers.Referrer);
    }

    [Fact]
    public async Task ASiteThatNeedsTheReferer_Answers403Without_AndAPictureWith()
    {
        var handler = new FakeHandler(request => Task.FromResult(
            request.Headers.Referrer == new Uri("https://example.com/page") ? Ok(Png()) : Status(HttpStatusCode.Forbidden)));

        var withReferer = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png", "https://example.com/page");
        Assert.True(withReferer.Succeeded);

        // Without the page, the picture's own site is sent, which this site refuses.
        var without = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png");
        Assert.Equal(DownloadError.Refused, without.Error);
    }

    [Fact]
    public async Task WithoutAPage_TheRefererIsThePicturesOwnOrigin()
    {
        var handler = new FakeHandler(request => Task.FromResult(
            request.Headers.Referrer == new Uri("https://cdn.example.com/") ? Ok(Png()) : Status(HttpStatusCode.Forbidden)));

        var result = await Downloader(handler).DownloadAsync("https://cdn.example.com/img/a.png");

        Assert.True(result.Succeeded);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A403_IsRetriedOnce_WithADifferentReferer_ThenReportedWithItsStatus()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Status(HttpStatusCode.Forbidden)));

        var result = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png", "https://example.com/page");

        Assert.Equal(DownloadError.Refused, result.Error);
        Assert.Equal("The site refused the download (403)", result.Message);
        Assert.Equal(new Uri?[] { new Uri("https://example.com/page"), new Uri("https://cdn.example.com/") },
            handler.Requests.Select(r => r.Headers.Referrer).ToArray());
    }

    [Fact]
    public async Task ARetryWithTheOrigin_SucceedsWhenThePageRefererIsRefused()
    {
        var handler = new FakeHandler(request => Task.FromResult(
            request.Headers.Referrer == new Uri("https://cdn.example.com/") ? Ok(Png()) : Status(HttpStatusCode.Unauthorized)));

        var result = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png", "https://example.com/page");

        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A401_ReportsItsOwnStatus()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Status(HttpStatusCode.Unauthorized)));

        var result = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.png");

        Assert.Equal("The site refused the download (401)", result.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AnAvifContentType_GivesTheSpecificMessage()
    {
        var avif = new byte[] { 0, 0, 0, 0x1C, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'a', (byte)'v', (byte)'i', (byte)'f', 0, 0, 0, 0 };
        var handler = new FakeHandler(_ => Task.FromResult(Ok(avif, "image/avif")));

        var result = await Downloader(handler).DownloadAsync("https://cdn.example.com/a.avif");

        Assert.Equal(DownloadError.Unsupported, result.Error);
        Assert.Equal("AVIF and other formats are not supported", result.Message);
    }

    [Fact]
    public async Task AWebPage_IsAnErrorOfItsOwn()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Ok("<!DOCTYPE html><html><body>hello</body></html>"u8.ToArray(), "text/html")));

        var result = await Downloader(handler).DownloadAsync("https://example.com/page");

        Assert.Equal(DownloadError.WebPage, result.Error);
        Assert.Equal("That link is a web page, not an image", result.Message);
    }

    [Fact]
    public async Task AnUnknownBlob_IsNotAnImage()
    {
        var handler = new FakeHandler(_ => Task.FromResult(Ok(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })));

        var result = await Downloader(handler).DownloadAsync("https://example.com/a.bin");

        Assert.Equal(DownloadError.NotAPicture, result.Error);
        Assert.Equal("Not an image", result.Message);
    }

    [Fact]
    public async Task ASiteThatCannotBeReached_IsSaidSo_AndALargeFileIsTooLarge()
    {
        var down = new FakeHandler(_ => throw new HttpRequestException("no route"));
        var unreachable = await Downloader(down).DownloadAsync("https://example.com/a.png");
        Assert.Equal(DownloadError.Unreachable, unreachable.Error);
        Assert.Equal("Could not reach the site", unreachable.Message);

        var big = new FakeHandler(_ => Task.FromResult(Ok(new byte[31 * 1024 * 1024])));
        var tooLarge = await Downloader(big).DownloadAsync("https://example.com/a.png");
        Assert.Equal("The picture is too large", tooLarge.Message);
    }

    [Fact]
    public async Task ARedirectToAPicture_KeepsTheReferer_AndFollowsToAnotherHost()
    {
        var handler = new FakeHandler(request => Task.FromResult(
            request.RequestUri!.Host == "short.example.com" ? Redirect("https://cdn.example.net/real.png") : Ok(Png())));

        var result = await Downloader(handler).DownloadAsync("https://short.example.com/x", "https://example.com/page");

        Assert.True(result.Succeeded);
        Assert.Equal("real.png", result.FileName);
        Assert.All(handler.Requests, r => Assert.Equal(new Uri("https://example.com/page"), r.Headers.Referrer));
    }

    [Fact]
    public async Task EveryAttempt_IsLogged_WithStatusTypeSizeAndFirstBytes_AndNeverThePageContents()
    {
        var log = new MemoryLog();
        var handler = new FakeHandler(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/page"
            ? Ok("<!DOCTYPE html><html><body>secret page text</body></html>"u8.ToArray(), "text/html")
            : Ok(Png(), "image/png")));
        var downloader = Downloader(handler, log);

        await downloader.DownloadAsync("https://example.com/page");
        await downloader.DownloadAsync("https://example.com/a.png", "https://example.com/gallery");

        var text = string.Join("\n", log.Lines);
        Assert.Contains("https://example.com/a.png", text);
        Assert.Contains("status=200", text);
        Assert.Contains("content-type=image/png", text);
        Assert.Contains("bytes=33", text);
        Assert.Contains("first8=89504E470D0A1A0A", text);
        Assert.Contains("referer=https://example.com/gallery", text);
        Assert.Contains("result=ok", text);
        Assert.Contains("content-type=text/html", text);
        Assert.Contains("first8=3C21444F43545950", text);
        Assert.Contains("result=WebPage", text);
        Assert.DoesNotContain("secret page text", text);
    }
}
