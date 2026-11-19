using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;

namespace Chisel.App.Services;

// Where a dropped picture comes from: a file on disk, bytes already in hand (a dragged bitmap), or an address to download.
public abstract record PictureSource;

public sealed record FileSource(string Path) : PictureSource;

public sealed record BytesSource(byte[] Bytes, string Name) : PictureSource;

public sealed record UrlSource(string Url) : PictureSource;

// A source after its first step: a picture ready to store, or the reason it is not one. Name is what to call it in a message.
public sealed record PreparedSource(PreparedPicture? Picture, string? Error, string Name);

// Reads, downloads and checks one dropped picture off the UI thread (Prepare), and stores it on it (Commit).
public sealed class PictureImportService(PictureImporter importer, IImageDownloader downloader, IUserSettingsRepository? settings)
{
    public bool KeepOriginals => settings?.Get()?.PictureKeepOriginals ?? true;

    public MultiDropMode MultiDrop => settings?.Get()?.PictureMultiDrop ?? MultiDropMode.OneSlideshow;

    // keepOriginals is read once by the caller (on the UI thread) because the settings live in the database.
    public async Task<PreparedSource> PrepareAsync(PictureSource source, bool keepOriginals, CancellationToken cancellation = default)
    {
        switch (source)
        {
            case FileSource file:
                var name = Path.GetFileName(file.Path);
                try
                {
                    if (new FileInfo(file.Path).Length > ImageLimits.MaxDownloadBytes)
                    {
                        return new PreparedSource(null, "That file is larger than 30 MB.", name);
                    }
                    var bytes = await File.ReadAllBytesAsync(file.Path, cancellation);
                    return await Task.Run(() => Wrap(importer.Prepare(bytes, name, null, keepOriginals), name), cancellation);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return new PreparedSource(null, "That file can't be read.", name);
                }
            case BytesSource bytes:
                return await Task.Run(() => Wrap(importer.Prepare(bytes.Bytes, bytes.Name, null, keepOriginals), bytes.Name), cancellation);
            case UrlSource url:
                var result = await downloader.DownloadAsync(url.Url, cancellation);
                var label = result.FileName ?? Label(url.Url);
                return result.Bytes is { } downloaded
                    ? await Task.Run(() => Wrap(importer.Prepare(downloaded, label, url.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? null : url.Url, keepOriginals), label), cancellation)
                    : new PreparedSource(null, result.Message ?? "Couldn't download that picture.", label);
            default:
                return new PreparedSource(null, "That can't be added.", "picture");
        }
    }

    public PictureAsset Commit(PreparedPicture picture) => importer.Commit(picture);

    private static PreparedSource Wrap(PrepareResult result, string name) => new(result.Picture, result.Error, name);

    // A short name for an address: its host and last part.
    private static string Label(string url) =>
        url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? "picture"
        : Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "picture";
}
