using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Chisel.Core.Pictures;

namespace Chisel.App.Services;

// What a drop from outside carries, in order of preference: files from Explorer, then bitmap data, then addresses. A browser
// gives several for one picture, so they become the ordered candidates of one WebSource: the img src values in the HTML
// (made absolute against the page address), then the entries of a URI list, then plain-text addresses. Only http, https and
// data:image addresses count; the picture itself is checked later, by its bytes.
public static class PictureDropPayload
{
    // Whether the drag has anything that could be a picture. Cheap: used while the pointer moves.
    public static bool HasPictures(IDataTransfer data) => Read(data, null, cheap: true).Count > 0;

    // log: where to write what the drop carried (the format names present, and the candidates found).
    public static IReadOnlyList<PictureSource> Read(IDataTransfer data, IPictureDropLog? log = null) => Read(data, log, cheap: false);

    private static IReadOnlyList<PictureSource> Read(IDataTransfer data, IPictureDropLog? log, bool cheap)
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
        if (files.Count == 0 && png is null)
        {
            texts.Add(data.TryGetText());
            html = ReadByName(data, "html", uriLists);
        }
        var sources = Choose(files, png, texts, html, uriLists);
        if (!cheap)
        {
            log?.Write(files.Count > 0 ? $"drop files ({files.Count})"
                : png is not null ? "drop bitmap"
                : $"drop candidates ({(sources.FirstOrDefault() as WebSource)?.Candidates.Count ?? 0}): "
                    + string.Join(" | ", (sources.FirstOrDefault() as WebSource)?.Candidates.Select(c => c.Address.Length > 200 ? c.Address[..200] + "..." : c.Address) ?? []));
        }
        return sources;
    }

    public static IReadOnlyList<PictureSource> Choose(IReadOnlyList<string> files, byte[]? bitmapPng, IEnumerable<string?> texts, string? html) =>
        Choose(files, bitmapPng, texts, html, []);

    // The choice itself, without any drag data: files, else a bitmap's PNG bytes, else the web candidates as one picture.
    public static IReadOnlyList<PictureSource> Choose(IReadOnlyList<string> files, byte[]? bitmapPng, IEnumerable<string?> texts, string? html,
        IEnumerable<string?> uriLists)
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
        return candidates.Count > 0 ? [new WebSource(candidates)] : [];
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

    // Browsers put the dragged picture's HTML (and its address list) under platform formats whose names differ by system,
    // so look through the names present: html-like ones give the HTML, uri-list-like ones the address list.
    private static string? ReadByName(IDataTransfer data, string htmlPart, List<string?> uriLists)
    {
        string? html = null;
        try
        {
            foreach (var format in data.Formats)
            {
                var id = format.Identifier;
                var isHtml = id.Contains(htmlPart, StringComparison.OrdinalIgnoreCase);
                var isList = id.Contains("uri-list", StringComparison.OrdinalIgnoreCase) || id.Contains("UniformResourceLocator", StringComparison.OrdinalIgnoreCase);
                if (!isHtml && !isList)
                {
                    continue;
                }
                var text = data.TryGetValue(DataFormat.CreateStringPlatformFormat(id));
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                if (isHtml)
                {
                    html ??= text;
                }
                else
                {
                    uriLists.Add(text);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // No readable HTML: the plain address is used instead.
        }
        return html;
    }
}
