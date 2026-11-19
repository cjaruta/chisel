using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class WidgetInstanceRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Save_ThenGet_RoundTripsSeveralInstancesOfOneType_Alone_InATabGroupAndInSplitPanes()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement("frame:aaa111"),
                new WidgetPlacement("frame:bbb222", 1, false),
                new WidgetPlacement("frame:ccc333", 1, null,
                    new[] { new GroupMember("frame:ccc333", true, 1.5), new GroupMember("frame:ddd444"), new GroupMember("notes", false, 0.5) }, 1),
                new WidgetPlacement("frame:eee555", 1, null,
                    new[] { new GroupMember("frame:eee555"), new GroupMember("frame:fff666", null, 2) }, 0, GroupViewMode.Split),
            }),
        }, new[] { "media" });
        using (var context = _fixture.CreateContext())
        {
            new WidgetLayoutRepository(context).Save(layout);
        }

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get()!;

        Assert.Equal(layout.Describe(), loaded.Describe());
        var tabs = loaded.Columns[0].Widgets[2];
        Assert.Equal(new[] { "frame:ccc333", "frame:ddd444", "notes" }, tabs.MemberIds);
        Assert.Equal(new bool?[] { true, null, false }, tabs.Members!.Select(m => m.ShowHeader).ToArray());
        Assert.Equal(new[] { 1.5, 1, 0.5 }, tabs.Members!.Select(m => m.WidthWeight).ToArray());
        Assert.Equal("frame:ddd444", tabs.ActiveId);
        Assert.False(loaded.Columns[0].Widgets[1].ShowHeader);
        Assert.Equal(GroupViewMode.Split, loaded.Columns[0].Widgets[3].ViewMode);
    }

    [Fact]
    public void Get_ReadsOlderGroupRows_WithoutAKeyOrAWeight()
    {
        using (var context = _fixture.CreateContext())
        {
            context.LayoutColumns.Add(new LayoutColumnEntity { Position = 0, WidthWeight = 1 });
            context.WidgetPlacements.Add(new WidgetPlacementEntity
            {
                Id = Guid.NewGuid(), WidgetId = "notes", ColumnIndex = 0, Position = 0, HeightWeight = 1,
                GroupMembers = "notes:t;tasks:n;links:f", ActiveTab = 1,
            });
            context.WidgetPlacements.Add(new WidgetPlacementEntity
            {
                Id = Guid.NewGuid(), WidgetId = "clock", ColumnIndex = 0, Position = 1, HeightWeight = 1,
                GroupMembers = "clock:n:2;weather:f:0.5",
            });
            context.SaveChanges();
        }

        using var readContext = _fixture.CreateContext();
        var widgets = new WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets;

        Assert.Equal(new[] { "notes", "tasks", "links" }, widgets[0].MemberIds);
        Assert.Equal(new bool?[] { true, null, false }, widgets[0].Members!.Select(m => m.ShowHeader).ToArray());
        Assert.Equal(new[] { 1.0, 1, 1 }, widgets[0].Members!.Select(m => m.WidthWeight).ToArray());
        Assert.Equal(new[] { "clock", "weather" }, widgets[1].MemberIds);
        Assert.Equal(new[] { 2.0, 0.5 }, widgets[1].Members!.Select(m => m.WidthWeight).ToArray());
    }

    [Fact]
    public void TwoPlacementRows_MayShareATypeButNotAnInstance()
    {
        // Rows are keyed by their own Guid, so several rows of one type are fine (no unique index on the widget id).
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("frame:a"), new WidgetPlacement("frame:b") }),
        }));

        Assert.Equal(2, context.WidgetPlacements.Count(p => p.WidgetId.StartsWith("frame:")));
    }

    public void Dispose() => _fixture.Dispose();
}
