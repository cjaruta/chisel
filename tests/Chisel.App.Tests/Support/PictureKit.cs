using System;
using System.IO;
using Chisel.App.Services;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Pictures;
using Chisel.Infrastructure.Repositories;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Support;

// The picture services behind a test window: the real repository and file store (in a temporary folder), a fake
// downloader, and the fake clock the window runs on. UiSnapshot.ShowMainWindow fills it in; pictures can be added
// before the window opens (Add) or while it is open.
public sealed class PictureKit : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "chisel-test-pictures-" + Guid.NewGuid().ToString("N"));
    public FakeDownloader Downloader { get; } = new();
    public FakeTimeProvider Time { get; private set; } = new(new DateTimeOffset(2000, 1, 1, 9, 30, 0, TimeSpan.Zero));
    public PictureCatalog Catalog { get; private set; } = null!;
    public PictureImporter Importer { get; private set; } = null!;
    public PictureImportService Service { get; private set; } = null!;
    public IPictureRepository Repository { get; private set; } = null!;

    private ChiselDbContext? _context;

    internal void Attach(ChiselDbContext context, FakeTimeProvider time, IUserSettingsRepository settings)
    {
        _context = context;
        Time = time;
        var files = new PictureFileStore(Path.Combine(Folder, "pictures"));
        Repository = new PictureRepository(context);
        Catalog = new PictureCatalog(Repository, files);
        Importer = new PictureImporter(Repository, files, new AvaloniaImageResizer(), time);
        Service = new PictureImportService(Importer, Downloader, settings);
    }

    // Stores a picture (as a drop would) and returns its asset.
    public PictureAsset Add(byte[] bytes, string name = "picture.png")
    {
        var prepared = Importer.Prepare(bytes, name, null, keepOriginals: true);
        return Importer.Commit(prepared.Picture ?? throw new InvalidOperationException(prepared.Error));
    }

    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            try
            {
                Directory.Delete(Folder, true);
            }
            catch (IOException)
            {
                // A temporary folder; the system cleans up later.
            }
        }
    }
}
