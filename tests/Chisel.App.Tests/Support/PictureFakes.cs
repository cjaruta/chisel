using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Pictures;

namespace Chisel.App.Tests.Support;

internal sealed class FakePictureRepository : IPictureRepository
{
    public List<PictureAsset> Stored { get; } = [];
    public PictureFrameSet Frames { get; private set; } = PictureFrameSet.Empty;
    public int FrameSaves { get; private set; }
    public PictureFrameSet LoadFrames() => Frames;
    public void SaveFrames(PictureFrameSet set) { Frames = set; FrameSaves++; }
    public IReadOnlyList<PictureAsset> Assets() => Stored;
    public PictureAsset? Find(string hash) => Stored.FirstOrDefault(a => a.Hash == hash);
    public PictureAsset? Get(Guid id) => Stored.FirstOrDefault(a => a.Id == id);
    public void AddAsset(PictureAsset asset) => Stored.Add(asset);
    public void DeleteAssets(IEnumerable<Guid> ids) => Stored.RemoveAll(a => ids.Contains(a.Id));
}

internal sealed class FakePictureStore : IPictureFileStore
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public string PathOf(string storedName) => "pictures/" + storedName;
    public bool Exists(string storedName) => Files.ContainsKey(storedName);
    public void Write(string storedName, byte[] bytes) => Files[storedName] = bytes;
    public void Delete(string storedName) => Files.Remove(storedName);
}

internal sealed class FakeResizer : IImageResizer
{
    public byte[]? Resize(byte[] data, int width, int height) => TinyFiles.Png(width, height);
}

public sealed class FakeDownloader : IImageDownloader
{
    public Dictionary<string, DownloadResult> Answers { get; } = [];
    public List<string> Asked { get; } = [];
    public List<string?> Referers { get; } = [];

    // An answer that arrives only when the task completes (a download still on its way).
    public Dictionary<string, Task<DownloadResult>> Held { get; } = [];

    public Task<DownloadResult> DownloadAsync(string url, string? referer = null, CancellationToken cancellation = default)
    {
        Asked.Add(url);
        Referers.Add(referer);
        if (Held.TryGetValue(url, out var held))
        {
            return held;
        }
        return Task.FromResult(Answers.TryGetValue(url, out var answer) ? answer : new DownloadResult(null, DownloadError.Failed, "Couldn't download that picture."));
    }
}

// Tiny valid-header picture files; different sizes give different bytes (so different assets).
internal static class TinyFiles
{
    public static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }
}

internal sealed class PictureServicesFixture
{
    public FakePictureRepository Repository { get; } = new();
    public FakePictureStore Store { get; } = new();
    public FakeDownloader Downloader { get; } = new();
    public FakeResizer Resizer { get; } = new();
    public PictureCatalog Catalog => new(Repository, Store);
    public PictureImporter Importer(TimeProvider? time = null) => new(Repository, Store, Resizer, time ?? TimeProvider.System);

    // Stores a tiny picture and returns its asset.
    public PictureAsset Add(int width, int height = 10)
    {
        var importer = Importer();
        return importer.Commit(importer.Prepare(TinyFiles.Png(width, height), $"p{width}x{height}.png", null, true).Picture!);
    }
}
