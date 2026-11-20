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
    private static readonly Regex ImgTag = new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex Attribute(string name) => new(
        @"(?<![\w-])" + name + @"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SrcAttribute = Attribute("src");

    private static readonly Regex SrcsetAttribute = Attribute("srcset");

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

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
            foreach (Match tag in ImgTag.Matches(html))
            {
                Add(Resolve(Decode(ValueOf(SrcAttribute, tag.Value)), baseAddress), page);
                var first = Decode(ValueOf(SrcsetAttribute, tag.Value)).Split(',')[0].Trim().Split(' ', 2)[0];
                Add(Resolve(first, baseAddress), page);
            }
        }
        var lists = uriLists.ToList();
        foreach (var line in lists.Concat(texts).SelectMany(Lines).ToList())
        {
            foreach (var inner in Unwrap(line))
            {
                Add(inner, page);
            }
        }
        foreach (var list in lists)
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

    private static string ValueOf(Regex attribute, string tag)
    {
        var match = attribute.Match(tag);
        return !match.Success ? "" : match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
    }

    // The picture addresses hidden inside a link: a Google Images result carries the picture in "imgurl"; any other link
    // may carry one as a query value that is an http(s) address ending in a PNG, JPEG, GIF or WebP name.
    public static IReadOnlyList<string> Unwrap(string link)
    {
        var result = new List<string>();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Query.Length < 2)
        {
            return result;
        }
        var isGoogleImages = Regex.IsMatch(uri.Host, @"^(www\.)?google\.[a-z.]+$", RegexOptions.IgnoreCase)
            && uri.AbsolutePath.StartsWith("/imgres", StringComparison.OrdinalIgnoreCase);
        foreach (var pair in uri.Query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = pair.IndexOf('=');
            if (at <= 0)
            {
                continue;
            }
            var name = pair[..at];
            var value = Uri.UnescapeDataString(pair[(at + 1)..].Replace('+', ' '));
            if (!Uri.TryCreate(value, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https"))
            {
                continue;
            }
            if (isGoogleImages && name == "imgurl" || ImageExtensions.Any(e => target.AbsolutePath.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(value);
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
