using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Chisel.Core.Pictures;

namespace Chisel.App.Services;

// What a drop from outside carries, in order of preference: files from Explorer, then bitmap data, then a browser's virtual
// file, then addresses. A browser gives several addresses for one picture, so they become the ordered candidates of one
// WebSource: the img src values in the HTML (made absolute against the page address), then addresses unwrapped from
// image-search links, then the links themselves, then plain text. Only http, https and data:image addresses count; the
// picture itself is checked later, by its bytes.
public static class PictureDropPayload
{
    // Whether the drag has anything that could be a picture. Cheap: used while the pointer moves.
    public static bool HasPictures(IDataTransfer data) => Read(data, null, null, cheap: true).Count > 0;

    // log: where to write what the drop carried (each format and what was read from it, and the candidates found).
    // virtualFiles: how to open a browser's virtual file; null uses the platform's.
    public static IReadOnlyList<PictureSource> Read(IDataTransfer data, IPictureDropLog? log = null, IVirtualFiles? virtualFiles = null) =>
        Read(data, log, virtualFiles, cheap: false);

    private static IReadOnlyList<PictureSource> Read(IDataTransfer data, IPictureDropLog? log, IVirtualFiles? virtualFiles, bool cheap)
    {
        if (!cheap)
        {
            log?.Write("drop formats: " + string.Join(", ", data.Formats.Select(f => f.Identifier)));
        }
        var files = new List<string>();
        try
        {
            foreach (var item in data.TryGetFiles() ?? [])
            {
                if (item.TryGetLocalPath() is { Length: > 0 } path && File.Exists(path))
                {
                    files.Add(path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Files that cannot be listed are not pictures.
        }

        byte[]? png = null;
        if (files.Count == 0 && data.Contains(DataFormat.Bitmap))
        {
            // A bitmap is only turned into bytes on the drop; while dragging, knowing it is there is enough.
            png = cheap ? [0] : Encode(data.TryGetBitmap());
        }

        var texts = new List<string?>();
        var uriLists = new List<string?>();
        string? html = null;
        Func<CancellationToken, Task<VirtualFile?>>? virtualFile = null;
        if (files.Count == 0 && png is null)
        {
            html = ReadFormats(data, cheap ? null : log, texts, uriLists);
            virtualFiles ??= PlatformVirtualFiles.Default;
            virtualFile = data.Formats.Any(f => f.Identifier == "FileGroupDescriptorW") ? virtualFiles?.Open(data, cheap ? null : log) : null;
        }
        var sources = Choose(files, png, texts, html, uriLists, virtualFile);
        if (!cheap)
        {
            var web = (sources.FirstOrDefault() as WebSource) ?? (sources.FirstOrDefault() as DeferredFileSource)?.Fallback;
            log?.Write(files.Count > 0 ? $"drop files ({files.Count})"
                : png is not null ? "drop bitmap"
                : $"drop candidates ({web?.Candidates.Count ?? 0}){(virtualFile is null ? "" : " + virtual file")}: "
                    + string.Join(" | ", web?.Candidates.Select(c => c.Address.Length > 200 ? c.Address[..200] + "..." : c.Address) ?? []));
        }
        return sources;
    }

    public static IReadOnlyList<PictureSource> Choose(IReadOnlyList<string> files, byte[]? bitmapPng, IEnumerable<string?> texts, string? html) =>
        Choose(files, bitmapPng, texts, html, []);

    // The choice itself, without any drag data: files, else a bitmap's PNG bytes, else a virtual file (with the web candidates
    // to fall back on), else the web candidates as one picture.
    public static IReadOnlyList<PictureSource> Choose(IReadOnlyList<string> files, byte[]? bitmapPng, IEnumerable<string?> texts, string? html,
        IEnumerable<string?> uriLists, Func<CancellationToken, Task<VirtualFile?>>? virtualFile = null)
    {
        if (files.Count > 0)
        {
            return files.Select(f => (PictureSource)new FileSource(f)).ToList();
        }
        if (bitmapPng is { Length: > 0 })
        {
            return [new BytesSource(bitmapPng, "picture.png")];
        }
        var candidates = PictureCandidates.Build(html, uriLists, texts);
        var web = candidates.Count > 0 ? new WebSource(candidates) : null;
        if (virtualFile is not null)
        {
            return [new DeferredFileSource(virtualFile, web)];
        }
        return web is not null ? [web] : [];
    }

    private static byte[]? Encode(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return null;
        }
        using (bitmap)
        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream);
            return stream.ToArray();
        }
    }

    // Formats whose contents are never text and never a picture's address, so they are not read.
    private static bool IsBinary(string id) =>
        id.Equals("FileContents", StringComparison.OrdinalIgnoreCase) || id.Equals("DragImageBits", StringComparison.OrdinalIgnoreCase)
        || id.Equals("DragContext", StringComparison.OrdinalIgnoreCase) || id.StartsWith("chromium/", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("Unknown_Format", StringComparison.OrdinalIgnoreCase);

    // Reads every format that can hold text, by its bytes (browsers write CF_HTML as UTF-8 and others as UTF-16, so the
    // decoding is done here). html-like formats give the HTML ("HTML Format", with its header, first), uri-list-like ones
    // an address list, the rest plain text. log gets one line per format.
    private static string? ReadFormats(IDataTransfer data, IPictureDropLog? log, List<string?> texts, List<string?> uriLists)
    {
        var htmls = new List<(string Id, string Text)>();
        var text = TryGet(() => data.TryGetText());
        if (!string.IsNullOrEmpty(text))
        {
            texts.Add(text);
        }
        foreach (var format in TryGet(() => data.Formats.ToList()) ?? [])
        {
            var id = format.Identifier;
            if (id == "FileGroupDescriptorW")
            {
                var bytes = TryGet(() => data.TryGetValue(DataFormat.CreateBytesPlatformFormat(id)));
                log?.Write($"format {id}: read {bytes?.Length ?? 0} bytes, files: {string.Join(", ", bytes is null ? [] : FileDescriptors.Names(bytes))}");
                continue;
            }
            if (IsBinary(id) || format == DataFormat.Bitmap || format == DataFormat.File)
            {
                log?.Write($"format {id}: not read");
                continue;
            }
            if (format == DataFormat.Text)
            {
                log?.Write($"format {id}: read as text, {text?.Length ?? 0} characters: {Preview(text)}");
                continue;
            }
            var raw = TryGet(() => data.TryGetValue(DataFormat.CreateBytesPlatformFormat(id)));
            var decoded = raw is { Length: > 0 } ? HtmlClipboard.Decode(raw) : TryGet(() => data.TryGetValue(DataFormat.CreateStringPlatformFormat(id)));
            log?.Write($"format {id}: read {raw?.Length ?? 0} bytes{(raw is null && decoded is not null ? " (as text)" : "")}: {Preview(decoded)}");
            if (string.IsNullOrEmpty(decoded))
            {
                continue;
            }
            if (id.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                htmls.Add((id, decoded));
            }
            else if (id.Contains("uri-list", StringComparison.OrdinalIgnoreCase) || id.Contains("UniformResourceLocator", StringComparison.OrdinalIgnoreCase)
                || id.Contains("moz-url", StringComparison.OrdinalIgnoreCase))
            {
                uriLists.Add(decoded);
            }
            else if (id.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                texts.Add(decoded);
            }
        }
        // CF_HTML carries the page address, so it is preferred over a bare text/html.
        return htmls.OrderBy(h => h.Id.Equals("HTML Format", StringComparison.OrdinalIgnoreCase) ? 0 : 1).Select(h => h.Text).FirstOrDefault();
    }

    private static T? TryGet<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or IOException)
        {
            return default;
        }
    }

    // The first 300 characters on one line, without control characters.
    private static string Preview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var builder = new StringBuilder();
        foreach (var c in text.Length > 300 ? text[..300] : text)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }
        return builder.ToString();
    }
}
