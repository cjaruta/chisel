using Chisel.Core.Layout;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class LayoutProfileRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    // Columns, weights, a header choice, a split group with an active tab, and a hidden widget: all of it must come back.
    private static readonly WidgetLayout Rich = new(
        new[]
        {
            new LayoutColumn(0.8, new[] { new WidgetPlacement("tasks", 2, false), new WidgetPlacement("links", 0.6) }),
            new LayoutColumn(1.4, new[]
            {
                new WidgetPlacement("schedule", 1.5, null,
                    new[] { new GroupMember("schedule", true, 1.2), new GroupMember("notes", null, 0.8) }, 1, GroupViewMode.Split),
            }),
        },
        new[] { "weather" });

    [Fact]
    public void GetAll_OnEmptyDatabase_ReturnsNothing()
    {
        using var context = _fixture.CreateContext();

        Assert.Empty(new LayoutProfileRepository(context).GetAll());
    }

    [Fact]
    public void Save_ThenGetAll_RoundTripsTheWholeLayout_InSlotOrder()
    {
        using (var context = _fixture.CreateContext())
        {
            var repository = new LayoutProfileRepository(context);
            repository.Save(new LayoutProfile(3, "Profile 3", Rich));
            repository.Save(new LayoutProfile(1, "Work", Rich));
        }

        using var readContext = _fixture.CreateContext();
        var all = new LayoutProfileRepository(readContext).GetAll();

        Assert.Equal(new[] { 1, 3 }, all.Select(p => p.Slot));
        Assert.Equal("Work", all[0].Name);
        Assert.Equal(Rich.Describe(), all[0].Layout.Describe());
        Assert.False(all[0].Layout.Columns[0].Widgets[0].ShowHeader);
    }

    [Fact]
    public void Save_ToAUsedSlot_ReplacesIt_AndDeleteEmptiesIt()
    {
        using var context = _fixture.CreateContext();
        var repository = new LayoutProfileRepository(context);
        repository.Save(new LayoutProfile(2, "Profile 2", Rich));

        repository.Save(new LayoutProfile(2, "Profile 2", new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("schedule") }) })));
        Assert.Equal("1[schedule:1]", Assert.Single(repository.GetAll()).Layout.Describe());

        repository.Delete(2);
        repository.Delete(2);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Save_ToASlotThatDoesNotExist_IsRefused()
    {
        using var context = _fixture.CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutProfileRepository(context).Save(new LayoutProfile(4, "x", Rich)));
    }

    public void Dispose() => _fixture.Dispose();
}
