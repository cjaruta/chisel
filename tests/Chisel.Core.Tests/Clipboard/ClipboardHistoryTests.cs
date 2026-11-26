using Chisel.Core.Clipboard;

namespace Chisel.Core.Tests.Clipboard;

public class ClipboardHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static string[] Texts(ClipboardHistory history) => history.Items.Select(i => i.Text).ToArray();

    private static ClipboardHistory With(int max, params string[] texts)
    {
        var history = new ClipboardHistory(max);
        for (var i = 0; i < texts.Length; i++)
        {
            history.Add(texts[i], T0.AddMinutes(i));
        }
        return history;
    }

    [Fact]
    public void Add_PutsTheNewestFirst()
    {
        Assert.Equal(new[] { "c", "b", "a" }, Texts(With(25, "a", "b", "c")));
    }

    [Fact]
    public void Add_ADuplicate_MovesItToTheTopInsteadOfStoringItAgain()
    {
        var history = With(25, "a", "b", "c");
        var id = history.Items[2].Id;

        history.Add("a", T0.AddHours(1));

        Assert.Equal(new[] { "a", "c", "b" }, Texts(history));
        Assert.Equal(id, history.Items[0].Id);
        Assert.Equal(T0.AddHours(1), history.Items[0].CopiedAt);
    }

    [Fact]
    public void Cap_RemovesTheOldestFirst()
    {
        Assert.Equal(new[] { "d", "c", "b" }, Texts(With(3, "a", "b", "c", "d")));
    }

    [Fact]
    public void PinnedItems_StayOnTop_AndAreNotRemovedByTheCap()
    {
        var history = With(3, "a", "b", "c");
        history.SetPinned(history.Items[2].Id, true); // "a"

        history.Add("d", T0.AddHours(1));
        history.Add("e", T0.AddHours(2));

        Assert.Equal(new[] { "a", "e", "d" }, Texts(history));
        Assert.True(history.Items[0].Pinned);

        history.SetPinned(history.Items[0].Id, false);
        Assert.Equal(new[] { "e", "d", "a" }, Texts(history)); // back in time order
    }

    [Fact]
    public void Remove_AndClear_DropItems()
    {
        var history = With(25, "a", "b", "c");
        history.SetPinned(history.Items[0].Id, true);

        history.Remove(history.Items[1].Id);
        Assert.Equal(new[] { "c", "a" }, Texts(history));

        history.Clear();
        Assert.Empty(history.Items);
    }

    [Fact]
    public void LoweringTheCap_TrimsAtOnce()
    {
        var history = With(25, "a", "b", "c", "d");
        history.MaxItems = 2;
        Assert.Equal(new[] { "d", "c" }, Texts(history));
    }

    [Fact]
    public void Search_MatchesIgnoringCase()
    {
        var history = With(25, "Shopping list", "meeting notes", "List of names");
        Assert.Equal(new[] { "List of names", "Shopping list" }, history.Search("LIST").Select(i => i.Text));
        Assert.Equal(3, history.Search(" ").Count());
    }

    [Fact]
    public void Entry_PreviewIsOneTrimmedLine_AndToStringHidesTheText()
    {
        var entry = new ClipboardEntry(Guid.NewGuid(), "  first   line\r\nsecond line  ", T0, false);
        Assert.Equal("first line second line", entry.Preview);

        var long_ = new ClipboardEntry(Guid.NewGuid(), new string('x', 500), T0, false);
        Assert.Equal(ClipboardEntry.PreviewLength + 1, long_.Preview.Length);
        Assert.EndsWith("…", long_.Preview);

        Assert.DoesNotContain("first", entry.ToString());
    }

    [Theory]
    [InlineData(20, "just now")]
    [InlineData(120, "2 min ago")]
    [InlineData(3 * 3600 + 5, "3 h ago")]
    [InlineData(2 * 86400, "2 d ago")]
    public void RelativeTime_IsShort(int seconds, string expected) =>
        Assert.Equal(expected, RelativeTime.Format(TimeSpan.FromSeconds(seconds)));
}
