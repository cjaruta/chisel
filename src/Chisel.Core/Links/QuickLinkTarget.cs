using System.Text.RegularExpressions;

namespace Chisel.Core.Links;

public enum QuickLinkKind
{
    Web,
    Folder,
    File,
}

// Asks the disk whether a path exists (so the rules can be tested without one).
public interface IPathProbe
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
}

public sealed record QuickLinkCheck(bool IsValid, string Target, QuickLinkKind Kind, string? Error);

// What a quick link may point at, and nothing else: an http or https address, or a full path to a folder or a document that
// exists. Everything is opened by the shell with no arguments, so the rules keep programs and scripts out, and any other
// kind of address (file:, javascript:, ms-settings: and so on) is refused.
public static partial class QuickLinkTarget
{
    // Opening one of these through the shell would run it, so they are never accepted.
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".scr", ".pif", ".bat", ".cmd", ".msi", ".msp", ".msc", ".cpl", ".dll", ".sys",
        ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".reg", ".inf",
        ".lnk", ".url", ".jar", ".application", ".gadget", ".appref-ms", ".appx", ".msix",
    };

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]+:")]
    private static partial Regex SchemePattern();

    // The input as it will be stored (trimmed, pasted quotes removed).
    public static string Clean(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1].Trim();
        }
        return text;
    }

    // The rules that need no disk. Null when the text is fine, else a short message for the user.
    public static string? CheckShape(string? input)
    {
        var text = Clean(input);
        if (text.Length == 0)
        {
            return "Enter a web address or a full path.";
        }
        if (text.Any(char.IsControl))
        {
            return "That address contains characters that can't be used.";
        }

        if (SchemePattern().IsMatch(text))
        {
            // A scheme of more than one letter is an address ("https:", "file:"); one letter is a drive ("C:").
            return Uri.TryCreate(text, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                   && !string.IsNullOrEmpty(uri.Host)
                ? null
                : "Only http and https addresses can be used.";
        }

        if (!Path.IsPathFullyQualified(text))
        {
            return "Enter a web address starting with http:// or https://, or a full path such as C:\\Documents.";
        }
        if (Blocked.Contains(Path.GetExtension(text)))
        {
            return "Programs and scripts can't be added. Pick a document or a folder.";
        }
        return null;
    }

    public static QuickLinkCheck Validate(string? input, IPathProbe probe)
    {
        var text = Clean(input);
        if (CheckShape(text) is { } problem)
        {
            return new QuickLinkCheck(false, text, QuickLinkKind.Web, problem);
        }
        if (SchemePattern().IsMatch(text))
        {
            return new QuickLinkCheck(true, text, QuickLinkKind.Web, null);
        }
        if (probe.DirectoryExists(text))
        {
            return new QuickLinkCheck(true, text, QuickLinkKind.Folder, null);
        }
        if (probe.FileExists(text))
        {
            return new QuickLinkCheck(true, text, QuickLinkKind.File, null);
        }
        return new QuickLinkCheck(false, text, QuickLinkKind.File, "That file or folder doesn't exist.");
    }
}
