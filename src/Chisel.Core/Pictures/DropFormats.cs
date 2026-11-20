using System.Text;
using System.Text.RegularExpressions;

namespace Chisel.Core.Pictures;

// Reading the raw clipboard formats a browser puts on a drag.
public static class HtmlClipboard
{
    // Text from the bytes of a text format: UTF-16 when it has a byte order mark or looks like it (every other byte zero),
    // otherwise UTF-8. Trailing nulls are dropped.
    public static string DecodeText(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return "";
        }
        string text;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0)
        {
            text = Encoding.Unicode.GetString(bytes);
        }
        else
        {
            text = Encoding.UTF8.GetString(bytes);
        }
        return text.TrimEnd('\0');
    }

    // The HTML of a drag: for the CF_HTML format ("HTML Format": a header of byte offsets, then the document) only the dragged
    // fragment, headed by its SourceURL line so relative addresses can be resolved; for any other text, the text itself.
    public static string? Decode(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }
        var text = DecodeText(bytes);
        if (!text.StartsWith("Version:", StringComparison.Ordinal))
        {
            return text.Length == 0 ? null : text;
        }
        var header = text.Length > 600 ? text[..600] : text;
        var start = Offset(header, "StartFragment");
        var end = Offset(header, "EndFragment");
        if (start is null || end is null || start < 0 || end <= start || end > bytes.Length)
        {
            return text;
        }
        var fragment = Encoding.UTF8.GetString(bytes, start.Value, end.Value - start.Value);
        var source = Regex.Match(header, @"^SourceURL:\s*(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return (source.Success ? "SourceURL:" + source.Groups[1].Value + "\r\n" : "") + fragment;
    }

    private static int? Offset(string header, string name)
    {
        var match = Regex.Match(header, "^" + name + @":\s*(\d+)", RegexOptions.Multiline);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }
}

// The FILEGROUPDESCRIPTORW a browser offers for a dragged picture: a count, then one 592-byte descriptor per file whose name
// starts 72 bytes in (260 UTF-16 characters).
public static class FileDescriptors
{
    private const int ItemSize = 592;
    private const int NameOffset = 72;
    private const int NameChars = 260;

    public static IReadOnlyList<string> Names(byte[] descriptor)
    {
        var names = new List<string>();
        if (descriptor.Length < 4)
        {
            return names;
        }
        var count = BitConverter.ToInt32(descriptor, 0);
        for (var i = 0; i < count && 4 + (i + 1) * ItemSize <= descriptor.Length; i++)
        {
            var at = 4 + i * ItemSize + NameOffset;
            var name = Encoding.Unicode.GetString(descriptor, at, NameChars * 2);
            var end = name.IndexOf('\0');
            names.Add(end >= 0 ? name[..end] : name);
        }
        return names;
    }
}
