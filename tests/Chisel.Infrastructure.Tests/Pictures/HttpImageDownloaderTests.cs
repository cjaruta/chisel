using System.Buffers.Binary;
using System.Net;
using Chisel.Core.Pictures;
using Chisel.Infrastructure.Pictures;
using Xunit;

namespace Chisel.Infrastructure.Tests.Pictures;

public class HttpImageDownloaderTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return respond(request, cancellationToken);
        }
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

    private static HttpResponseMessage Ok(byte[] body, long? declaredLength = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        if (declaredLength is { } length)
        {
            response.Content.Headers.ContentLength = length;
        }
        return response;
    }

    private static HttpResponseMessage Redirect(string to)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(to);
        return response;
    }

    private static HttpImageDownloader Downloader(FakeHandler handler, TimeSpan? timeout = null) => new(handler, timeout ?? TimeSpan.FromSeconds(15));

    [Fact]
    public async Task APicture_IsDownloaded_WithTheNameFromItsAddress()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Ok(Png())));

        var result = await Downloader(handler).DownloadAsync("https://example.com/photos/cat.png?size=big");

        Assert.True(result.Succeeded);
        Assert.Equal(Png(), result.Bytes);
        Assert.Equal("cat.png", result.FileName);
    }

    [Theory]
    [InlineData("file:///c:/secret.png")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("not a url")]
    public async Task OtherSchemes_AreRefusedWithoutAnyRequest(string url)
    {
        var handler = new FakeHandler((_, _) => throw new InvalidOperationException("no request expected"));

        var result = await Downloader(handler).DownloadAsync(url);

        Assert.Equal(DownloadError.BadAddress, result.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ADataImage_IsDecodedWithoutAnyRequest()
    {
        var handler = new FakeHandler((_, _) => throw new InvalidOperationException("no request expected"));

        var result = await Downloader(handler).DownloadAsync("data:image/png;base64," + Convert.ToBase64String(Png()));

        Assert.True(result.Succeeded);
        Assert.Equal(Png(), result.Bytes);
    }

    [Fact]
    public async Task AFileDeclaredOverTheLimit_IsRefusedBeforeItIsRead()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Ok(Png(), declaredLength: 31L * 1024 * 1024)));

        var result = await Downloader(handler).DownloadAsync("https://example.com/big.png");

        Assert.Equal(DownloadError.TooLarge, result.Error);
    }

    [Fact]
    public async Task AFileThatGrowsPastTheLimit_IsStopped()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Ok(new byte[31 * 1024 * 1024])));

        var result = await Downloader(handler).DownloadAsync("https://example.com/big.png");

        Assert.Equal(DownloadError.TooLarge, result.Error);
    }

    [Fact]
    public async Task ARealTypeIsCheckedByItsFirstBytes_NotByItsName()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Ok("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray())));

        var result = await Downloader(handler).DownloadAsync("https://example.com/logo.png");

        Assert.Equal(DownloadError.NotAPicture, result.Error);
        Assert.Contains("PNG, JPEG, GIF or WebP", result.Message);
    }

    [Fact]
    public async Task ASlowServer_TimesOut()
    {
        var handler = new FakeHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Ok(Png());
        });

        var result = await Downloader(handler, TimeSpan.FromMilliseconds(100)).DownloadAsync("https://example.com/slow.png");

        Assert.Equal(DownloadError.Timeout, result.Error);
    }

    [Fact]
    public async Task UpToFiveRedirects_AreFollowed()
    {
        var hops = 0;
        var handler = new FakeHandler((_, _) => Task.FromResult(hops++ < 5 ? Redirect($"https://example.com/{hops}.png") : Ok(Png())));

        var result = await Downloader(handler).DownloadAsync("https://example.com/start.png");

        Assert.True(result.Succeeded);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task ASixthRedirect_IsRefused()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Redirect("https://example.com/again.png")));

        var result = await Downloader(handler).DownloadAsync("https://example.com/start.png");

        Assert.Equal(DownloadError.TooManyRedirects, result.Error);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task ARedirectToAnotherScheme_IsRefused()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Redirect("file:///c:/secret.png")));

        var result = await Downloader(handler).DownloadAsync("https://example.com/start.png");

        Assert.Equal(DownloadError.BadAddress, result.Error);
    }

    [Fact]
    public async Task AServerError_IsAFailureWithAMessage()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await Downloader(handler).DownloadAsync("https://example.com/missing.png");

        Assert.Equal(DownloadError.Failed, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task NoCookiesAreSent()
    {
        HttpRequestMessage? seen = null;
        var handler = new FakeHandler((request, _) =>
        {
            seen = request;
            return Task.FromResult(Ok(Png()));
        });

        await Downloader(handler).DownloadAsync("https://example.com/a.png");

        Assert.False(seen!.Headers.Contains("Cookie"));
    }
}
