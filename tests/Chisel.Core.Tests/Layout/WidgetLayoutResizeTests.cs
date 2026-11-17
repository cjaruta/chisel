using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Resizing moves height weight between a widget and the one right below it. The delta and the minimum are
// fractions of the column's height; the column's weight total never changes, so it always fills its height.
public class WidgetLayoutResizeTests
{
    private const double Tolerance = 1e-9;

    // Right column weights 1 + 2 + 1 = 4, so a fraction f of the column is f * 4 in weight.
    private static readonly WidgetLayout Sample = new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1), new WidgetPlacement("notes", 2), new WidgetPlacement("links", 1) }),
    });

    private static double[] Heights(WidgetLayout layout, int column) =>
        layout.Columns[column].Widgets.Select(w => w.HeightWeight).ToArray();

    private static void AssertHeights(double[] expected, WidgetLayout layout, int column)
    {
        var actual = Heights(layout, column);
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], Tolerance);
        }
    }

    [Fact]
    public void Growing_TakesWeightFromTheWidgetBelow()
    {
        AssertHeights(new[] { 1.5, 1.5, 1.0 }, Sample.Resize("tasks", 0.125, 0.05), 1);
    }

    [Fact]
    public void Shrinking_GivesWeightToTheWidgetBelow()
    {
        AssertHeights(new[] { 1.0, 1.6, 1.4 }, Sample.Resize("notes", -0.1, 0.05), 1);
    }

    [Theory]
    [InlineData("tasks", 0.3)]
    [InlineData("tasks", -0.2)]
    [InlineData("notes", 0.2)]
    [InlineData("notes", -0.45)]
    public void TheColumnTotalNeverChanges(string id, double delta)
    {
        Assert.Equal(4, Heights(Sample.Resize(id, delta, 0.1), 1).Sum(), Tolerance);
    }

    [Fact]
    public void GrowingStopsWhenTheWidgetBelowReachesTheMinimum()
    {
        // Notes can give at most 2 - 0.2*4 = 1.2.
        AssertHeights(new[] { 2.2, 0.8, 1.0 }, Sample.Resize("tasks", 0.9, 0.2), 1);
    }

    [Fact]
    public void ShrinkingStopsWhenTheWidgetItselfReachesTheMinimum()
    {
        // Tasks can give at most 1 - 0.2*4 = 0.2.
        AssertHeights(new[] { 0.8, 2.2, 1.0 }, Sample.Resize("tasks", -0.9, 0.2), 1);
    }

    [Fact]
    public void AtTheMinimumAlready_FurtherShrinkingIsANoOp()
    {
        var atMinimum = Sample.Resize("tasks", -0.9, 0.2);

        Assert.Same(atMinimum, atMinimum.Resize("tasks", -0.1, 0.2));
    }

    [Fact]
    public void WhenTheMinimumsCannotBothFit_NothingChanges()
    {
        // Each would need 0.4 * 4 = 1.6, but tasks and notes only have 3 between them.
        Assert.Same(Sample, Sample.Resize("tasks", 0.1, 0.4));
        Assert.Same(Sample, Sample.Resize("tasks", -0.1, 0.4));
    }

    [Fact]
    public void BelowTheMinimumAlready_CanOnlyGrowTowardIt()
    {
        // Tasks (1) is under the 0.3 * 4 = 1.2 minimum: it may take weight from notes, never give any.
        AssertHeights(new[] { 1.4, 1.6, 1.0 }, Sample.Resize("tasks", 0.1, 0.3), 1);
        Assert.Same(Sample, Sample.Resize("tasks", -0.1, 0.3));
    }

    [Fact]
    public void ZeroDelta_IsANoOp()
    {
        Assert.Same(Sample, Sample.Resize("notes", 0, 0.1));
    }

    [Fact]
    public void OtherWidgetsAndColumnsAreUntouched()
    {
        var resized = Sample.Resize("tasks", 0.1, 0.05);

        Assert.Equal(1.0, Heights(resized, 1)[2]);
        Assert.Equal(Sample.Columns[0], resized.Columns[0]);
        Assert.Equal(new[] { 1.3, 1.0 }, resized.Columns.Select(c => c.WidthWeight));
        Assert.Equal(Sample.WidgetIds, resized.WidgetIds);
    }

    [Fact]
    public void ManySmallDeltas_AddUpToOneLargeOne()
    {
        var stepped = Sample;
        for (var i = 0; i < 10; i++)
        {
            stepped = stepped.Resize("tasks", 0.02, 0.05);
        }

        AssertHeights(Heights(Sample.Resize("tasks", 0.2, 0.05), 1), stepped, 1);
    }

    [Theory]
    [InlineData("links")]
    [InlineData("schedule")]
    public void TheLastWidgetInAColumnOrALoneWidget_IsRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => Sample.Resize(id, 0.1, 0.05));
    }

    [Fact]
    public void AnUnknownWidget_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Sample.Resize("weather", 0.1, 0.05));
    }
}
