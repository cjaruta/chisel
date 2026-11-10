using Chisel.Core.Layout;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WidgetLayoutRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Get_OnEmptyDatabase_ReturnsEmptyLayout()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);

        var layout = repository.Get();

        Assert.Empty(layout.Side);
        Assert.Empty(layout.Main);
    }

    [Fact]
    public void Save_ThenGet_RoundTripsBothColumns()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        var layout = new WidgetLayout();
        layout.Move("clock", Column.Side, 0);
        layout.Move("tasks", Column.Side, 1);
        layout.Move("notes", Column.Main, 0);

        repository.Save(layout);

        using var readContext = _fixture.CreateContext();
        var saved = new WidgetLayoutRepository(readContext).Get();

        Assert.Equal(new[] { "clock", "tasks" }, saved.Side);
        Assert.Equal(new[] { "notes" }, saved.Main);
    }

    [Fact]
    public void Save_CalledAgain_ReplacesThePreviousLayout()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        var first = new WidgetLayout();
        first.Move("clock", Column.Side, 0);
        repository.Save(first);

        var second = new WidgetLayout();
        second.Move("notes", Column.Main, 0);
        repository.Save(second);

        using var readContext = _fixture.CreateContext();
        var saved = new WidgetLayoutRepository(readContext).Get();

        Assert.Empty(saved.Side);
        Assert.Equal(new[] { "notes" }, saved.Main);
    }

    public void Dispose() => _fixture.Dispose();
}
