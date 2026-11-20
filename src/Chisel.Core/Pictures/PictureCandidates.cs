using System.Text.RegularExpressions;

namespace Chisel.Core.Pictures;

// One address a dropped picture might be at, and the page it came from (sent as the Referer; null lets the downloader
// use the picture's own site).
public sealed record PictureCandidate(string Address, string? Referer = null);

// From what a browser puts on a drag, the ordered addresses to try: every img src in the HTML (made absolute against the
// page address or a base tag), then the entries of a URI list, then plain-text addresses, without repeats. Only http,
// https and data:image addresses are kept.
public static class PictureCandidates
{
    private static readonly Regex ImgSrc = new(
        @"<img\b[^>]*?\bsrc\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BaseHref = new(
        @"<base\b[^>]*?\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SourceUrl = new(@"^\s*SourceURL:\s*(\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // The page address a browser's HTML clipboard format carries ("SourceURL:" in its header), or null.
    public static string? SourceUrlOf(string? html)
    {
        if (string.IsNullOrEmpty(html) || SourceUrl.Match(html) is not { Success: true } match)
        {
            return null;
        }
        return IsWebAddress(match.Groups[1].Value) ? match.Groups[1].Value : null;
    }

    public static IReadOnlyList<PictureCandidate> Build(string? html, IEnumerable<string?> uriLists, IEnumerable<string?> texts, string? pageUrl = null)
    {
        var page = IsWebAddress(pageUrl) ? pageUrl : SourceUrlOf(html);
        var result = new List<PictureCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? address, string? referer)
        {
            if (address is { Length: > 0 } && PictureUrls.IsAllowed(address) && seen.Add(address.Trim()))
            {
                result.Add(new PictureCandidate(address.Trim(), address.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? null : referer));
            }
        }

        if (!string.IsNullOrEmpty(html))
        {
            var baseAddress = BaseOf(html, page) ?? page;
            foreach (Match match in ImgSrc.Matches(html))
            {
                var src = Decode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value);
                Add(Resolve(src, baseAddress), page);
            }
        }
        foreach (var list in uriLists)
        {
            foreach (var line in Lines(list))
            {
                Add(line, page);
            }
        }
        foreach (var text in texts)
        {
            foreach (var line in Lines(text))
            {
                Add(line, page);
            }
        }
        return result;
    }

    // Each non-empty line that is not a comment.
    public static IEnumerable<string> Lines(string? text) =>
        (text ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'));

    private static string? BaseOf(string html, string? page)
    {
        if (BaseHref.Match(html) is not { Success: true } match)
        {
            return null;
        }
        var href = Decode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value);
        return Resolve(href, page);
    }

    // An address made absolute: itself when it already is, otherwise relative to the page. Null when there is no page.
    private static string? Resolve(string address, string? page)
    {
        address = address.Trim();
        if (address.Length == 0)
        {
            return null;
        }
        if (address.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(address, UriKind.Absolute, out _) && !address.StartsWith("//", StringComparison.Ordinal))
        {
            return address;
        }
        if (!IsWebAddress(page) || !Uri.TryCreate(page, UriKind.Absolute, out var baseUri))
        {
            return null;
        }
        return Uri.TryCreate(baseUri, address, out var resolved) ? resolved.AbsoluteUri : null;
    }

    private static bool IsWebAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string Decode(string value) => value.Replace("&amp;", "&", StringComparison.Ordinal).Trim();
}
