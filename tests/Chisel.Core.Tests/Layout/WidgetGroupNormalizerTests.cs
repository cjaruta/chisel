using Chisel.Core.Layout;

namespace Chisel.Core.Tests.Layout;

public class WidgetGroupNormalizerTests
{
    private static readonly WidgetLayout Defaults = new(new[]
    {
        new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c") }),
    }, new[] { "d", "e", "f" });

    private static WidgetPlacement Group(string id, int active, params string[] members) =>
        new(id, 1, null, members.Select(m => new GroupMember(m)).ToList(), active);

    private static WidgetLayout Normalize(params WidgetPlacement[] widgets) =>
        WidgetLayoutNormalizer.Normalize(new WidgetLayout(new[] { new LayoutColumn(1, widgets) }, new[] { "d", "e", "f" }), Defaults);

    [Fact]
    public void AValidGroup_LoadsUnchanged()
    {
        var layout = Normalize(Group("a", 1, "a", "b"), new WidgetPlacement("c"));

        var group = layout.Columns[0].Widgets[0];
        Assert.Equal(new[] { "a", "b" }, group.MemberIds);
        Assert.Equal("b", group.ActiveId);
    }

    [Fact]
    public void AGroupMember_IsNotListedAgain_AndIsNotBrandNew()
    {
        var layout = Normalize(Group("a", 0, "a", "b"), new WidgetPlacement("b"), new WidgetPlacement("c"));

        Assert.Equal(new[] { "a", "b", "c" }, layout.WidgetIds.ToArray());
        Assert.Equal(2, layout.Columns[0].Widgets.Count);
    }

    [Fact]
    public void AnUnknownMember_IsDropped_AndAGroupLeftWithOneBecomesAPlainCard()
    {
        var layout = Normalize(new WidgetPlacement("a", 1, null, new[] { new GroupMember("a", false), new GroupMember("zzz") }, 0), new WidgetPlacement("b"), new WidgetPlacement("c"));

        var first = layout.Columns[0].Widgets[0];
        Assert.False(first.IsGroup);
        Assert.Equal("a", first.WidgetId);
        Assert.False(first.ShowHeader);
    }

    [Fact]
    public void ATooLargeActiveIndex_IsClamped()
    {
        var layout = Normalize(Group("a", 9, "a", "b"), new WidgetPlacement("c"));

        Assert.Equal(1, layout.Columns[0].Widgets[0].ActiveIndex);
    }

    [Fact]
    public void AGroupOverTheLimit_KeepsTheFirstFive_AndTheRestAreNotLost()
    {
        var defaults = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { "a", "b", "c", "d", "e", "f" }.Select(id => new WidgetPlacement(id)).ToList()),
        });
        var saved = new WidgetLayout(new[] { new LayoutColumn(1, new[] { Group("a", 0, "a", "b", "c", "d", "e", "f") }) });

        var layout = WidgetLayoutNormalizer.Normalize(saved, defaults);

        Assert.Equal(LayoutLimits.MaximumGroupMembers, layout.Columns[0].Widgets[0].MemberIds.Count);
        Assert.Equal(6, layout.WidgetIds.Count());
    }

    [Fact]
    public void AnOldLayoutWithNoGroups_LoadsUnchanged()
    {
        var layout = Normalize(new WidgetPlacement("a", 2), new WidgetPlacement("b", 0.5, true), new WidgetPlacement("c"));

        Assert.All(layout.Columns[0].Widgets, w => Assert.False(w.IsGroup));
        Assert.True(layout.Columns[0].Widgets[1].ShowHeader);
    }

    [Fact]
    public void AGroupMemberThatIsAlsoHidden_StaysOnTheDashboard()
    {
        var saved = new WidgetLayout(new[] { new LayoutColumn(1, new[] { Group("a", 0, "a", "b"), new WidgetPlacement("c") }) }, new[] { "b", "d", "e", "f" });

        var layout = WidgetLayoutNormalizer.Normalize(saved, Defaults);

        Assert.DoesNotContain("b", layout.HiddenWidgetIds);
    }
}
