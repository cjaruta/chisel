using System.Text.RegularExpressions;

namespace Chisel.Core.Clipboard;

// Windows clipboard formats an app sets to ask other apps not to keep what it copied. Password managers set them.
[Flags]
public enum ClipboardFlags
{
    None = 0,
    ExcludeFromMonitoring = 1, // "ExcludeClipboardContentFromMonitorProcessing" is present
    NoHistory = 2,             // "CanIncludeInClipboardHistory" is 0
    NoCloudUpload = 4,         // "CanUploadToCloudClipboard" is 0
    ViewerIgnore = 8,          // "Clipboard Viewer Ignore" is present (older password managers)
}

// One look at the clipboard: its text (null when it holds no text), the opt-out flags, and the name of the process that owns it.
// ToString leaves the text out so it can't reach a log, an exception or test output.
public sealed record ClipboardSnapshot(string? Text, ClipboardFlags Flags = ClipboardFlags.None, string? OwnerProcess = null)
{
    public override string ToString() => $"ClipboardSnapshot({Text?.Length ?? 0} chars, {Flags})";
}

// Why a clipboard change wasn't recorded. None means it was.
public enum ClipboardSkip
{
    None,
    NoText,
    Empty,
    Excluded,
    PasswordManager,
    TooLong,
    LooksLikeSecret,
}

// Decides whether a clipboard change may be recorded. Checks run cheapest and most certain first: the opt-out formats, the owning
// process, then the text itself.
//
// Heuristics used for "looks like a secret" (they can't be perfect: they miss some secrets and skip some ordinary text):
//  1. A card number: 13 to 19 digits, optionally grouped by spaces or dashes, that passes the Luhn check.
//  2. A PEM private key block ("-----BEGIN ... PRIVATE KEY").
//  3. Well-known token prefixes: GitHub (ghp_, gho_, ghu_, ghs_, github_pat_), Slack (xoxa-, xoxb-, xoxp-, xoxr-), "sk-" API keys,
//     AWS access key ids (AKIA), Google API keys (AIza), each followed by a long run of token characters.
//  4. A JSON web token: three base64url parts starting with "eyJ".
//  5. A random-looking token: one word of 20 to 512 characters with no spaces, that is not a URL or a path, mixes letters and
//     digits often (3+ switches), uses 3+ character classes (or 2 and is 32+ long, like a hex key), and has high character
//     entropy (3+ bits per character).
public static partial class ClipboardFilter
{
    // Process names (lowercase, no ".exe") of password managers whose copies are never recorded, even if they don't set the formats.
    private static readonly HashSet<string> PasswordManagers = new(StringComparer.OrdinalIgnoreCase)
    {
        "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane", "enpass", "roboform", "nordpass", "keeper",
        "keeperpasswordmanager", "passwordsafe", "pwsafe",
    };

    public const int MinTokenLength = 20;
    public const int MaxTokenLength = 512;
    public const double MinTokenEntropy = 3.0;

    public static ClipboardSkip Check(ClipboardSnapshot snapshot, ClipboardHistorySettings settings)
    {
        if (snapshot.Flags != ClipboardFlags.None)
        {
            return ClipboardSkip.Excluded;
        }
        if (snapshot.OwnerProcess is { } owner && PasswordManagers.Contains(owner))
        {
            return ClipboardSkip.PasswordManager;
        }
        if (snapshot.Text is not { } text)
        {
            return ClipboardSkip.NoText;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return ClipboardSkip.Empty;
        }
        if (text.Length > settings.MaxLength)
        {
            return ClipboardSkip.TooLong;
        }
        return LooksLikeSecret(text) ? ClipboardSkip.LooksLikeSecret : ClipboardSkip.None;
    }

    public static bool LooksLikeSecret(string text)
    {
        foreach (Match match in CardPattern().Matches(text))
        {
            if (Luhn.IsCardNumber(match.Value))
            {
                return true;
            }
        }
        return text.Contains("-----BEGIN", StringComparison.Ordinal) && text.Contains("PRIVATE KEY", StringComparison.Ordinal)
            || TokenPrefixPattern().IsMatch(text)
            || JwtPattern().IsMatch(text)
            || IsRandomToken(text.Trim());
    }

    private static bool IsRandomToken(string word)
    {
        if (word.Length is < MinTokenLength or > MaxTokenLength || word.Contains("://", StringComparison.Ordinal)
            || word[0] == '/' || word.Contains('\\'))
        {
            return false;
        }
        bool lower = false, upper = false, digit = false, other = false;
        var switches = 0;
        var wasDigit = char.IsAsciiDigit(word[0]);
        Span<int> counts = stackalloc int[128];
        foreach (var c in word)
        {
            if (char.IsWhiteSpace(c) || c >= 128)
            {
                return false;
            }
            counts[c]++;
            var isDigit = char.IsAsciiDigit(c);
            if (isDigit != wasDigit && (isDigit || char.IsAsciiLetter(c)))
            {
                switches++;
                wasDigit = isDigit;
            }
            if (char.IsAsciiLetterLower(c)) lower = true;
            else if (char.IsAsciiLetterUpper(c)) upper = true;
            else if (isDigit) digit = true;
            else if (c is not ('-' or '_' or '.')) other = true; // separators don't count: slugs and file names use them
        }
        var classes = (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
        if (!digit || switches < 3 || !(classes >= 3 || classes == 2 && word.Length >= 32))
        {
            return false;
        }
        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count > 0)
            {
                var p = (double)count / word.Length;
                entropy -= p * Math.Log2(p);
            }
        }
        return entropy >= MinTokenEntropy;
    }

    [GeneratedRegex(@"(?<!\d)(?:\d[ -]?){12,18}\d(?!\d)")]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"\b(?:gh[pous]_|github_pat_|xox[abpr]-|sk-|AKIA|AIza)[A-Za-z0-9_\-]{16,}")]
    private static partial Regex TokenPrefixPattern();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}")]
    private static partial Regex JwtPattern();
}

// The Luhn check digit test card numbers use.
public static class Luhn
{
    // True for 13 to 19 digits (spaces and dashes between them are ignored) that pass the Luhn check.
    public static bool IsCardNumber(string text)
    {
        Span<int> digits = stackalloc int[19];
        var n = 0;
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                if (n == digits.Length)
                {
                    return false;
                }
                digits[n++] = c - '0';
            }
            else if (c is not (' ' or '-'))
            {
                return false;
            }
        }
        if (n < 13)
        {
            return false;
        }
        var sum = 0;
        for (var i = 0; i < n; i++)
        {
            var d = digits[n - 1 - i];
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
