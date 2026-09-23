using Chisel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class ChiselDbContextTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Tasks_AddAndSave_CanBeReadBack()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var task = new TaskEntity { Id = Guid.NewGuid(), Text = "Buy groceries", IsDone = false };

        // Act
        context.Tasks.Add(task);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var savedTask = readContext.Tasks.First(t => t.Id == task.Id);

        // Assert
        Assert.Equal("Buy groceries", savedTask.Text);
        Assert.False(savedTask.IsDone);
    }

    public void Dispose() => _fixture.Dispose();
}