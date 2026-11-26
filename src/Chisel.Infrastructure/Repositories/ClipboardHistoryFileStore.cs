using System.Text.Json;
using Chisel.Core.Clipboard;

namespace Chisel.Infrastructure.Repositories;

// Saved clipboard history, used only while "Keep history between sessions" is on: one JSON file in the app's data folder, written
// whole (to a temporary file, then moved over) and deleted outright when the setting is turned off or the history is cleared.
// Local only. Failures are swallowed without detail so no clipboard text can end up in an error log.
public sealed class ClipboardHistoryFileStore : IClipboardHistoryStore
{
    private sealed record Saved(Guid Id, string Text, DateTimeOffset CopiedAt, bool Pinned);

    private readonly string _path;

    public ClipboardHistoryFileStore(string path)
    {
        _path = path;
    }

    public string Location => _path;

    public IReadOnlyList<ClipboardEntry> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }
            var saved = JsonSerializer.Deserialize<List<Saved>>(File.ReadAllText(_path)) ?? [];
            return saved.Where(s => s.Text is not null).Select(s => new ClipboardEntry(s.Id, s.Text, s.CopiedAt, s.Pinned)).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<ClipboardEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(entries.Select(e => new Saved(e.Id, e.Text, e.CopiedAt, e.Pinned))));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not saved this time; the next change tries again.
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(_path);
            File.Delete(_path + ".tmp");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
