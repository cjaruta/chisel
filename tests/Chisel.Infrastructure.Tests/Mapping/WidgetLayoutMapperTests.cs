using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class WidgetLayoutMapperTests
{
    private static readonly WidgetLayout Sample = new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(0.9, new[] { new WidgetPlacement("tasks", 0.7), new WidgetPlacement("notes", 1.5), new WidgetPlacement("links") }),
    });

    [Fact]
    public void ToEntities_GivesColumnIndexPositionAndWeights()
    {
        var (columns, placements) = Sample.ToEntities();

        Assert.Equal(new[] { (0, 1.3), (1, 0.9) }, columns.Select(c => (c.Position, c.WidthWeight)));
        Assert.Equal(
            new[] { ("schedule", 0, 0, 1.0), ("tasks", 1, 0, 0.7), ("notes", 1, 1, 1.5), ("links", 1, 2, 1.0) },
            placements.Select(p => (p.WidgetId, p.ColumnIndex, p.Position, p.HeightWeight)));
        Assert.All(placements, p => Assert.NotEqual(Guid.Empty, p.Id));
    }

    [Fact]
    public void ToLayout_RoundTripsColumnsWidthsAndHeights()
    {
        var (columns, placements) = Sample.ToEntities();

        var layout = WidgetLayoutMapper.ToLayout(columns.AsEnumerable().Reverse(), placements.AsEnumerable().Reverse());

        Assert.Equal(Sample.Describe(), layout.Describe());
    }

    [Fact]
    public void ToLayout_OrdersColumnsByIndexEvenWithGapsAndUsesWidthOneWhenAColumnRowIsMissing()
    {
        var placements = new[]
        {
            new WidgetPlacementEntity { WidgetId = "tasks", ColumnIndex = 4, Position = 0, HeightWeight = 1 },
            new WidgetPlacementEntity { WidgetId = "schedule", ColumnIndex = 2, Position = 0, HeightWeight = 1 },
        };

        var layout = WidgetLayoutMapper.ToLayout(new[] { new LayoutColumnEntity { Position = 2, WidthWeight = 1.3 } }, placements);

        Assert.Equal("1.3[schedule:1] | 1[tasks:1]", layout.Describe());
    }
}
