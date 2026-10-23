using Chisel.Core.Tasks;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class TaskRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void GetAll_AfterAddingTasks_ReturnsThemInTheOrderAdded()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);

        repository.Add(new TaskItem("First"));
        repository.Add(new TaskItem("Second"));
        repository.Add(new TaskItem("Third"));

        using var readContext = _fixture.CreateContext();
        var texts = new TaskRepository(readContext).GetAll().Select(t => t.Text).ToList();

        Assert.Equal(new[] { "First", "Second", "Third" }, texts);
    }

    public void Dispose() => _fixture.Dispose();
}