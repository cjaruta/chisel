using Chisel.Core.Pictures;

namespace Chisel.Infrastructure.Pictures;

// The pictures folder: one file per stored asset, named <hash>.<extension>. Names that would leave the folder are refused.
public sealed class PictureFileStore(string folder) : IPictureFileStore
{
    public string PathOf(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) || storedName != Path.GetFileName(storedName) || storedName is "." or "..")
        {
            throw new ArgumentException("A picture file name has no folder part.", nameof(storedName));
        }
        return Path.Combine(folder, storedName);
    }

    public bool Exists(string storedName) => File.Exists(PathOf(storedName));

    // Written to a temporary file and moved into place, so a half-written file is never taken for a picture.
    public void Write(string storedName, byte[] bytes)
    {
        var path = PathOf(storedName);
        Directory.CreateDirectory(folder);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string storedName)
    {
        try
        {
            File.Delete(PathOf(storedName));
        }
        catch (IOException)
        {
            // Still open somewhere: the next sweep tries again.
        }
    }
}
