using System.IO;
using Chisel.Core.Links;

namespace Chisel.App.Services;

// The real disk, for the quick link rules (see QuickLinkTarget).
public sealed class FileSystemPathProbe : IPathProbe
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);
}
