using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Chisel.Core.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The task list: open tasks in their saved order, then a collapsible Completed section. Every change is saved at once,
// and only the rows that changed are touched (nothing is rebuilt), so typing, focus and scroll position stay put.
public partial class TaskListViewModel : ViewModelBase
{
    private readonly ITaskRepository _repository;
    private readonly TimeProvider _time;
    private int _nextOrder;

    public ObservableCollection<TaskRowViewModel> Open { get; } = new();

    public ObservableCollection<TaskRowViewModel> Done { get; } = new();

    [ObservableProperty]
    public partial string NewTaskText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDoneExpanded { get; set; }

    public bool IsEmpty => Open.Count == 0 && Done.Count == 0;

    public bool HasDone => Done.Count > 0;

    public string DoneHeader => $"Completed ({Done.Count})";

    public TaskListViewModel(ITaskRepository repository, TimeProvider? time = null)
    {
        _repository = repository;
        _time = time ?? TimeProvider.System;
        var today = Today();
        foreach (var task in _repository.GetAll())
        {
            var row = new TaskRowViewModel(this, task, _nextOrder++, today);
            (task.IsDone ? Done : Open).Add(row);
        }
        Open.CollectionChanged += OnSectionsChanged;
        Done.CollectionChanged += OnSectionsChanged;
    }

    public DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasDone));
        OnPropertyChanged(nameof(DoneHeader));
    }

    [RelayCommand]
    private void AddTask()
    {
        var text = NewTaskText.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var task = new TaskItem(text);
        _repository.Add(task);
        Open.Add(new TaskRowViewModel(this, task, _nextOrder++, Today()));
        NewTaskText = string.Empty;
    }

    [RelayCommand]
    private void ToggleDoneExpanded() => IsDoneExpanded = !IsDoneExpanded;

    [RelayCommand]
    private void ClearCompleted()
    {
        foreach (var row in Done)
        {
            _repository.Remove(row.Id);
        }
        Done.Clear();
    }

    internal void Toggle(TaskRowViewModel row)
    {
        if (row.Item.IsDone)
        {
            row.Item.Reopen();
        }
        else
        {
            row.Item.Complete();
        }
        _repository.Update(row.Item);
        row.Sync(Today());

        (row.IsDone ? Open : Done).Remove(row);
        InsertByOrder(row.IsDone ? Done : Open, row);
    }

    internal void Delete(TaskRowViewModel row)
    {
        _repository.Remove(row.Id);
        Open.Remove(row);
        Done.Remove(row);
    }

    internal void Save(TaskItem task) => _repository.Update(task);

    public void SetDueDate(TaskRowViewModel row, DateOnly? date)
    {
        row.Item.SetDueDate(date);
        _repository.Update(row.Item);
        row.Sync(Today());
    }

    // Moves an open task up (-1) or down (+1) one place; stops at the ends.
    public void MoveBy(TaskRowViewModel row, int delta)
    {
        var index = Open.IndexOf(row);
        if (index < 0)
        {
            return;
        }
        MoveTo(row, index + delta);
    }

    // Puts an open task at the given place in the open list (clamped) and saves the order.
    public void MoveTo(TaskRowViewModel row, int newIndex)
    {
        var index = Open.IndexOf(row);
        newIndex = Math.Clamp(newIndex, 0, Open.Count - 1);
        if (index < 0 || index == newIndex)
        {
            return;
        }

        Open.Move(index, newIndex);
        var order = 0;
        foreach (var r in Open.Concat(Done))
        {
            r.Order = order++;
        }
        _nextOrder = order;
        _repository.Reorder(Open.Concat(Done).Select(r => r.Id).ToList());
    }

    private static void InsertByOrder(ObservableCollection<TaskRowViewModel> list, TaskRowViewModel row)
    {
        var i = 0;
        while (i < list.Count && list[i].Order < row.Order)
        {
            i++;
        }
        list.Insert(i, row);
    }
}
