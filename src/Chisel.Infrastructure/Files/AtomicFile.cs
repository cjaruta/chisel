using System.IO;
using System.Text;

namespace Chisel.Infrastructure.Files;

// Writes a whole file safely: the text goes to "<path>.tmp", is flushed to disk, then replaces the file in one move.
// A crash or power cut part way through leaves either the old file or the new one, never half of each.
public static class AtomicFile
{
    public static void WriteAllText(string path, string text) => WriteAllBytes(path, Encoding.UTF8.GetBytes(text));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}
