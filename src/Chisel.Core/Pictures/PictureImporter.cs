using System.Security.Cryptography;

namespace Chisel.Core.Pictures;

// Where pictures and their frames are saved. Asset rows are added at once; a frame's list is saved with the layout (Done).
public interface IPictureRepository
{
    PictureFrameSet LoadFrames();
    void SaveFrames(PictureFrameSet set);
    IReadOnlyList<PictureAsset> Assets();
    PictureAsset? Find(string hash);
    PictureAsset? Get(Guid id);
    void AddAsset(PictureAsset asset);
    void DeleteAssets(IEnumerable<Guid> ids);
}

// The files in the app data folder's pictures folder, named by StoredName.
public interface IPictureFileStore
{
    string PathOf(string storedName);
    bool Exists(string storedName);
    void Write(string storedName, byte[] bytes);
    void Delete(string storedName);
}

// Makes a smaller copy of a still picture, encoded as PNG. Null when it cannot.
public interface IImageResizer
{
    byte[]? Resize(byte[] data, int width, int height);
}

public enum DownloadError { None, BadAddress, TooLarge, Timeout, TooManyRedirects, NotAPicture, Failed, Refused, WebPage, Unsupported, Unreachable }

// Status and ContentType: what the server said (0 and null when there was no answer); ContentType is the downloaded
// file's declared type, for classifying it.
public sealed record DownloadResult(byte[]? Bytes, DownloadError Error = DownloadError.None, string? Message = null, string? FileName = null,
    int Status = 0, string? ContentType = null)
{
    public bool Succeeded => Bytes is not null;
}

public interface IImageDownloader
{
    // referer: the page the address came from, when known (a site may only serve its pictures to its own pages).
    Task<DownloadResult> DownloadAsync(string url, string? referer = null, CancellationToken cancellation = default);
}

// A plain-text diagnostic log for drops of pictures. Never receives page contents; never throws.
public interface IPictureDropLog
{
    void Write(string line);
}

// A picture that passed every check and is ready to store: the bytes to keep (a display-size copy when asked for),
// what they are, and the SHA-256 that names the file.
public sealed record PreparedPicture(byte[] Bytes, string Hash, ImageInfo Info, string FileName, string? SourceUrl);

public sealed record PrepareResult(PreparedPicture? Picture, string? Error);

// Turns bytes into a stored asset in two steps, so the slow first step can run off the UI thread and the second
// (files and the database) on it. The same bytes are stored once.
public sealed class PictureImporter(IPictureRepository repository, IPictureFileStore files, IImageResizer resizer, TimeProvider time)
{
    // Checks the bytes by their content, applies the "Keep original files" decision, and hashes the result.
    public PrepareResult Prepare(byte[] bytes, string fileName, string? sourceUrl, bool keepOriginals, string? contentType = null)
    {
        if (bytes.LongLength > ImageLimits.MaxDownloadBytes)
        {
            return new PrepareResult(null, "That file is larger than 30 MB.");
        }
        if (ImageSniffer.Sniff(bytes) is not { } info)
        {
            return new PrepareResult(null, ImageProblems.Message(ImageSniffer.Classify(bytes, contentType)));
        }
        if (ImageLimits.Check(info.Width, info.Height) is { } tooBig)
        {
            return new PrepareResult(null, tooBig);
        }

        var decision = PictureStoragePolicy.Decide(keepOriginals, info.Width, info.Height, info.MimeType == ImageFormats.Gif && info.IsAnimated);
        // A copy that is not smaller than the original is not worth keeping: the setting must never make a file bigger.
        if (decision.Resize && resizer.Resize(bytes, decision.Width, decision.Height) is { } smaller && smaller.Length < bytes.Length
            && ImageSniffer.Sniff(smaller) is { } smallInfo)
        {
            bytes = smaller;
            info = smallInfo;
            fileName = Path.ChangeExtension(Path.GetFileName(fileName), ImageFormats.ExtensionOf(info.MimeType));
        }
        return new PrepareResult(new PreparedPicture(bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), info, fileName, sourceUrl), null);
    }

    // Stores the file and its row, or returns the asset already stored for these bytes.
    public PictureAsset Commit(PreparedPicture picture)
    {
        var asset = repository.Find(picture.Hash)
            ?? PictureAsset.Create(picture.Hash, picture.FileName, picture.Info.MimeType, picture.Info.Width, picture.Info.Height,
                picture.Bytes.LongLength, picture.SourceUrl, time.GetUtcNow());
        if (!files.Exists(asset.StoredName))
        {
            files.Write(asset.StoredName, picture.Bytes);
        }
        if (repository.Find(picture.Hash) is null)
        {
            repository.AddAsset(asset);
        }
        return asset;
    }
}
