using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Chisel.Core.Pictures;

namespace Chisel.App.Services;

// What a drop from outside carries, in order of preference: files from Explorer, then bitmap data, then an image address
// (an img src in HTML first, then a dragged link). Only http, https and data:image addresses count; the picture itself is
// checked later, by its bytes.
public static class PictureDropPayload
{
    // Whether the drag has anything that could be a picture. Cheap: used while the pointer moves.
    public static bool HasPictures(IDataTransfer data) => Read(data, cheap: true).Count > 0;

    public static IReadOnlyList<PictureSource> Read(IDataTransfer data) => Read(data, cheap: false);

    private static IReadOnlyList<PictureSource> Read(IDataTransfer data, bool cheap)
    {
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
        string? html = null;
        if (files.Count == 0 && png is null)
        {
            texts.Add(data.TryGetText());
            html = ReadHtml(data);
        }
        return Choose(files, png, texts, html);
    }

    // The choice itself, without any drag data: files, else a bitmap's PNG bytes, else addresses.
    public static IReadOnlyList<PictureSource> Choose(IReadOnlyList<string> files, byte[]? bitmapPng, IEnumerable<string?> texts, string? html)
    {
        if (files.Count > 0)
        {
            return files.Select(f => (PictureSource)new FileSource(f)).ToList();
        }
        if (bitmapPng is { Length: > 0 })
        {
            return [new BytesSource(bitmapPng, "picture.png")];
        }
        if (PictureUrls.FromHtml(html) is { } src && PictureUrls.IsAllowed(src))
        {
            return [new UrlSource(src.Trim())];
        }
        return texts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .SelectMany(t => t!.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith('#') && PictureUrls.IsAllowed(line))
            .Select(line => (PictureSource)new UrlSource(line))
            .ToList();
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

    // Browsers put the dragged picture's HTML under a platform format; the name differs by system, so look for it.
    private static string? ReadHtml(IDataTransfer data)
    {
        try
        {
            foreach (var format in data.Formats.Where(f => f.Identifier.Contains("html", StringComparison.OrdinalIgnoreCase)))
            {
                var text = data.TryGetValue(DataFormat.CreateStringPlatformFormat(format.Identifier));
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // No readable HTML: the plain address is used instead.
        }
        return null;
    }
}
