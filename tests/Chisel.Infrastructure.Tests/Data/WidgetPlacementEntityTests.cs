using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class WidgetPlacementEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void WidgetPlacements_AddAndSave_CanBeReadBackInOrder()
    {
        using var context = _fixture.CreateContext();
        context.WidgetPlacements.AddRange(
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "tasks", Column = Column.Side, Position = 1 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "clock", Column = Column.Side, Position = 0 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "notes", Column = Column.Main, Position = 0 });
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var side = readContext.WidgetPlacements
            .Where(p => p.Column == Column.Side)
            .OrderBy(p => p.Position)
            .Select(p => p.WidgetId)
            .ToList();

        Assert.Equal(new[] { "clock", "tasks" }, side);
    }

    public void Dispose() => _fixture.Dispose();
}