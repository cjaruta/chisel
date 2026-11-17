using Chisel.Core.Layout;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WidgetLayoutRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private static readonly WidgetLayout Custom = new(new[]
    {
        new LayoutColumn(0.8, new[] { new WidgetPlacement("tasks", 2), new WidgetPlacement("links", 0.6) }),
        new LayoutColumn(1.4, new[] { new WidgetPlacement("schedule") }),
        new LayoutColumn(1.1, new[] { new WidgetPlacement("notes", 1.25) }),
    });

    [Fact]
    public void Get_OnEmptyDatabase_ReturnsNull()
    {
        using var context = _fixture.CreateContext();

        Assert.Null(new WidgetLayoutRepository(context).Get());
    }

    [Fact]
    public void Save_ThenGet_RoundTripsColumnWidthsOrderAndHeightWeights()
    {
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(Custom);

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get();

        Assert.Equal("0.8[tasks:2, links:0.6] | 1.4[schedule:1] | 1.1[notes:1.25]", loaded!.Describe());
    }

    [Fact]
    public void Save_CalledAgain_ReplacesThePreviousLayout()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        repository.Save(Custom);

        repository.Save(new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("schedule") }) }));

        using var readContext = _fixture.CreateContext();
        Assert.Equal("1[schedule:1]", new WidgetLayoutRepository(readContext).Get()!.Describe());
        Assert.Single(readContext.LayoutColumns);
        Assert.Single(readContext.WidgetPlacements);
    }

    public void Dispose() => _fixture.Dispose();
}
