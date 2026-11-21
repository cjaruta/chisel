namespace Chisel.Core.Scripture;

// One bundled passage: a verse or a short run of verses from one chapter, with its exact wording.
// BookCode is the three-character code the chapter pages use ("JHN", "1CO"); BookName is the name shown in the reference.
public sealed record ScripturePassage(string BookCode, string BookName, int Chapter, int FirstVerse, int LastVerse, string Text)
{
    // "John 3:16" or "John 3:16-17".
    public string Reference =>
        LastVerse > FirstVerse ? $"{BookName} {Chapter}:{FirstVerse}-{LastVerse}" : $"{BookName} {Chapter}:{FirstVerse}";
}
