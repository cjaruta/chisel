using Chisel.Core.Layout;
using Microsoft.EntityFrameworkCore;
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
    public void Save_ThenGet_RoundTripsTheViewModeAndPaneWidths()
    {
        var split = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement("notes", 1, null,
                    new[] { new GroupMember("notes", true, 1.25), new GroupMember("tasks", null, 0.75) }, 1, GroupViewMode.Split),
            }),
        });
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(split);

        using var readContext = _fixture.CreateContext();
        var group = new WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets[0];

        Assert.Equal(GroupViewMode.Split, group.ViewMode);
        Assert.Equal(new[] { 1.25, 0.75 }, group.Members!.Select(m => m.WidthWeight).ToArray());
        Assert.Equal(new bool?[] { true, null }, group.Members!.Select(m => m.ShowHeader).ToArray());
        Assert.Equal(1, group.ActiveIndex);
    }

    [Fact]
    public void Get_LoadsAGroupSavedBeforeSplitViewAsTabsWithEqualWidths()
    {
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(Grouped);
        context.Database.ExecuteSqlRaw("UPDATE WidgetPlacements SET GroupMembers = 'notes:t;tasks:n;weather:f' WHERE GroupMembers IS NOT NULL");

        using var readContext = _fixture.CreateContext();
        var group = new WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets[0];

        Assert.Equal(GroupViewMode.Tabs, group.ViewMode);
        Assert.All(group.Members!, m => Assert.Equal(1, m.WidthWeight));
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
