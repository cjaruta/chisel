using Chisel.Core.Layout;

namespace Chisel.Core.Tests.Layout;

public class WidgetGroupSplitTests
{
    private static double Min(string id) => 200;

    private static WidgetLayout Group(params string[] ids)
    {
        var layout = new WidgetLayout(new[] { new LayoutColumn(1, ids.Select(id => new WidgetPlacement(id)).ToList()) });
        for (var i = 1; i < ids.Length; i++)
        {
            layout = layout.Combine(ids[i], ids[0]);
        }
        return layout;
    }

    private static WidgetPlacement Card(WidgetLayout layout) => layout.Columns[0].Widgets[0];

    [Fact]
    public void SetViewMode_Split_IsAllowedForTwoOrThreeMembersWithRoom_AndKeepsTheActiveTab()
    {
        var layout = Group("a", "b", "c").SetActive("b");

        var result = layout.SetViewMode("a", GroupViewMode.Split, 700, Min);

        Assert.Null(result.Refusal);
        Assert.Equal(GroupViewMode.Split, Card(result.Layout).ViewMode);
        Assert.Equal("b", Card(result.Layout).ActiveId);
        Assert.Equal("b", Card(result.Layout.SetViewMode("a", GroupViewMode.Tabs, 700, Min).Layout).ActiveId);
    }

    [Theory]
    [InlineData(1, 900)]
    [InlineData(4, 2000)]
    [InlineData(5, 2000)]
    [InlineData(2, 300)]
    [InlineData(3, 600)]
    public void SetViewMode_Split_IsRefusedWithAReason_ForTooFewOrTooManyMembersOrANarrowCard(int members, double width)
    {
        var ids = new[] { "a", "b", "c", "d", "e" }.Take(members).ToArray();
        var layout = members == 1 ? Group("a") : Group(ids);

        var result = layout.SetViewMode("a", GroupViewMode.Split, width, Min);

        Assert.False(string.IsNullOrWhiteSpace(result.Refusal));
        Assert.Same(layout, result.Layout);
    }

    [Fact]
    public void SetViewMode_TheReasonsAreDifferent_ForCountAndWidth()
    {
        var count = Group("a", "b", "c", "d").SetViewMode("a", GroupViewMode.Split, 2000, Min).Refusal;
        var width = Group("a", "b").SetViewMode("a", GroupViewMode.Split, 300, Min).Refusal;

        Assert.NotEqual(count, width);
    }

    [Fact]
    public void SetViewMode_Tabs_IsAlwaysAllowed_EvenWhenTheCardIsNarrow()
    {
        var split = Group("a", "b").SetViewMode("a", GroupViewMode.Split, 700, Min).Layout;

        var result = split.SetViewMode("a", GroupViewMode.Tabs, 100, Min);

        Assert.Null(result.Refusal);
        Assert.Equal(GroupViewMode.Tabs, Card(result.Layout).ViewMode);
    }

    [Fact]
    public void Minimum_IsThePanesPlusTheGaps()
    {
        Assert.Equal(200 + 150 + 180 + 2 * GroupSplit.Gap, GroupSplit.MinimumWidth(new[] { 200.0, 150.0, 180.0 }));
    }

    [Fact]
    public void ResizePanes_MovesWeightBetweenNeighbours_AndKeepsTheTotal()
    {
        var split = Group("a", "b", "c").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        var resized = split.ResizePanes("a", 0.1, 0.1, 0.1);

        var weights = Card(resized).Members!.Select(m => m.WidthWeight).ToList();
        Assert.Equal(1 + 0.3, weights[0], 3);
        Assert.Equal(1 - 0.3, weights[1], 3);
        Assert.Equal(1, weights[2]);
        Assert.Equal(3, weights.Sum(), 6);
    }

    [Fact]
    public void ResizePanes_StopsAtEachPanesOwnMinimum()
    {
        var split = Group("a", "b").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        var wide = Card(split.ResizePanes("a", 5, 0.2, 0.3)).Members!.Select(m => m.WidthWeight).ToList();
        var narrow = Card(split.ResizePanes("a", -5, 0.2, 0.3)).Members!.Select(m => m.WidthWeight).ToList();

        Assert.Equal(2 * 0.3, wide[1], 6);
        Assert.Equal(2 * 0.2, narrow[0], 6);
    }

    [Fact]
    public void ResizePanes_OfTheLastPane_Throws()
    {
        var split = Group("a", "b").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        Assert.Throws<ArgumentException>(() => split.ResizePanes("b", 0.1, 0.1, 0.1));
    }

    [Fact]
    public void Combine_IntoASplitGroup_AddsAPane_WhenItFits()
    {
        var split = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c") }) })
            .Combine("b", "a").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        var combined = split.Combine("c", "a", 900, Min);

        Assert.Equal(GroupViewMode.Split, Card(combined).ViewMode);
        Assert.Equal(new[] { "a", "b", "c" }, Card(combined).MemberIds);
    }

    [Fact]
    public void Combine_IntoASplitGroup_FallsBackToTabs_WhenItDoesNotFit()
    {
        var split = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c") }) })
            .Combine("b", "a").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        Assert.Equal(GroupViewMode.Tabs, Card(split.Combine("c", "a", 500, Min)).ViewMode);
        Assert.Equal(GroupViewMode.Tabs, Card(split.Combine("c", "a")).ViewMode);
    }

    [Fact]
    public void Combine_IntoASplitGroupOfThree_FallsBackToTabs()
    {
        var layout = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c"), new WidgetPlacement("d") }) })
            .Combine("b", "a").Combine("c", "a");
        var split = layout.SetViewMode("a", GroupViewMode.Split, 2000, Min).Layout;

        var combined = split.Combine("d", "a", 2000, Min);

        Assert.Equal(GroupViewMode.Tabs, Card(combined).ViewMode);
        Assert.Equal(4, Card(combined).MemberIds.Count);
    }

    [Fact]
    public void Combine_IntoATabsGroup_StaysTabs()
    {
        var layout = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c") }) })
            .Combine("b", "a");

        Assert.Equal(GroupViewMode.Tabs, Card(layout.Combine("c", "a", 2000, Min)).ViewMode);
    }

    [Fact]
    public void Combine_NewPaneStartsWithTheAverageWeight()
    {
        var layout = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b"), new WidgetPlacement("c") }) })
            .Combine("b", "a").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout.ResizePanes("a", 0.1, 0.1, 0.1);

        var combined = layout.Combine("c", "a", 900, Min);

        Assert.Equal(1.0, Card(combined).Members![2].WidthWeight, 6);
    }

    private static WidgetLayout Defaults(params string[] ids) =>
        new(new[] { new LayoutColumn(1, ids.Select(id => new WidgetPlacement(id)).ToList()) });

    [Fact]
    public void Normalize_KeepsAValidSplit_AndRepairsBadWeights()
    {
        var saved = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement("a", 1, null,
                    new[] { new GroupMember("a", null, double.NaN), new GroupMember("b", null, -3) }, 1, GroupViewMode.Split),
            }),
        });

        var card = WidgetLayoutNormalizer.Normalize(saved, Defaults("a", "b")).Columns[0].Widgets[0];

        Assert.Equal(GroupViewMode.Split, card.ViewMode);
        Assert.All(card.Members!, m => Assert.True(m.WidthWeight >= WidgetLayout.MinimumWeight && double.IsFinite(m.WidthWeight)));
    }

    [Fact]
    public void Normalize_TurnsASplitOfFourIntoTabs_AndAPlainCardsModeIsIgnored()
    {
        var four = new WidgetPlacement("a", 1, null, new[] { new GroupMember("a"), new GroupMember("b"), new GroupMember("c"), new GroupMember("d") }, 0, GroupViewMode.Split);
        var one = new WidgetPlacement("e", 1, null, null, 0, GroupViewMode.Split);
        var saved = new WidgetLayout(new[] { new LayoutColumn(1, new[] { four, one }) });

        var cards = WidgetLayoutNormalizer.Normalize(saved, Defaults("a", "b", "c", "d", "e")).Columns[0].Widgets;

        Assert.Equal(GroupViewMode.Tabs, cards[0].ViewMode);
        Assert.Equal(GroupViewMode.Tabs, cards[1].ViewMode);
    }

    [Fact]
    public void Split_OutOfAThreeSplit_KeepsTheRestInSplit()
    {
        var split = Group("a", "b", "c").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        var result = split.Split("b");

        Assert.Equal(GroupViewMode.Split, Card(result).ViewMode);
        Assert.Equal(new[] { "a", "c" }, Card(result).MemberIds);
    }

    [Fact]
    public void Split_OutOfATwoSplit_DissolvesTheGroupIntoPlainCards()
    {
        var split = Group("a", "b").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout;

        var result = split.Split("b");

        Assert.All(result.Columns[0].Widgets, w => Assert.False(w.IsGroup));
        Assert.All(result.Columns[0].Widgets, w => Assert.Equal(GroupViewMode.Tabs, w.ViewMode));
    }

    [Fact]
    public void Split_OutOfAFourTabsGroup_StaysTabs()
    {
        var result = Group("a", "b", "c", "d").Split("d");

        Assert.Equal(GroupViewMode.Tabs, Card(result).ViewMode);
        Assert.Equal(3, Card(result).MemberIds.Count);
    }

    [Fact]
    public void Split_GivesTheLeavingWidgetAFreshWeight_AndKeepsTheOthers()
    {
        var split = Group("a", "b", "c").SetViewMode("a", GroupViewMode.Split, 900, Min).Layout.ResizePanes("a", 0.1, 0.1, 0.1);

        var rest = Card(split.Split("c"));

        Assert.Equal(1.3, rest.Members![0].WidthWeight, 6);
        Assert.Equal(0.7, rest.Members[1].WidthWeight, 6);
    }
}
