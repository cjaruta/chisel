using System.Net;
using Chisel.Core.Pictures;

namespace Chisel.Infrastructure.Pictures;

// Downloads a picture from an http or https address (or reads a data:image address). At most 30 MB, 15 seconds in all,
// 5 redirects, no cookies, and what comes back must be a PNG, JPEG, GIF or WebP by its first bytes. The request looks like a
// browser's (user agent, an Accept list without AVIF, the page as Referer) because many sites refuse anything else; a 401 or
// 403 is tried once more with a different Referer. Every attempt is written to the log.
public sealed class HttpImageDownloader : IImageDownloader
{
    public const int MaxRedirects = 5;
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
    public const string Accept = "image/png,image/jpeg,image/webp,image/gif,image/*;q=0.8";

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;
    private readonly IPictureDropLog? _log;

    // The handler must not follow redirects or keep cookies itself (CreateDefault makes one that does neither).
    public HttpImageDownloader(HttpMessageHandler handler, TimeSpan timeout, IPictureDropLog? log = null)
    {
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _timeout = timeout;
        _log = log;
    }

    public static HttpImageDownloader CreateDefault(IPictureDropLog? log = null) =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }, TimeSpan.FromSeconds(15), log);

    public async Task<DownloadResult> DownloadAsync(string url, string? referer = null, CancellationToken cancellation = default)
    {
        if (!PictureUrls.IsAllowed(url))
        {
            Log($"attempt {Shorten(url)} result=BadAddress");
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
            var address = new Uri(url.Trim());
            var page = Uri.TryCreate(referer, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" ? parsed.AbsoluteUri : null;
            // First the page (or the picture's own site when the page is not known); after a refusal, one more try the other way.
            var first = page ?? Origin(address);
            var result = await Fetch(address, first, timeout.Token);
            if (result.Error == DownloadError.Refused)
            {
                var second = page is not null ? Origin(address) : null;
                result = await Fetch(address, second, timeout.Token);
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            Log($"attempt {Shorten(url)} result=Timeout");
            return Fail(DownloadError.Timeout, "The picture took too long to download.");
        }
        catch (HttpRequestException ex)
        {
            Log($"attempt {Shorten(url)} result=Unreachable ({ex.GetType().Name})");
            return Fail(DownloadError.Unreachable, "Could not reach the site");
        }
    }

    private static string Origin(Uri address) => address.GetLeftPart(UriPartial.Authority) + "/";

    private async Task<DownloadResult> Fetch(Uri address, string? referer, CancellationToken token)
    {
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", Accept);
            if (referer is not null)
            {
                request.Headers.Referrer = new Uri(referer);
            }
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var status = (int)response.StatusCode;
            var type = response.Content.Headers.ContentType?.MediaType;
            var prefix = $"attempt {Shorten(address.AbsoluteUri)} referer={referer ?? "none"} status={status} content-type={type ?? "none"}";

            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                Log($"{prefix} redirect={Shorten(location.ToString())}");
                if (redirects >= MaxRedirects)
                {
                    return Fail(DownloadError.TooManyRedirects, "That address redirects too many times.", status);
                }
                address = location.IsAbsoluteUri ? location : new Uri(address, location);
                if (address.Scheme is not ("http" or "https"))
                {
                    return Fail(DownloadError.BadAddress, "Only web addresses (http or https) and pictures can be dropped.", status);
                }
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var head = await ReadAsync(response, 8, token);
                Log($"{prefix} bytes={head.Length}+ first8={Hex(head)} result={(status is 401 or 403 ? "Refused" : "Failed")}");
                return status is 401 or 403
                    ? Fail(DownloadError.Refused, $"The site refused the download ({status})", status)
                    : Fail(DownloadError.Failed, status == 404 ? "The picture was not found (404)" : $"The server answered {status}.", status);
            }
            if (response.Content.Headers.ContentLength > ImageLimits.MaxDownloadBytes)
            {
                Log($"{prefix} bytes={response.Content.Headers.ContentLength} result=TooLarge");
                return Fail(DownloadError.TooLarge, "The picture is too large", status);
            }

            var body = await ReadAsync(response, ImageLimits.MaxDownloadBytes + 1, token);
            if (body.Length > ImageLimits.MaxDownloadBytes)
            {
                Log($"{prefix} bytes={body.Length}+ result=TooLarge");
                return Fail(DownloadError.TooLarge, "The picture is too large", status);
            }
            var result = Checked(body, NameFrom(address), type, status);
            Log($"{prefix} bytes={body.Length} first8={Hex(body.AsSpan(0, Math.Min(8, body.Length)))} result={(result.Succeeded ? "ok" : result.Error)}");
            return result;
        }
    }

    private static async Task<byte[]> ReadAsync(HttpResponseMessage response, long limit, CancellationToken token)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while (memory.Length < limit && (read = await stream.ReadAsync(buffer, token)) > 0)
        {
            memory.Write(buffer, 0, (int)Math.Min(read, limit - memory.Length));
        }
        return memory.ToArray();
    }

    private DownloadResult FromDataUrl(string url)
    {
        var comma = url.IndexOf(',');
        if (comma < 0 || !url[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            Log($"attempt data-url result=BadAddress");
            return Fail(DownloadError.BadAddress, "Not an image");
        }
        var text = url[(comma + 1)..];
        if (text.Length / 4L * 3 > ImageLimits.MaxDownloadBytes)
        {
            Log("attempt data-url result=TooLarge");
            return Fail(DownloadError.TooLarge, "The picture is too large");
        }
        try
        {
            var bytes = Convert.FromBase64String(Uri.UnescapeDataString(text));
            var result = Checked(bytes, null, url[5..comma].Split(';')[0], 0);
            Log($"attempt data-url content-type={url[5..comma].Split(';')[0]} bytes={bytes.Length} first8={Hex(bytes.AsSpan(0, Math.Min(8, bytes.Length)))} result={(result.Succeeded ? "ok" : result.Error)}");
            return result;
        }
        catch (FormatException)
        {
            Log("attempt data-url result=BadAddress");
            return Fail(DownloadError.BadAddress, "Not an image");
        }
    }

    // What came back must be a picture by its content; the name is only for display.
    private static DownloadResult Checked(byte[] bytes, string? name, string? contentType, int status)
    {
        if (ImageSniffer.Sniff(bytes) is not { } info)
        {
            var problem = ImageSniffer.Classify(bytes, contentType);
            var error = problem switch { ImageProblem.WebPage => DownloadError.WebPage, ImageProblem.Unsupported => DownloadError.Unsupported, _ => DownloadError.NotAPicture };
            return new DownloadResult(null, error, ImageProblems.Message(problem), Status: status, ContentType: contentType);
        }
        var extension = ImageFormats.ExtensionOf(info.MimeType);
        return new DownloadResult(bytes, FileName: string.IsNullOrWhiteSpace(name) || !Path.HasExtension(name) ? $"picture.{extension}" : name,
            Status: status, ContentType: contentType);
    }

    private static string? NameFrom(Uri address)
    {
        var last = address.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(last) ? null : Uri.UnescapeDataString(last);
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);

    // Addresses can be very long (data: and signed links): only the start is logged.
    private static string Shorten(string value) => value.Length <= 300 ? value : value[..300] + "...";

    private void Log(string line) => _log?.Write(line);

    private static DownloadResult Fail(DownloadError error, string message, int status = 0) => new(null, error, message, Status: status);
}
