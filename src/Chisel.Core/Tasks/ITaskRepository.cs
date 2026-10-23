namespace Chisel.Core.Tasks;

public interface ITaskRepository
{
    IReadOnlyList<TaskItem> GetAll();
    void Add(TaskItem task);
    void Update(TaskItem task);
    void Remove(Guid id);
}