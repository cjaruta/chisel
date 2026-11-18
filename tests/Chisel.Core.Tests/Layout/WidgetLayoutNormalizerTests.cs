using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Normalizing a loaded layout against the default: unknown widgets go, missing widgets come back
// in their default place, empty columns are dropped and weights are kept sensible.
public class WidgetLayoutNormalizerTests
{
    private static readonly WidgetLayout Defaults = new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(1, new[] { new WidgetPlacement("tasks"), new WidgetPlacement("notes", 1.2), new WidgetPlacement("links", 0.8) }),
    });

    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params (string Id, double Height)[] widgets) =>
        new(width, widgets.Select(w => new WidgetPlacement(w.Id, w.Height)).ToList());

    private static string Normalize(WidgetLayout layout) => WidgetLayoutNormalizer.Normalize(layout, Defaults).Describe();

    [Fact]
    public void DefaultLayout_IsUnchanged()
    {
        Assert.Equal(Defaults.Describe(), Normalize(Defaults));
    }

    [Fact]
    public void CustomLayoutWithEveryWidget_IsKept()
    {
        var layout = Layout(Col(1, ("tasks", 2), ("schedule", 1)), Col(2, ("links", 1)), Col(0.5, ("notes", 3)));

        Assert.Equal("1[tasks:2, schedule:1] | 2[links:1] | 0.5[notes:3]", Normalize(layout));
    }

    [Fact]
    public void UnknownWidgetIds_AreIgnored()
    {
        var layout = Layout(Col(1.3, ("schedule", 1), ("weather", 1)), Col(1, ("tasks", 1), ("notes", 1.2), ("media", 1), ("links", 0.8)));

        Assert.Equal(Defaults.Describe(), Normalize(layout));
    }

    [Fact]
    public void DuplicateWidgets_KeepOnlyTheFirst()
    {
        var layout = Layout(Col(1.3, ("schedule", 1), ("tasks", 1)), Col(1, ("tasks", 5), ("notes", 1.2), ("links", 0.8)));

        Assert.Equal("1.3[schedule:1, tasks:1] | 1[notes:1.2, links:0.8]", Normalize(layout));
    }

    [Fact]
    public void NeverSeenWidget_IsAddedAtTheBottomOfTheFirstColumn()
    {
        // Notes is neither placed nor hidden, so it is new: it joins the first column with its default
        // height, and that column is rescaled to average 1.
        var layout = Layout(Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("links", 0.8)));

        Assert.Equal("1.3[schedule:0.909, notes:1.091] | 1[tasks:1, links:0.8]", Normalize(layout));
    }

    [Fact]
    public void SeveralNeverSeenWidgets_JoinTheFirstColumnInRegistryOrder()
    {
        var layout = Layout(Col(1, ("tasks", 1)));

        Assert.Equal("1[tasks:1, schedule:1, notes:1.2, links:0.8]", Normalize(layout));
    }

    [Fact]
    public void HiddenWidget_StaysHidden()
    {
        var layout = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("links", 0.8)) }, new[] { "notes" });

        Assert.Equal("1.3[schedule:1] | 1[tasks:1, links:0.8] (hidden: notes)", Normalize(layout));
    }

    [Fact]
    public void HiddenList_DropsUnknownWidgetsAndWidgetsThatArePlaced()
    {
        var layout = new WidgetLayout(Defaults.Columns, new[] { "weather", "tasks" });

        Assert.Equal(Defaults.Describe(), Normalize(layout));
    }

    [Fact]
    public void HiddenWidgets_AreListedInRegistryOrder()
    {
        var layout = new WidgetLayout(new[] { Col(1, ("tasks", 1)) }, new[] { "links", "schedule", "notes" });

        Assert.Equal("1[tasks:1] (hidden: schedule, notes, links)", Normalize(layout));
    }

    [Fact]
    public void NothingPlaced_BecomesTheDefault()
    {
        var layout = new WidgetLayout(Array.Empty<LayoutColumn>(), new[] { "schedule", "tasks", "notes", "links" });

        Assert.Equal(Defaults.Describe(), Normalize(layout));
    }

    [Fact]
    public void EmptyColumns_AreDropped()
    {
        var layout = Layout(Col(1), Col(1.3, ("schedule", 1)), Col(2), Col(1, ("tasks", 1), ("notes", 1.2), ("links", 0.8)), Col(1));

        Assert.Equal(Defaults.Describe(), Normalize(layout));
    }

    [Fact]
    public void ColumnLeftWithOnlyUnknownWidgets_IsDropped()
    {
        var layout = Layout(Col(1.3, ("schedule", 1)), Col(3, ("weather", 1)), Col(1, ("tasks", 1), ("notes", 1.2), ("links", 0.8)));

        Assert.Equal(Defaults.Describe(), Normalize(layout));
    }

    [Fact]
    public void EmptyLayout_BecomesTheDefault()
    {
        Assert.Equal(Defaults.Describe(), Normalize(Layout()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(0.01)]
    public void TooSmallWeights_AreRaisedToTheMinimum(double weight)
    {
        var layout = Layout(Col(weight, ("schedule", weight)), Col(1, ("tasks", 1), ("notes", 1.2), ("links", 0.8)));

        var normalized = WidgetLayoutNormalizer.Normalize(layout, Defaults);

        Assert.Equal(WidgetLayout.MinimumWeight, normalized.Columns[0].WidthWeight);
        Assert.Equal(WidgetLayout.MinimumWeight, normalized.Columns[0].Widgets[0].HeightWeight);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidWeights_BecomeOne(double weight)
    {
        var layout = Layout(Col(weight, ("schedule", weight)), Col(1, ("tasks", 1), ("notes", 1.2), ("links", 0.8)));

        var normalized = WidgetLayoutNormalizer.Normalize(layout, Defaults);

        Assert.Equal(1, normalized.Columns[0].WidthWeight);
        Assert.Equal(1, normalized.Columns[0].Widgets[0].HeightWeight);
    }

    [Fact]
    public void MoreColumnsThanTheMaximum_FoldIntoTheLastAllowedColumn()
    {
        var layout = Layout(
            Col(1, ("schedule", 1)), Col(1, ("tasks", 1)), Col(1, ("notes", 1)),
            Col(1, ("links", 1)), Col(1, ("extra", 1)));
        var defaults = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("schedule"), new WidgetPlacement("tasks"), new WidgetPlacement("notes"),
                new WidgetPlacement("links"), new WidgetPlacement("extra") }),
        });

        var normalized = WidgetLayoutNormalizer.Normalize(layout, defaults);

        Assert.Equal(LayoutLimits.MaximumColumns, normalized.Columns.Count);
        Assert.Equal("1[schedule:1] | 1[tasks:1] | 1[notes:1] | 1[links:1, extra:1]", normalized.Describe());
    }
}

public class WidgetLayoutNormalizerHiddenByDefaultTests
{
    private static readonly WidgetLayout Defaults = new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(1, new[] { new WidgetPlacement("tasks") }),
    }, new[] { "clock" });

    [Fact]
    public void SavedLayoutWithoutTheWidget_KeepsItHidden()
    {
        var saved = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("schedule", 1) }),
            new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1) }),
        });

        var result = WidgetLayoutNormalizer.Normalize(saved, Defaults);

        Assert.Equal(new[] { "clock" }, result.HiddenWidgetIds);
        Assert.Equal(new[] { "schedule", "tasks" }, result.WidgetIds);
    }

    [Fact]
    public void SavedLayoutWithTheWidgetPlaced_KeepsItPlaced()
    {
        var saved = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("schedule", 1), new WidgetPlacement("clock", 1) }),
            new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1) }),
        });

        var result = WidgetLayoutNormalizer.Normalize(saved, Defaults);

        Assert.Empty(result.HiddenWidgetIds);
        Assert.Contains("clock", result.WidgetIds);
    }
}
