using System.Linq;
using System.Text;
using Chisel.Core.Pictures;

namespace Chisel.Infrastructure.Pictures;

// The drop diagnostics: a plain-text file, one line per entry, capped by trimming the oldest lines. Writing never throws
// (a log must not break a drop).
public sealed class FilePictureDropLog : IPictureDropLog
{
    public const long DefaultMaxBytes = 200 * 1024;

    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _gate = new();

    public FilePictureDropLog(string path, long maxBytes = DefaultMaxBytes)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    public string Path => _path;

    public void Write(string line)
    {
        try
        {
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line.Replace('\r', ' ').Replace('\n', ' ')}{Environment.NewLine}";
            lock (_gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, text, Encoding.UTF8);
                if (new FileInfo(_path).Length > _maxBytes)
                {
                    Trim();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to do: the drop goes on without a log.
        }
    }

    // Keeps the newest lines that fit in three quarters of the cap, whole lines only.
    private void Trim()
    {
        var lines = File.ReadAllLines(_path, Encoding.UTF8);
        var budget = _maxBytes * 3 / 4;
        var kept = new List<string>();
        long size = 0;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            size += Encoding.UTF8.GetByteCount(lines[i]) + Environment.NewLine.Length;
            if (size > budget)
            {
                break;
            }
            kept.Add(lines[i]);
        }
        kept.Reverse();
        Files.AtomicFile.WriteAllText(_path, string.Concat(kept.Select(line => line + Environment.NewLine)));
    }
}
