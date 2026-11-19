using System.Net;
using Chisel.Core.Pictures;

namespace Chisel.Infrastructure.Pictures;

// Downloads a picture from an http or https address (or reads a data:image address). At most 30 MB, 15 seconds in all,
// 5 redirects, no cookies, and what comes back must be a PNG, JPEG, GIF or WebP by its first bytes.
public sealed class HttpImageDownloader : IImageDownloader
{
    public const int MaxRedirects = 5;
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    // The handler must not follow redirects or keep cookies itself (CreateDefault makes one that does neither).
    public HttpImageDownloader(HttpMessageHandler handler, TimeSpan timeout)
    {
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _timeout = timeout;
    }

    public static HttpImageDownloader CreateDefault() =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }, TimeSpan.FromSeconds(15));

    public async Task<DownloadResult> DownloadAsync(string url, CancellationToken cancellation = default)
    {
        if (!PictureUrls.IsAllowed(url))
        {
            return Fail(DownloadError.BadAddress, "Only web addresses (http or https) and pictures can be dropped.");
        }
        if (url.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return FromDataUrl(url.Trim());
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(_timeout);
        try
        {
            return await Fetch(new Uri(url.Trim()), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return Fail(DownloadError.Timeout, "The picture took too long to download.");
        }
        catch (HttpRequestException)
        {
            return Fail(DownloadError.Failed, "Couldn't download that picture.");
        }
    }

    private async Task<DownloadResult> Fetch(Uri address, CancellationToken token)
    {
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PictureFrame)");
            request.Headers.Accept.ParseAdd("image/*");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                if (redirects >= MaxRedirects)
                {
                    return Fail(DownloadError.TooManyRedirects, "That address redirects too many times.");
                }
                address = location.IsAbsoluteUri ? location : new Uri(address, location);
                if (address.Scheme is not ("http" or "https"))
                {
                    return Fail(DownloadError.BadAddress, "Only web addresses (http or https) and pictures can be dropped.");
                }
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Fail(DownloadError.Failed, $"The server answered {(int)response.StatusCode}.");
            }
            if (response.Content.Headers.ContentLength > ImageLimits.MaxDownloadBytes)
            {
                return Fail(DownloadError.TooLarge, "That picture is larger than 30 MB.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, token)) > 0)
            {
                memory.Write(buffer, 0, read);
                if (memory.Length > ImageLimits.MaxDownloadBytes)
                {
                    return Fail(DownloadError.TooLarge, "That picture is larger than 30 MB.");
                }
            }
            return Checked(memory.ToArray(), NameFrom(address));
        }
    }

    private static DownloadResult FromDataUrl(string url)
    {
        var comma = url.IndexOf(',');
        if (comma < 0 || !url[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(DownloadError.BadAddress, "That picture can't be read.");
        }
        var text = url[(comma + 1)..];
        if (text.Length / 4L * 3 > ImageLimits.MaxDownloadBytes)
        {
            return Fail(DownloadError.TooLarge, "That picture is larger than 30 MB.");
        }
        try
        {
            return Checked(Convert.FromBase64String(Uri.UnescapeDataString(text)), null);
        }
        catch (FormatException)
        {
            return Fail(DownloadError.BadAddress, "That picture can't be read.");
        }
    }

    // What came back must be a picture by its content; the name is only for display.
    private static DownloadResult Checked(byte[] bytes, string? name)
    {
        if (ImageSniffer.Sniff(bytes) is not { } info)
        {
            return Fail(DownloadError.NotAPicture, "That isn't a PNG, JPEG, GIF or WebP picture.");
        }
        var extension = ImageFormats.ExtensionOf(info.MimeType);
        return new DownloadResult(bytes, FileName: string.IsNullOrWhiteSpace(name) || !Path.HasExtension(name) ? $"picture.{extension}" : name);
    }

    private static string? NameFrom(Uri address)
    {
        var last = address.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(last) ? null : Uri.UnescapeDataString(last);
    }

    private static DownloadResult Fail(DownloadError error, string message) => new(null, error, message);
}
