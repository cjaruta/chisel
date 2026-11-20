namespace Chisel.Core.Scripture;

// Where the bundled text came from. Kept next to the data so the licence travels with it.
// ChapterBaseUrl is the folder of the translation's chapter pages (one page per chapter).
public sealed record ScriptureSource(
    string Name, string Abbreviation, string Edition, bool PublicDomain, string License, string Origin, string ChapterBaseUrl);
