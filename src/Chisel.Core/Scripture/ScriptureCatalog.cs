using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chisel.Core.Scripture;

// The bundled passages (a curated selection, not the whole Bible) and what is done with them: which one a day gets,
// the text to copy, and the page of its chapter. Pure: the date is passed in (the caller gets it from TimeProvider).
public sealed class ScriptureCatalog
{
    private const string ResourceName = "Chisel.Core.Scripture.web-passages.json";
    private static readonly Lazy<ScriptureCatalog> BundledCatalog = new(LoadBundled);

    public ScriptureCatalog(ScriptureSource source, IReadOnlyList<ScripturePassage> passages)
    {
        if (passages.Count == 0)
        {
            throw new ArgumentException("A catalog needs at least one passage.", nameof(passages));
        }
        Source = source;
        Passages = passages;
    }

    public static ScriptureCatalog Bundled => BundledCatalog.Value;

    public ScriptureSource Source { get; }

    public IReadOnlyList<ScripturePassage> Passages { get; }

    // One passage per calendar day, in file order, wrapping after the last. Depends only on the date, so it is the same
    // after a restart and for anyone; consecutive days get consecutive passages.
    public ScripturePassage ForDate(DateOnly date) => Passages[date.DayNumber % Passages.Count];

    // The verse, then the reference and translation on a second line.
    public string CopyText(ScripturePassage passage) =>
        $"{passage.Text}\n— {passage.Reference}, {Source.Name} ({Source.Abbreviation})";

    // The page of the passage's whole chapter ("…/JHN03.htm"; Psalms use three digits, as the source does).
    public string ChapterUrl(ScripturePassage passage)
    {
        var digits = passage.BookCode == "PSA" ? 3 : 2;
        return $"{Source.ChapterBaseUrl}{passage.BookCode}{passage.Chapter.ToString().PadLeft(digits, '0')}.htm";
    }

    public static ScriptureCatalog Parse(string json)
    {
        var file = JsonSerializer.Deserialize<DataFile>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("Empty scripture data.");
        var s = file.Source;
        return new ScriptureCatalog(
            new ScriptureSource(s.Name, s.Abbreviation, s.Edition, s.PublicDomain, s.License, s.Origin, s.ChapterBaseUrl),
            file.Passages.Select(p => new ScripturePassage(p.Book, p.Name, p.Chapter, p.First, p.Last, p.Text)).ToList());
    }

    private static ScriptureCatalog LoadBundled()
    {
        using var stream = typeof(ScriptureCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private sealed record DataFile(
        [property: JsonPropertyName("source")] DataSource Source,
        [property: JsonPropertyName("passages")] List<DataPassage> Passages);

    private sealed record DataSource(string Name, string Abbreviation, string Edition, bool PublicDomain, string License, string Origin, string ChapterBaseUrl);

    private sealed record DataPassage(string Book, string Name, int Chapter, int First, int Last, string Text);
}
