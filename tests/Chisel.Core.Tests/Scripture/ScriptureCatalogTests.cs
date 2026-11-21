using Chisel.Core.Scripture;

namespace Chisel.Core.Tests.Scripture;

public class ScriptureCatalogTests
{
    private static ScriptureCatalog Bundled => ScriptureCatalog.Bundled;

    [Fact]
    public void Bundled_RecordsTheSourceEditionAndPublicDomainStatus()
    {
        var source = Bundled.Source;
        Assert.Equal("World English Bible", source.Name);
        Assert.Equal("WEB", source.Abbreviation);
        Assert.True(source.PublicDomain);
        Assert.Contains("2020", source.Edition);
        Assert.StartsWith("https://ebible.org/", source.Origin);
        Assert.Contains("Public Domain", source.License);
    }

    [Fact]
    public void Bundled_HoldsASelectionNotTheWholeBible()
    {
        Assert.InRange(Bundled.Passages.Count, 100, 400);
        Assert.Equal(Bundled.Passages.Count, Bundled.Passages.Select(p => p.Reference).Distinct().Count());
        Assert.All(Bundled.Passages, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Text), p.Reference);
            Assert.Equal(p.Text.Trim(), p.Text);
            Assert.DoesNotContain("  ", p.Text);
            Assert.True(p.Chapter > 0 && p.FirstVerse > 0 && p.LastVerse >= p.FirstVerse, p.Reference);
        });
    }

    [Fact]
    public void Bundled_KeepsTheExactWordingAndReference()
    {
        var john = Bundled.Passages.Single(p => p.Reference == "John 3:16-17");
        Assert.Equal(
            "For God so loved the world, that he gave his only born Son, that whoever believes in him should not perish, but have eternal life. "
            + "For God didn’t send his Son into the world to judge the world, but that the world should be saved through him.",
            john.Text);
        var psalm = Bundled.Passages.Single(p => p.Reference == "Psalm 23:1-6");
        Assert.StartsWith("A Psalm by David. Yahweh is my shepherd; I shall lack nothing.", psalm.Text);
        Assert.EndsWith("and I will dwell in Yahweh’s house forever.", psalm.Text);
        Assert.Equal("Genesis 1:1-5", Bundled.Passages[0].Reference);
        Assert.Equal("Philippians 4:13", Bundled.Passages.Single(p => p.Reference.StartsWith("Philippians 4:13")).Reference);
    }

    [Fact]
    public void Reference_NamesASingleVerseOrARange()
    {
        Assert.Equal("Romans 8:28", new ScripturePassage("ROM", "Romans", 8, 28, 28, "x").Reference);
        Assert.Equal("1 Corinthians 13:1-13", new ScripturePassage("1CO", "1 Corinthians", 13, 1, 13, "x").Reference);
    }

    [Fact]
    public void ForDate_IsTheSameForTheSameDate_AndStepsOnePassagePerDay()
    {
        var date = new DateOnly(2026, 11, 20);
        Assert.Same(Bundled.ForDate(date), Bundled.ForDate(date));
        var index = IndexOf(Bundled.ForDate(date));
        Assert.Equal((index + 1) % Bundled.Passages.Count, IndexOf(Bundled.ForDate(date.AddDays(1))));
    }

    [Fact]
    public void ForDate_WrapsAroundAfterTheLastPassage_AndReachesEveryPassage()
    {
        var count = Bundled.Passages.Count;
        var start = new DateOnly(2026, 1, 1);
        Assert.Same(Bundled.ForDate(start), Bundled.ForDate(start.AddDays(count)));
        var seen = Enumerable.Range(0, count).Select(i => Bundled.ForDate(start.AddDays(i))).Distinct().Count();
        Assert.Equal(count, seen);
        Assert.Same(Bundled.Passages[0], Bundled.ForDate(DateOnly.FromDayNumber(count * 1000)));
        Assert.Same(Bundled.Passages[count - 1], Bundled.ForDate(DateOnly.FromDayNumber(count * 1000 - 1)));
    }

    [Fact]
    public void CopyText_HasTheVerseTheReferenceAndTheTranslation()
    {
        var passage = new ScripturePassage("ROM", "Romans", 8, 28, 28, "We know that all things work together for good.");
        Assert.Equal("We know that all things work together for good.\n— Romans 8:28, World English Bible (WEB)", Bundled.CopyText(passage));
    }

    [Fact]
    public void ChapterUrl_OpensTheWebChapter()
    {
        Assert.Equal("https://ebible.org/eng-web/JHN03.htm", Bundled.ChapterUrl(new ScripturePassage("JHN", "John", 3, 16, 17, "x")));
        Assert.Equal("https://ebible.org/eng-web/PSA023.htm", Bundled.ChapterUrl(new ScripturePassage("PSA", "Psalm", 23, 1, 6, "x")));
        Assert.Equal("https://ebible.org/eng-web/1CO13.htm", Bundled.ChapterUrl(new ScripturePassage("1CO", "1 Corinthians", 13, 1, 13, "x")));
    }

    [Fact]
    public void ChapterUrl_ExistsForEveryBundledPassage()
    {
        Assert.All(Bundled.Passages, p => Assert.Matches(@"^https://ebible\.org/eng-web/[0-9A-Z]{3}\d{2,3}\.htm$", Bundled.ChapterUrl(p)));
    }

    private static int IndexOf(ScripturePassage passage) => Bundled.Passages.ToList().IndexOf(passage);
}
