using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ClockFaceRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private static CustomClockFace Face(string name) =>
        new(Guid.NewGuid(), name, ClockPresets.All[2].Style with { Alignment = ClockAlignment.Right, Colors = new ClockColors("#112233", null, null, "#445566", null, null) },
            new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Scrolling(ClockTool.Timer, ClockTool.Alarm)]));

    [Fact]
    public void Empty_HasNoFaces()
    {
        using var context = _fixture.CreateContext();

        Assert.Empty(new ClockFaceRepository(context).Load());
    }

    [Fact]
    public void Save_RoundTripsTheFacesInOrder_AndReplacesThePreviousList()
    {
        var first = Face("Day");
        var second = Face("Night");
        using (var context = _fixture.CreateContext())
        {
            var repository = new ClockFaceRepository(context);
            repository.Save([first, second]);
            repository.Save([second, first]);
        }

        using var read = _fixture.CreateContext();
        var back = new ClockFaceRepository(read).Load();
        Assert.Equal(new[] { "Night", "Day" }, back.Select(f => f.Name).ToArray());
        Assert.Equal(second.Style, back[0].Style);
        Assert.Equal(second.Slots, back[0].Slots);
        Assert.Equal(first.Id, back[1].Id);
    }

    [Fact]
    public void SavingNothing_ClearsTheList()
    {
        using var context = _fixture.CreateContext();
        var repository = new ClockFaceRepository(context);
        repository.Save([Face("Day")]);

        repository.Save([]);

        Assert.Empty(new ClockFaceRepository(context).Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"version\":1,\"faces\":[")]
    [InlineData("")]
    public void CorruptSavedText_LoadsAsNoFaces_WithoutThrowing(string text)
    {
        using var context = _fixture.CreateContext();
        context.ClockFaceLibraries.Add(new ClockFaceLibraryEntity { Json = text });
        context.SaveChanges();

        Assert.Empty(new ClockFaceRepository(context).Load());
    }

    [Fact]
    public void ACorruptFile_IsReplacedByTheNextSave()
    {
        using var context = _fixture.CreateContext();
        context.ClockFaceLibraries.Add(new ClockFaceLibraryEntity { Json = "garbage" });
        context.SaveChanges();
        var repository = new ClockFaceRepository(context);

        repository.Save([Face("Fresh")]);

        Assert.Equal(new[] { "Fresh" }, new ClockFaceRepository(context).Load().Select(f => f.Name).ToArray());
    }

    public void Dispose() => _fixture.Dispose();
}
