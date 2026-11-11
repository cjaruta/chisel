using System.Collections.ObjectModel;
using Chisel.Core.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class TaskListViewModel : ViewModelBase
{
    private readonly ITaskRepository _repository;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    [ObservableProperty]
    public partial string NewTaskText { get; set; } = string.Empty;

    public TaskListViewModel(ITaskRepository repository)
    {
        _repository = repository;
        Refresh();
    }

    public void Refresh()
    {
        Tasks.Clear();
        foreach (var task in _repository.GetAll())
        {
            Tasks.Add(task);
        }
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskText))
        {
            return;
        }

        _repository.Add(new TaskItem(NewTaskText));
        NewTaskText = string.Empty;
        Refresh();
    }

    [RelayCommand]
    private void ToggleComplete(TaskItem task)
    {
        if (task.IsDone)
        {
            task.Reopen();
        }
        else
        {
            task.Complete();
        }

        _repository.Update(task);
        Refresh();
    }

    [RelayCommand]
    private void RemoveTask(TaskItem task)
    {
        _repository.Remove(task.Id);
        Refresh();
    }
}
