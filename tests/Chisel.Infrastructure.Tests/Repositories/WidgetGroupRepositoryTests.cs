using Chisel.Core.Layout;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WidgetGroupRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private static readonly WidgetLayout Grouped = new(new[]
    {
        new LayoutColumn(1, new[]
        {
            new WidgetPlacement("notes", 1.5, null,
                new[] { new GroupMember("notes", true), new GroupMember("tasks"), new GroupMember("weather", false) }, 2),
            new WidgetPlacement("links", 0.5, false),
        }),
    }, new[] { "media" });

    [Fact]
    public void Save_ThenGet_RoundTripsGroupsMembersRememberedHeadersAndTheActiveTab()
    {
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(Grouped);

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get()!;

        var group = loaded.Columns[0].Widgets[0];
        Assert.Equal(new[] { "notes", "tasks", "weather" }, group.MemberIds);
        Assert.Equal(new bool?[] { true, null, false }, group.Members!.Select(m => m.ShowHeader).ToArray());
        Assert.Equal(2, group.ActiveIndex);
        Assert.Equal("notes", group.WidgetId);
        Assert.Equal(1.5, group.HeightWeight);
        Assert.False(loaded.Columns[0].Widgets[1].IsGroup);
        Assert.False(loaded.Columns[0].Widgets[1].ShowHeader);
        Assert.Equal(new[] { "media" }, loaded.HiddenWidgetIds);
    }

    [Fact]
    public void Save_ThenGet_KeepsAPlainLayoutPlain()
    {
        using var context = _fixture.CreateContext();
        var plain = new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("tasks"), new WidgetPlacement("notes") }) });
        new WidgetLayoutRepository(context).Save(plain);

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get()!;

        Assert.All(loaded.Columns[0].Widgets, w => Assert.False(w.IsGroup));
        Assert.Equal("1[tasks:1, notes:1]", loaded.Describe());
    }

    [Fact]
    public void Save_ASecondTime_ReplacesTheGroup()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        repository.Save(Grouped);
        repository.Save(new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("notes") }) }));

        using var readContext = _fixture.CreateContext();
        Assert.False(new WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets[0].IsGroup);
    }

    public void Dispose() => _fixture.Dispose();
}
