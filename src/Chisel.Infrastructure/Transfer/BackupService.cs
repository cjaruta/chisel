using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Transfer;

// The file system as backups use it (a fake in tests).
public interface IBackupFiles
{
    bool Exists(string path);
    long Length(string path);
    byte[] ReadAllBytes(string path);
    // Writes the whole file or nothing: a failed write leaves any earlier file as it was.
    void WriteAllText(string path, string text);
    void Copy(string from, string to);
    void Delete(string path);
    void DeleteDirectory(string path);
    IReadOnlyList<string> Files(string directory, string pattern);
}

public sealed class FileSystemBackupFiles : IBackupFiles
{
    public bool Exists(string path) => File.Exists(path);

    public long Length(string path) => new FileInfo(path).Length;

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    public void Copy(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(to))!);
        File.Copy(from, to, overwrite: true);
    }

    public void Delete(string path) => File.Delete(path);

    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public IReadOnlyList<string> Files(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];
}

public sealed record ImportResult(bool Succeeded, string Message);

public interface IBackupService
{
    void Export(string path);
    BackupReadResult Inspect(string path);
    ImportResult Import(BackupPackage package, ImportMode mode);
    bool CanUndo { get; }
    ImportResult Undo();
}

// Export, import and undo. Before every import the current data is saved as an undo point (with copies of the picture files,
// which the next start would otherwise clean up), so an import can always be taken back.
public sealed class BackupService(ChiselDbContext context, IBackupFiles files, string backupsFolder, string picturesFolder,
    string appVersion, TimeProvider time) : IBackupService
{
    private const string UndoPrefix = "before-import-";
    public const int UndoPointsKept = 3;

    public void Export(string path) => files.WriteAllText(path, BackupDocument.Export(context, appVersion, time.GetUtcNow()));

    // Reads and checks a file without changing anything.
    public BackupReadResult Inspect(string path)
    {
        try
        {
            if (files.Length(path) > BackupDocument.MaxBytes)
            {
                return BackupDocument.Read(new byte[BackupDocument.MaxBytes + 1]);
            }
            return BackupDocument.Read(files.ReadAllBytes(path), name => files.Exists(Path.Combine(picturesFolder, name)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BackupReadResult.Fail("The file couldn't be read.");
        }
    }

    public ImportResult Import(BackupPackage package, ImportMode mode)
    {
        try
        {
            SaveUndoPoint();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ImportResult(false, "Nothing was imported: the current data couldn't be backed up first.");
        }
        return Apply(package, mode, "Imported.");
    }

    public bool CanUndo => LatestUndoPoint() is not null;

    // Puts back the data from just before the last import, then forgets that undo point.
    public ImportResult Undo()
    {
        if (LatestUndoPoint() is not { } point)
        {
            return new ImportResult(false, "There is no import to undo.");
        }
        var read = Inspect(point);
        if (read.Package is not { } package)
        {
            return new ImportResult(false, "The backup taken before the import couldn't be read.");
        }
        var result = Apply(package, ImportMode.Replace, "The last import was undone.");
        if (!result.Succeeded)
        {
            return result;
        }
        try
        {
            var pictures = PicturesOf(point);
            foreach (var file in files.Files(pictures, "*"))
            {
                var target = Path.Combine(picturesFolder, Path.GetFileName(file));
                if (!files.Exists(target))
                {
                    files.Copy(file, target);
                }
            }
            files.Delete(point);
            files.DeleteDirectory(pictures);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The data is back; at worst some pictures stay missing.
        }
        return result;
    }

    private ImportResult Apply(BackupPackage package, ImportMode mode, string done)
    {
        try
        {
            BackupImporter.Apply(context, package, mode);
            return new ImportResult(true, done);
        }
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            return new ImportResult(false, "The import failed, and nothing was changed.");
        }
    }

    private void SaveUndoPoint()
    {
        var name = UndoPrefix + time.GetUtcNow().ToString("yyyyMMdd-HHmmss-fff");
        var point = Path.Combine(backupsFolder, name + ".json");
        files.WriteAllText(point, BackupDocument.Export(context, appVersion, time.GetUtcNow()));
        foreach (var asset in context.PictureAssets.ToList())
        {
            var stored = BackupDocument.StoredName(asset);
            var source = Path.Combine(picturesFolder, stored);
            if (files.Exists(source))
            {
                files.Copy(source, Path.Combine(PicturesOf(point), stored));
            }
        }
        foreach (var old in UndoPoints().SkipLast(UndoPointsKept))
        {
            files.Delete(old);
            files.DeleteDirectory(PicturesOf(old));
        }
    }

    private static string PicturesOf(string point) => Path.ChangeExtension(point, null) + "-pictures";

    private IEnumerable<string> UndoPoints() => files.Files(backupsFolder, UndoPrefix + "*.json").Order(StringComparer.Ordinal);

    private string? LatestUndoPoint() => UndoPoints().LastOrDefault();
}
