using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class WidgetPlacementEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void WidgetPlacements_AddAndSave_CanBeReadBackInOrderWithWeights()
    {
        using var context = _fixture.CreateContext();
        context.WidgetPlacements.AddRange(
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "notes", ColumnIndex = 1, Position = 1, HeightWeight = 1.2 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "tasks", ColumnIndex = 1, Position = 0, HeightWeight = 0.8 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "schedule", ColumnIndex = 0, Position = 0 });
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var right = readContext.WidgetPlacements
            .Where(p => p.ColumnIndex == 1)
            .OrderBy(p => p.Position)
            .Select(p => new { p.WidgetId, p.HeightWeight })
            .ToList();

        Assert.Equal(new[] { ("tasks", 0.8), ("notes", 1.2) }, right.Select(p => (p.WidgetId, p.HeightWeight)));
    }

    [Fact]
    public void HeightWeight_WhenNotSet_IsOne()
    {
        Assert.Equal(1, new WidgetPlacementEntity().HeightWeight);
    }

    [Fact]
    public void LayoutColumns_AddAndSave_CanBeReadBack()
    {
        using var context = _fixture.CreateContext();
        context.LayoutColumns.AddRange(
            new LayoutColumnEntity { Position = 1, WidthWeight = 1 },
            new LayoutColumnEntity { Position = 0, WidthWeight = 1.3 });
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var widths = readContext.LayoutColumns.OrderBy(c => c.Position).Select(c => c.WidthWeight).ToList();

        Assert.Equal(new[] { 1.3, 1.0 }, widths);
    }

    public void Dispose() => _fixture.Dispose();
}
