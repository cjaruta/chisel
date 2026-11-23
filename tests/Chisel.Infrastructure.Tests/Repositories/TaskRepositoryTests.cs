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

    [Fact]
    public void Update_ChangedTask_IsSavedToTheDatabase()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);
        var task = new TaskItem("Buy milk");
        repository.Add(task);

        task.Complete();
        task.Edit("Buy oat milk");
        repository.Update(task);

        using var readContext = _fixture.CreateContext();
        var saved = Assert.Single(new TaskRepository(readContext).GetAll());
        Assert.Equal(task.Id, saved.Id);
        Assert.Equal("Buy oat milk", saved.Text);
        Assert.True(saved.IsDone);
    }

    [Fact]
    public void Update_UnknownTask_ThrowsInvalidOperationException()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);

        void Act() => repository.Update(new TaskItem("Ghost"));

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void Remove_ExistingTask_DeletesItFromTheDatabase()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);
        var keep = new TaskItem("Keep");
        var drop = new TaskItem("Drop");
        repository.Add(keep);
        repository.Add(drop);

        repository.Remove(drop.Id);

        using var readContext = _fixture.CreateContext();
        var remaining = Assert.Single(new TaskRepository(readContext).GetAll());
        Assert.Equal(keep.Id, remaining.Id);
    }

    [Fact]
    public void Remove_UnknownId_DoesNothing()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);
        repository.Add(new TaskItem("Keep"));

        repository.Remove(Guid.NewGuid());

        Assert.Single(repository.GetAll());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void DueDate_IsSavedAndReadBack()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);
        var task = new TaskItem("Pay rent");
        repository.Add(task);
        task.SetDueDate(new DateOnly(2000, 1, 2));
        repository.Update(task);

        using var readContext = _fixture.CreateContext();
        Assert.Equal(new DateOnly(2000, 1, 2), Assert.Single(new TaskRepository(readContext).GetAll()).DueDate);
    }

    [Fact]
    public void Reorder_SavesTheNewOrder_AndNewTasksGoAfterIt()
    {
        using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);
        var a = new TaskItem("A");
        var b = new TaskItem("B");
        var c = new TaskItem("C");
        repository.Add(a);
        repository.Add(b);
        repository.Add(c);

        repository.Reorder(new[] { c.Id, a.Id, b.Id });
        repository.Add(new TaskItem("D"));

        using var readContext = _fixture.CreateContext();
        Assert.Equal(new[] { "C", "A", "B", "D" }, new TaskRepository(readContext).GetAll().Select(t => t.Text).ToArray());
    }
}
