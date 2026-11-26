namespace Chisel.Core.Tasks;

public interface ITaskRepository
{
    IReadOnlyList<TaskItem> GetAll();
    void Add(TaskItem task);
    void Update(TaskItem task);
    void Remove(Guid id);

    // Saves the order of the tasks: the first id comes first. Ids not listed keep their place after the listed ones.
    void Reorder(IReadOnlyList<Guid> orderedIds);
}