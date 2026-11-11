using Chisel.App.ViewModels;
using Chisel.Core.Tasks;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class TaskListViewModelTests
{
    private sealed class FakeTaskRepository : ITaskRepository
    {
        private readonly List<TaskItem> _items = new();

        public IReadOnlyList<TaskItem> GetAll() => _items;

        public void Add(TaskItem task) => _items.Add(task);

        public void Update(TaskItem task)
        {
            var index = _items.FindIndex(t => t.Id == task.Id);
            if (index >= 0)
            {
                _items[index] = task;
            }
        }

        public void Remove(Guid id) => _items.RemoveAll(t => t.Id == id);
    }

    [Fact]
    public void Constructor_LoadsExistingTasks()
    {
        var repository = new FakeTaskRepository();
        repository.Add(new TaskItem("Buy milk"));

        var viewModel = new TaskListViewModel(repository);

        Assert.Equal(new[] { "Buy milk" }, viewModel.Tasks.Select(t => t.Text).ToArray());
    }

    [Fact]
    public void AddTask_WithText_AddsToRepositoryAndClearsInput()
    {
        var repository = new FakeTaskRepository();
        var viewModel = new TaskListViewModel(repository) { NewTaskText = "Buy milk" };

        viewModel.AddTaskCommand.Execute(null);

        Assert.Equal(new[] { "Buy milk" }, repository.GetAll().Select(t => t.Text).ToArray());
        Assert.Equal(new[] { "Buy milk" }, viewModel.Tasks.Select(t => t.Text).ToArray());
        Assert.Equal(string.Empty, viewModel.NewTaskText);
    }

    [Fact]
    public void AddTask_WithBlankText_DoesNothing()
    {
        var repository = new FakeTaskRepository();
        var viewModel = new TaskListViewModel(repository) { NewTaskText = "   " };

        viewModel.AddTaskCommand.Execute(null);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void ToggleComplete_OnOpenTask_MarksItDoneInRepository()
    {
        var repository = new FakeTaskRepository();
        var task = new TaskItem("Buy milk");
        repository.Add(task);
        var viewModel = new TaskListViewModel(repository);

        viewModel.ToggleCompleteCommand.Execute(viewModel.Tasks[0]);

        Assert.True(repository.GetAll()[0].IsDone);
    }

    [Fact]
    public void RemoveTask_ExistingTask_RemovesFromRepository()
    {
        var repository = new FakeTaskRepository();
        var task = new TaskItem("Buy milk");
        repository.Add(task);
        var viewModel = new TaskListViewModel(repository);

        viewModel.RemoveTaskCommand.Execute(task);

        Assert.Empty(repository.GetAll());
        Assert.Empty(viewModel.Tasks);
    }
}
