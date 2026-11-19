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

    public static TheoryData<string> ColumnCounts => new()
    {
        "1[schedule:1, tasks:0.5, notes:1.25, links:2.25]",
        "1.3[schedule:1] | 1[tasks:1.05, notes:1.1, links:0.85]",
        "1.3[schedule:1] | 1[tasks:1.2, links:0.8] | 1.15[notes:1]",
        "1.15[links:1] | 1.3[schedule:1] | 1[tasks:1] | 1.15[notes:1]",
    };

    [Theory]
    [MemberData(nameof(ColumnCounts))]
    public void LayoutsWithOneToFourColumns_SaveAndLoadUnchanged(string described)
    {
        var layout = Parse(described);
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(layout);

        using var readContext = _fixture.CreateContext();

        Assert.Equal(described, new WidgetLayoutRepository(readContext).Get()!.Describe());
    }

    [Fact]
    public void GoingFromFourColumnsBackToTwo_LeavesNoStaleColumns()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        repository.Save(Parse("1.15[links:1] | 1.3[schedule:1] | 1[tasks:1] | 1.15[notes:1]"));

        repository.Save(Parse("1.3[schedule:1] | 1[tasks:1, notes:1, links:1]"));

        using var readContext = _fixture.CreateContext();
        Assert.Equal("1.3[schedule:1] | 1[tasks:1, notes:1, links:1]", new WidgetLayoutRepository(readContext).Get()!.Describe());
        Assert.Equal(2, readContext.LayoutColumns.Count());
    }

    [Fact]
    public void HeaderChoice_RoundTrips_IncludingNoChoice()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement("schedule", 1, true),
                new WidgetPlacement("tasks", 1, false),
                new WidgetPlacement("notes"),
            }),
        });
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(layout);

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get()!.Columns[0].Widgets;

        Assert.Equal(new bool?[] { true, false, null }, loaded.Select(w => w.ShowHeader).ToArray());
    }

    // The inverse of WidgetLayout.Describe, for writing test layouts compactly.
    private static WidgetLayout Parse(string described) =>
        new(described.Split(" | ").Select(column =>
        {
            var open = column.IndexOf('[');
            var width = double.Parse(column[..open], System.Globalization.CultureInfo.InvariantCulture);
            var widgets = column[(open + 1)..^1].Split(", ").Select(w =>
            {
                var parts = w.Split(':');
                return new WidgetPlacement(parts[0], double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            });
            return new LayoutColumn(width, widgets.ToList());
        }));

    [Fact]
    public void HiddenWidgets_SaveAndLoadStillHidden()
    {
        var layout = new WidgetLayout(
            new[] { new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }), new LayoutColumn(1, new[] { new WidgetPlacement("tasks") }) },
            new[] { "notes", "links" });
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(layout);

        using var readContext = _fixture.CreateContext();
        var loaded = new WidgetLayoutRepository(readContext).Get()!;

        Assert.Equal("1.3[schedule:1] | 1[tasks:1] (hidden: notes, links)", loaded.Describe());
    }

    [Fact]
    public void SavingAgain_ReplacesTheHiddenWidgets()
    {
        using var context = _fixture.CreateContext();
        var repository = new WidgetLayoutRepository(context);
        repository.Save(new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("schedule") }) }, new[] { "notes", "links" }));

        repository.Save(new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement("schedule"), new WidgetPlacement("notes") }) }, new[] { "links" }));

        using var readContext = _fixture.CreateContext();
        Assert.Equal(new[] { "links" }, new WidgetLayoutRepository(readContext).Get()!.HiddenWidgetIds);
        Assert.Single(readContext.HiddenWidgets);
    }

    [Fact]
    public void AWidgetThatWasNeverSaved_IsPlacedAtTheBottomOfTheFirstColumnWhenLoaded()
    {
        // Saved before "links" existed: it is neither placed nor hidden.
        using var context = _fixture.CreateContext();
        new WidgetLayoutRepository(context).Save(Parse("1.3[schedule:1] | 1[tasks:1, notes:1]"));
        var defaults = Parse("1.3[schedule:1] | 1[tasks:1, notes:1, links:1]");

        using var readContext = _fixture.CreateContext();
        var loaded = WidgetLayoutNormalizer.Normalize(new WidgetLayoutRepository(readContext).Get()!, defaults);

        Assert.Equal("1.3[schedule:1, links:1] | 1[tasks:1, notes:1]", loaded.Describe());
    }

    public void Dispose() => _fixture.Dispose();
}
