using Chisel.Core.Schedule;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ScheduleRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Get_OnEmptyDatabase_ReturnsScheduleWithNoBlocks()
    {
        using var context = _fixture.CreateContext();
        var repository = new ScheduleRepository(context);

        Assert.Empty(repository.Get().Blocks);
    }

    [Fact]
    public void Save_WithBlocks_PersistsThemAll()
    {
        using var context = _fixture.CreateContext();
        var repository = new ScheduleRepository(context);

        repository.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0))
        }));

        using var readContext = _fixture.CreateContext();
        var titles = new ScheduleRepository(readContext).Get().Blocks.Select(b => b.Title).ToList();

        Assert.Equal(new[] { "Focus", "Workout" }, titles);
    }

    [Fact]
    public void Save_CalledAgain_DiscardsThePreviousSet()
    {
        using var context = _fixture.CreateContext();
        var repository = new ScheduleRepository(context);
        repository.Save(new DaySchedule(new[] { new ScheduleBlock("Old", new TimeOnly(9, 0), new TimeOnly(10, 0)) }));

        repository.Save(new DaySchedule(new[] { new ScheduleBlock("New", new TimeOnly(11, 0), new TimeOnly(12, 0)) }));

        using var readContext = _fixture.CreateContext();
        var block = Assert.Single(new ScheduleRepository(readContext).Get().Blocks);
        Assert.Equal("New", block.Title);
    }

    public void Dispose() => _fixture.Dispose();
}
