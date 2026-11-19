using Chisel.Core.Layout;

namespace Chisel.Core.Tests.Layout;

public class WidgetGroupTests
{
    private static WidgetLayout Flat(params string[] ids) =>
        new(new[] { new LayoutColumn(1, ids.Select(id => new WidgetPlacement(id)).ToList()) }, new[] { "h" });

    private static string Ids(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.IsGroup
            ? "[" + string.Join("+", w.MemberIds) + "]@" + w.ActiveIndex
            : w.WidgetId))));

    [Fact]
    public void Combine_MakesTheDraggedWidgetTheLastTab_AndKeepsTheTargetActive()
    {
        var layout = Flat("a", "b", "c").Combine("c", "a");

        Assert.Equal("[a+c]@0,b", Ids(layout).Replace(" | ", ","));
        Assert.Equal(new[] { "a", "c" }, layout.Columns[0].Widgets[0].MemberIds);
        Assert.Equal("a", layout.Columns[0].Widgets[0].ActiveId);
    }

    [Fact]
    public void Combine_KeepsTargetsActiveTab_WhenTheTargetIsAlreadyAGroup()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a").SetActive("b").Combine("c", "a");

        Assert.Equal(new[] { "a", "b", "c" }, layout.Columns[0].Widgets[0].MemberIds);
        Assert.Equal("b", layout.Columns[0].Widgets[0].ActiveId);
    }

    [Fact]
    public void Combine_WithAGroup_BringsAllItsMembers()
    {
        var layout = Flat("a", "b", "c", "d").Combine("c", "d").Combine("a", "b").Combine("d", "a");

        Assert.Equal(new[] { "a", "b", "c", "d" }.Length, layout.Columns[0].Widgets[0].MemberIds.Count);
        Assert.Single(layout.Columns[0].Widgets);
    }

    [Fact]
    public void Combine_RemembersEachMembersOwnHeaderChoice()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a", 1, true), new WidgetPlacement("b", 1, false), new WidgetPlacement("c") }),
        });

        var group = layout.Combine("b", "a").Columns[0].Widgets[0];

        Assert.Equal(new bool?[] { true, false }, group.Members!.Select(m => m.ShowHeader).ToArray());
    }

    [Fact]
    public void Combine_RemovesTheDraggedCard_AndRescalesTheColumn()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a", 2), new WidgetPlacement("b", 1), new WidgetPlacement("c", 3) }),
        }).Combine("c", "a");

        Assert.Equal(2, layout.Columns[0].Widgets.Count);
        Assert.Equal(2, layout.Columns[0].Widgets.Sum(w => w.HeightWeight), 3);
    }

    [Fact]
    public void Combine_CanLeaveAnotherColumnEmpty_ForRemoveEmptyColumns()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a") }),
            new LayoutColumn(1, new[] { new WidgetPlacement("b") }),
        }).Combine("b", "a").RemoveEmptyColumns();

        Assert.Single(layout.Columns);
    }

    [Fact]
    public void Combine_StopsAtFiveMembers()
    {
        var layout = Flat("a", "b", "c", "d", "e", "f");
        foreach (var id in new[] { "b", "c", "d", "e" })
        {
            layout = layout.Combine(id, "a");
        }

        Assert.Equal(LayoutLimits.MaximumGroupMembers, layout.Columns[0].Widgets[0].MemberIds.Count);
        Assert.False(layout.CanCombine("f", "a"));
        Assert.Throws<InvalidOperationException>(() => layout.Combine("f", "a"));
    }

    [Fact]
    public void CanCombine_RejectsItself_AndUnknownWidgets_AndMembersOfTheSameGroup()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a");

        Assert.False(layout.CanCombine("a", "a"));
        Assert.False(layout.CanCombine("a", "b"));
        Assert.False(layout.CanCombine("zzz", "a"));
        Assert.False(layout.CanCombine("a", "zzz"));
        Assert.True(layout.CanCombine("c", "a"));
    }

    [Fact]
    public void Combine_RejectsAWidgetThatIsAlreadyInTheTargetGroup_BecauseEachWidgetExistsOnce()
    {
        var layout = Flat("a", "b").Combine("b", "a");

        Assert.False(layout.CanCombine("b", "a"));
        Assert.Throws<InvalidOperationException>(() => layout.Combine("b", "a"));
    }

    [Fact]
    public void Find_AndWidgetIds_SeeGroupMembers()
    {
        var layout = Flat("a", "b", "c").Combine("c", "a");

        Assert.Equal((0, 0), layout.Find("c"));
        Assert.Equal(new[] { "a", "c", "b" }, layout.WidgetIds.ToArray());
    }

    [Fact]
    public void Split_PutsTheMemberBelowTheGroup_WithItsHeightAndItsOwnHeaderChoiceBack()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a", 1, true), new WidgetPlacement("b", 1, false), new WidgetPlacement("c"), new WidgetPlacement("d") }),
        }).Combine("b", "a").Combine("c", "a");

        var result = layout.Split("b", 0.8);

        Assert.Equal("[a+c]@0,b,d", Ids(result).Replace(" | ", ","));
        var b = result.Columns[0].Widgets[1];
        Assert.False(b.ShowHeader);
        Assert.Equal(0.8, b.HeightWeight / result.Columns[0].Widgets[0].HeightWeight, 2);
    }

    [Fact]
    public void Split_DissolvesTheGroup_WhenOneMemberIsLeft_AndRestoresItsHeader()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a", 1, true), new WidgetPlacement("b", 1, false) }),
        }).Combine("b", "a");

        var result = layout.Split("b");

        Assert.Equal("a,b", Ids(result).Replace(" | ", ","));
        Assert.All(result.Columns[0].Widgets, w => Assert.False(w.IsGroup));
        Assert.True(result.Columns[0].Widgets[0].ShowHeader);
        Assert.False(result.Columns[0].Widgets[1].ShowHeader);
    }

    [Fact]
    public void Split_OfTheAnchorMember_GivesTheGroupANewId_AndKeepsTheActiveMember()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a").Combine("c", "a").SetActive("c");

        var result = layout.Split("a");

        var group = result.Columns[0].Widgets[0];
        Assert.Equal(new[] { "b", "c" }, group.MemberIds);
        Assert.Equal("c", group.ActiveId);
        Assert.Contains(group.WidgetId, group.MemberIds);
        Assert.Equal("a", result.Columns[0].Widgets[1].WidgetId);
    }

    [Fact]
    public void Split_OfTheActiveMember_ActivatesTheNeighbour()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a").Combine("c", "a").SetActive("b");

        var group = layout.Split("b").Columns[0].Widgets[0];

        Assert.Equal(new[] { "a", "c" }, group.MemberIds);
        Assert.Equal("c", group.ActiveId);
    }

    [Fact]
    public void Split_OfAPlainWidget_ChangesNothing()
    {
        var layout = Flat("a", "b");

        Assert.Same(layout, layout.Split("a"));
    }

    [Fact]
    public void ReorderMember_MovesATab_AndTheActiveTabFollowsItsMember()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a").Combine("c", "a").SetActive("a");

        var result = layout.ReorderMember("a", 2);

        var group = result.Columns[0].Widgets[0];
        Assert.Equal(new[] { "b", "c", "a" }, group.MemberIds);
        Assert.Equal("a", group.ActiveId);
        Assert.Equal(2, group.ActiveIndex);
    }

    [Fact]
    public void ReorderMember_ToWhereItIs_ReturnsTheSameLayout_AndClampsTheIndex()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a").Combine("c", "a");

        Assert.Same(layout, layout.ReorderMember("a", 0));
        Assert.Equal(new[] { "b", "c", "a" }, layout.ReorderMember("a", 99).Columns[0].Widgets[0].MemberIds);
    }

    [Fact]
    public void SetActive_ChangesOnlyTheActiveIndex()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a");

        var result = layout.SetActive("b");

        Assert.Equal(1, result.Columns[0].Widgets[0].ActiveIndex);
        Assert.Equal("b", result.Columns[0].Widgets[0].ActiveId);
        Assert.Same(result, result.SetActive("b"));
        Assert.Same(layout, layout.SetActive("a"));
        Assert.Throws<ArgumentException>(() => layout.SetActive("zzz"));
    }

    [Fact]
    public void GroupMinimumSize_IsTheLargestMinimumWidthAndHeightOfItsMembers()
    {
        var size = WidgetGroupSize.Minimum(new[] { (260.0, 110.0), (300.0, 90.0), (280.0, 200.0) });

        Assert.Equal((300.0, 200.0), size);
        Assert.Equal((0.0, 0.0), WidgetGroupSize.Minimum(Array.Empty<(double, double)>()));
    }

    [Fact]
    public void SetShowHeader_OnAGroupMember_ChangesNothing()
    {
        var layout = Flat("a", "b").Combine("b", "a");

        Assert.Same(layout, layout.SetShowHeader("b", false));
    }

    [Fact]
    public void RemoveWidget_OfAGroupMember_HidesTheWholeGroup()
    {
        var layout = Flat("a", "b", "c").Combine("b", "a");

        var result = layout.RemoveWidget("b");

        Assert.Equal("c", Ids(result));
        Assert.Equal(new[] { "h", "a", "b" }, result.HiddenWidgetIds);
    }

    [Fact]
    public void CanRemoveWidget_CountsCards_NotMembers()
    {
        var layout = Flat("a", "b").Combine("b", "a");

        Assert.False(layout.CanRemoveWidget);
        Assert.Throws<InvalidOperationException>(() => layout.RemoveWidget("a"));
    }

    [Fact]
    public void AddWidget_OfAGroupMember_IsRejected_LikeAnyWidgetAlreadyOnTheDashboard()
    {
        var layout = Flat("a", "b").Combine("b", "a");

        Assert.Throws<ArgumentException>(() => layout.AddWidget("b", 0, 0, 1));
    }

    [Fact]
    public void Move_OfAGroup_KeepsItsMembers()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b") }),
            new LayoutColumn(1, new[] { new WidgetPlacement("c") }),
        }).Combine("b", "a");

        var result = layout.Move("a", 1, 0);

        Assert.Empty(result.Columns[0].Widgets);
        Assert.Equal(new[] { "a", "b" }, result.Columns[1].Widgets[0].MemberIds);
    }

    [Fact]
    public void Describe_ShowsGroups()
    {
        var layout = Flat("a", "b").Combine("b", "a");

        Assert.Contains("a+b@0:1", layout.Describe());
    }
}
