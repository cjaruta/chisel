using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Resizing columns moves width weight between a column and the one right of it. The delta and the minimum are
// fractions of the total width; the weight total never changes, so the columns always fill the window.
public class WidgetLayoutResizeColumnsTests
{
    private const double Tolerance = 1e-9;

    // Widths 1 + 2 + 1 = 4, so a fraction f of the window is f * 4 in weight.
    private static readonly WidgetLayout Sample = new(new[]
    {
        new LayoutColumn(1, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(2, new[] { new WidgetPlacement("tasks"), new WidgetPlacement("links") }),
        new LayoutColumn(1, new[] { new WidgetPlacement("notes") }),
    });

    private static double[] Widths(WidgetLayout layout) => layout.Columns.Select(c => c.WidthWeight).ToArray();

    private static void AssertWidths(double[] expected, WidgetLayout layout)
    {
        var actual = Widths(layout);
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], Tolerance);
        }
    }

    [Fact]
    public void PositiveDelta_WidensTheColumnAndNarrowsItsRightNeighbour()
    {
        AssertWidths(new[] { 1.4, 1.6, 1 }, Sample.ResizeColumns(0, 0.1, 0.05));
    }

    [Fact]
    public void NegativeDelta_NarrowsTheColumnAndWidensItsRightNeighbour()
    {
        AssertWidths(new[] { 1, 1.6, 1.4 }, Sample.ResizeColumns(1, -0.1, 0.05));
    }

    [Fact]
    public void StopsAtTheMinimumOnEitherSide()
    {
        // Minimum 0.2 of 4 = 0.8 weight each.
        AssertWidths(new[] { 2.2, 0.8, 1 }, Sample.ResizeColumns(0, 5, 0.2));
        AssertWidths(new[] { 0.8, 2.2, 1 }, Sample.ResizeColumns(0, -5, 0.2));
    }

    [Fact]
    public void TotalAndOtherPartsAreUnchanged()
    {
        var resized = Sample.ResizeColumns(0, 0.13, 0.05);

        Assert.Equal(4, Widths(resized).Sum(), Tolerance);
        Assert.Equal(Sample.Columns[2], resized.Columns[2]);
        Assert.Equal(Sample.Columns[0].Widgets, resized.Columns[0].Widgets);
        Assert.Equal(Sample.WidgetIds, resized.WidgetIds);
    }

    [Fact]
    public void ManySmallDeltas_AddUpToOneLargeOne()
    {
        var steps = Sample;
        for (var i = 0; i < 10; i++)
        {
            steps = steps.ResizeColumns(1, 0.01, 0.05);
        }
        AssertWidths(Widths(Sample.ResizeColumns(1, 0.1, 0.05)), steps);
    }

    [Fact]
    public void NoRoomForBothMinimums_ChangesNothing()
    {
        Assert.Same(Sample, Sample.ResizeColumns(0, 0.1, 0.5));
    }

    [Fact]
    public void ZeroDelta_ReturnsTheSameLayout()
    {
        Assert.Same(Sample, Sample.ResizeColumns(0, 0, 0.05));
    }

    [Fact]
    public void TheLastColumnAndUnknownColumnsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Sample.ResizeColumns(2, 0.1, 0.05));
        Assert.Throws<ArgumentException>(() => Sample.ResizeColumns(-1, 0.1, 0.05));
        Assert.Throws<ArgumentException>(() => Sample.ResizeColumns(7, 0.1, 0.05));
    }
}
