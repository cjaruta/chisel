using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;

namespace Chisel.App.Services;

// Where a dropped picture comes from: a file on disk, bytes already in hand (a dragged bitmap), or an address to download.
public abstract record PictureSource;

public sealed record FileSource(string Path) : PictureSource;

public sealed record BytesSource(byte[] Bytes, string Name) : PictureSource;

// One address (and optionally the page it came from) to download.
public sealed record UrlSource(string Url, string? Referer = null) : PictureSource;

// What a browser drop gives for one picture: several candidate addresses, in the order to try them.
public sealed record WebSource(IReadOnlyList<PictureCandidate> Candidates) : PictureSource;

// A file a browser offers without a path (Windows "virtual file"): its bytes and the name it gave.
public sealed record VirtualFile(byte[] Bytes, string Name);

// A dropped virtual file, read off the UI thread when the drop is prepared. When it is not a picture (or cannot be read), the
// web candidates of the same drop are tried instead.
public sealed record DeferredFileSource(Func<CancellationToken, Task<VirtualFile?>> Load, WebSource? Fallback) : PictureSource;

// A source after its first step: a picture ready to store, or the reason it is not one. Name is what to call it in a message.
public sealed record PreparedSource(PreparedPicture? Picture, string? Error, string Name);

// Reads, downloads and checks one dropped picture off the UI thread (Prepare), and stores it on it (Commit).
public sealed class PictureImportService(PictureImporter importer, IImageDownloader downloader, IUserSettingsRepository? settings, IPictureDropLog? log = null)
{
    public bool KeepOriginals => settings?.Get()?.PictureKeepOriginals ?? true;

    public MultiDropMode MultiDrop => settings?.Get()?.PictureMultiDrop ?? MultiDropMode.OneSlideshow;

    // The drop diagnostics (see IPictureDropLog), or null.
    public IPictureDropLog? Log => log;

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
            case DeferredFileSource deferred:
                return await PrepareDeferredAsync(deferred, keepOriginals, cancellation);
            case UrlSource url:
                return await PrepareAsync(new WebSource([new PictureCandidate(url.Url, url.Referer)]), keepOriginals, cancellation);
            case WebSource web:
                return await PrepareWebAsync(web, keepOriginals, cancellation);
            default:
                return new PreparedSource(null, "That can't be added.", "picture");
        }
    }

    private async Task<PreparedSource> PrepareDeferredAsync(DeferredFileSource deferred, bool keepOriginals, CancellationToken cancellation)
    {
        VirtualFile? file = null;
        try
        {
            file = await deferred.Load(cancellation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Note($"virtual file result=failed: {ex.GetType().Name}");
        }
        if (file is { Bytes.Length: > 0 })
        {
            var prepared = await Task.Run(() => importer.Prepare(file.Bytes, file.Name, null, keepOriginals), cancellation);
            if (prepared.Picture is not null)
            {
                Note("virtual file result=ok");
                return new PreparedSource(prepared.Picture, null, file.Name);
            }
            Note($"virtual file result=rejected: {prepared.Error}");
        }
        else
        {
            Note("virtual file result=none");
        }
        return deferred.Fallback is { } web
            ? await PrepareWebAsync(web, keepOriginals, cancellation)
            : new PreparedSource(null, ImageProblems.Message(ImageProblem.NotAnImage), file?.Name ?? "picture");
    }

    // Tries the candidates in order and takes the first that is a picture. One that is a page, an error or a format that is
    // not supported is skipped; when none works, the most useful message is the one shown.
    private async Task<PreparedSource> PrepareWebAsync(WebSource web, bool keepOriginals, CancellationToken cancellation)
    {
        var failures = new List<(int Rank, string Message, string Name)>();
        for (var i = 0; i < web.Candidates.Count; i++)
        {
            var candidate = web.Candidates[i];
            var label = Label(candidate.Address);
            Note($"candidate {i + 1}/{web.Candidates.Count} {Shorten(candidate.Address)}");
            var download = await downloader.DownloadAsync(candidate.Address, candidate.Referer, cancellation);
            if (download.Bytes is not { } downloaded)
            {
                Note($"candidate {i + 1} result={download.Error}: {download.Message}");
                failures.Add((Rank(download.Error), download.Message ?? ImageProblems.Message(ImageProblem.NotAnImage), download.FileName ?? label));
                continue;
            }

            var fileName = download.FileName ?? label;
            var source = candidate.Address.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? null : candidate.Address;
            var prepared = await Task.Run(() => importer.Prepare(downloaded, fileName, source, keepOriginals, download.ContentType), cancellation);
            if (prepared.Picture is not null)
            {
                Note($"candidate {i + 1} result=ok");
                return new PreparedSource(prepared.Picture, null, fileName);
            }
            Note($"candidate {i + 1} result=rejected: {prepared.Error}");
            failures.Add((ImageSniffer.Sniff(downloaded) is null ? Rank(DownloadError.NotAPicture) : Rank(DownloadError.TooLarge), prepared.Error ?? "Not an image", fileName));
        }

        if (failures.Count == 0)
        {
            Note("drop result=no candidates");
            return new PreparedSource(null, ImageProblems.Message(ImageProblem.NotAnImage), "picture");
        }
        var best = failures.OrderBy(f => f.Rank).First();
        Note($"drop result=failed: {best.Message}");
        return new PreparedSource(null, best.Message, best.Name);
    }

    // Lower is more useful to the person: a refusal says what to do, a web page says the least.
    private static int Rank(DownloadError error) => error switch
    {
        DownloadError.Refused => 0,
        DownloadError.TooLarge => 1,
        DownloadError.Unsupported => 2,
        DownloadError.Unreachable => 3,
        DownloadError.Timeout => 4,
        DownloadError.TooManyRedirects => 5,
        DownloadError.Failed => 6,
        DownloadError.NotAPicture => 7,
        DownloadError.WebPage => 8,
        _ => 9,
    };

    public PictureAsset Commit(PreparedPicture picture) => importer.Commit(picture);

    private static PreparedSource Wrap(PrepareResult result, string name) => new(result.Picture, result.Error, name);

    private void Note(string line) => log?.Write(line);

    private static string Shorten(string value) => value.Length <= 200 ? value : value[..200] + "...";

    // A short name for an address: its host, or "picture" for a data address.
    private static string Label(string url) =>
        url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? "picture"
        : Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "picture";
}
