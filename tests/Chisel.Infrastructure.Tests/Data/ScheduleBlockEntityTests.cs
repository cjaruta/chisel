using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class ScheduleBlockEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void ScheduleBlocks_AddAndSave_CanBeReadBack()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var block = new ScheduleBlockEntity
        {
            Id = Guid.NewGuid(),
            Title = "Focused work",
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(11, 0)
        };

        // Act
        context.ScheduleBlocks.Add(block);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var saved = readContext.ScheduleBlocks.First(b => b.Id == block.Id);

        // Assert
        Assert.Equal("Focused work", saved.Title);
        Assert.Equal(new TimeOnly(8, 0), saved.Start);
        Assert.Equal(new TimeOnly(11, 0), saved.End);
    }

    public void Dispose() => _fixture.Dispose();
}