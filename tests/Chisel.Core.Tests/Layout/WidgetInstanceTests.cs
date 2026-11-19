using Chisel.Core.Layout;

namespace Chisel.Core.Tests.Layout;

public class WidgetInstanceTests
{
    // "a", "b" single; "m" allows several instances.
    private static readonly WidgetTypes Types = new(new[] { "a", "b" }, new[] { "m" });

    private static readonly WidgetLayout Defaults = new(new[]
    {
        new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b") }),
    });

    private static WidgetLayout Layout(params WidgetPlacement[] widgets) => new(new[] { new LayoutColumn(1, widgets) });

    private static WidgetLayout Normalize(WidgetLayout layout, IEnumerable<string>? hidden = null) =>
        WidgetLayoutNormalizer.Normalize(
            new WidgetLayout(layout.Columns, hidden ?? layout.HiddenWidgetIds), Defaults, Types);

    // ----- ids

    [Fact]
    public void Parse_SplitsAnInstanceIdIntoTypeAndKey()
    {
        Assert.Equal(("m", "k1"), InstanceIds.Parse("m:k1"));
        Assert.Equal(("clock", null), InstanceIds.Parse("clock"));
    }

    [Theory]
    [InlineData("m:")]
    [InlineData(":k")]
    [InlineData("m:a:b")]
    public void Parse_TreatsAMalformedIdAsInvalid(string id)
    {
        Assert.Null(InstanceIds.TryParse(id));
    }

    [Fact]
    public void Create_AndNewInstanceId_MakeKeyedIds()
    {
        Assert.Equal("m:k1", InstanceIds.Create("m", "k1"));

        var keys = new Queue<string>(new[] { "aaaaaa", "bbbbbb" });
        var id = InstanceIds.NewInstanceId("m", new[] { "m:aaaaaa" }, keys.Dequeue);
        Assert.Equal("m:bbbbbb", id);
        Assert.True(InstanceIds.IsInstance(id));
        Assert.False(InstanceIds.IsInstance("a"));
    }

    // ----- titles

    [Fact]
    public void Titles_AreNumberedOnlyWhenSeveralInstancesOfATypeAreOnTheDashboard()
    {
        var one = WidgetTitles.For(new[] { "a", "m:x" }, _ => "Frame");
        Assert.Equal("Frame", one["m:x"]);

        var two = WidgetTitles.For(new[] { "m:x", "a", "m:y", "m:z" }, id => id.StartsWith('m') ? "Frame" : "A");
        Assert.Equal("Frame", two["m:x"]);
        Assert.Equal("Frame 2", two["m:y"]);
        Assert.Equal("Frame 3", two["m:z"]);
        Assert.Equal("A", two["a"]);
    }

    // ----- layout operations

    [Fact]
    public void AddWidget_CreatesANewInstance_WithoutItBeingHidden()
    {
        var layout = Layout(new WidgetPlacement("a")).AddWidget("m:x", 0, 1, 1).AddWidget("m:y", 0, 2, 1);

        Assert.Equal(new[] { "a", "m:x", "m:y" }, layout.WidgetIds.ToArray());
        Assert.Empty(layout.HiddenWidgetIds);
    }

    [Fact]
    public void AddWidget_RefusesAnInstanceThatIsAlreadyOnTheDashboard()
    {
        var layout = Layout(new WidgetPlacement("a"), new WidgetPlacement("m:x"));
        Assert.Throws<ArgumentException>(() => layout.AddWidget("m:x", 0, 0, 1));
    }

    [Fact]
    public void AddWidgetInNewColumn_CreatesANewInstance()
    {
        var layout = Layout(new WidgetPlacement("a")).AddWidgetInNewColumn("m:x", 1);

        Assert.Equal(2, layout.Columns.Count);
        Assert.Empty(layout.HiddenWidgetIds);
    }

    [Fact]
    public void RemoveWidget_DeletesAnInstance_AndHidesASingleInstanceWidget()
    {
        var layout = Layout(new WidgetPlacement("a"), new WidgetPlacement("m:x"), new WidgetPlacement("b"));

        var removed = layout.RemoveWidget("m:x");
        Assert.Equal(new[] { "a", "b" }, removed.WidgetIds.ToArray());
        Assert.Empty(removed.HiddenWidgetIds);

        Assert.Equal(new[] { "b" }, layout.RemoveWidget("b").HiddenWidgetIds.ToArray());
    }

    [Fact]
    public void RemoveWidget_OfAGroupCard_DeletesItsInstances_AndHidesItsSingleInstanceWidgets()
    {
        var group = new WidgetPlacement("m:x", 1, null, new[] { new GroupMember("m:x"), new GroupMember("m:y"), new GroupMember("a") });
        var layout = Layout(group, new WidgetPlacement("b")).RemoveWidget("m:x");

        Assert.Equal(new[] { "b" }, layout.WidgetIds.ToArray());
        Assert.Equal(new[] { "a" }, layout.HiddenWidgetIds.ToArray());
    }

    [Fact]
    public void SeveralInstancesOfOneType_CanCombineInOneCard_AndSplitAgain()
    {
        var layout = Layout(new WidgetPlacement("m:x"), new WidgetPlacement("m:y"), new WidgetPlacement("m:z"));

        Assert.True(layout.CanCombine("m:y", "m:x"));
        var group = layout.Combine("m:y", "m:x").Combine("m:z", "m:x");
        Assert.Equal(new[] { "m:x", "m:y", "m:z" }, group.Columns[0].Widgets[0].MemberIds);

        var split = group.Split("m:y");
        Assert.Equal(new[] { "m:x", "m:z" }, split.Columns[0].Widgets[0].MemberIds);
        Assert.Equal("m:y", split.Columns[0].Widgets[1].WidgetId);
    }

    // ----- normalizer

    [Fact]
    public void Normalizer_KeepsSeveralInstancesOfAMultiType_InAnyPlace()
    {
        var group = new WidgetPlacement("m:x", 1, null, new[] { new GroupMember("m:x"), new GroupMember("m:y") });
        var layout = Normalize(Layout(new WidgetPlacement("a"), group, new WidgetPlacement("m:z"), new WidgetPlacement("b")));

        Assert.Equal(new[] { "a", "m:x", "m:y", "m:z", "b" }, layout.WidgetIds.ToArray());
    }

    [Fact]
    public void Normalizer_DropsUnknownTypes_AndDuplicateIds()
    {
        var layout = Normalize(Layout(
            new WidgetPlacement("a"), new WidgetPlacement("zzz:1"), new WidgetPlacement("m:x"), new WidgetPlacement("m:x"),
            new WidgetPlacement("b")));

        Assert.Equal(new[] { "a", "m:x", "b" }, layout.WidgetIds.ToArray());
    }

    [Fact]
    public void Normalizer_DropsAKeyedIdOfASingleInstanceType_AndRepeatsOfIt()
    {
        var layout = Normalize(Layout(new WidgetPlacement("a"), new WidgetPlacement("a:2"), new WidgetPlacement("a"), new WidgetPlacement("b")));

        Assert.Equal(new[] { "a", "b" }, layout.WidgetIds.ToArray());
    }

    [Fact]
    public void Normalizer_DropsMalformedIds_AndInstancesInTheHiddenList()
    {
        var layout = Normalize(Layout(new WidgetPlacement("a"), new WidgetPlacement("m:"), new WidgetPlacement("b")), new[] { "m:old" });

        Assert.Equal(new[] { "a", "b" }, layout.WidgetIds.ToArray());
        Assert.DoesNotContain("m:old", layout.HiddenWidgetIds);
    }

    [Fact]
    public void Normalizer_DedupesAnInstanceRepeatedInsideAGroup()
    {
        var group = new WidgetPlacement("m:x", 1, null, new[] { new GroupMember("m:x"), new GroupMember("m:x"), new GroupMember("m:y") });
        var layout = Normalize(Layout(group, new WidgetPlacement("a"), new WidgetPlacement("b")));

        Assert.Equal(new[] { "m:x", "m:y" }, layout.Columns[0].Widgets[0].MemberIds);
    }

    [Fact]
    public void Normalizer_WithoutTypes_BehavesAsBefore()
    {
        var layout = WidgetLayoutNormalizer.Normalize(Layout(new WidgetPlacement("a"), new WidgetPlacement("m:x")), Defaults);

        Assert.Equal(new[] { "a", "b" }, layout.WidgetIds.ToArray());
    }
}
