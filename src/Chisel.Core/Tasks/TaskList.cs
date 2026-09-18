namespace Chisel.Core.Tasks;

public class TaskList
{
    private readonly List<TaskItem> _items = new();

    public IReadOnlyList<TaskItem> Items => _items;

    public void Add(string text)
    {
        _items.Add(new TaskItem(text));
    }

    public void Reorder(Guid taskId, int newIndex)
    {
        var task = _items.First(t => t.Id == taskId);
        _items.Remove(task);
        _items.Insert(newIndex, task);
    }

    public void ClearCompleted()
    {
        _items.RemoveAll(t => t.IsDone);
    }
}