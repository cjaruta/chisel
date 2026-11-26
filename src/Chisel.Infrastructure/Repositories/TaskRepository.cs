using Chisel.Core.Tasks;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly ChiselDbContext _context;

    public TaskRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<TaskItem> GetAll() =>
        _context.Tasks
            .OrderBy(t => t.Position)
            .ToList()
            .Select(t => t.ToDomain())
            .ToList();

    public void Add(TaskItem task)
    {
        var entity = task.ToEntity();
        entity.Position = (_context.Tasks.Max(t => (int?)t.Position) ?? -1) + 1;

        _context.Tasks.Add(entity);
        _context.SaveChanges();
    }

    public void Update(TaskItem task)
    {
        var entity = _context.Tasks.Find(task.Id)
            ?? throw new InvalidOperationException($"Task {task.Id} does not exist.");

        entity.Text = task.Text;
        entity.IsDone = task.IsDone;
        entity.DueDate = task.DueDate;
        _context.SaveChanges();
    }

    public void Reorder(IReadOnlyList<Guid> orderedIds)
    {
        var entities = _context.Tasks.ToDictionary(t => t.Id);
        var position = 0;
        foreach (var id in orderedIds)
        {
            if (entities.Remove(id, out var entity))
            {
                entity.Position = position++;
            }
        }
        // Anything not listed keeps its relative order after the listed ones.
        foreach (var entity in entities.Values.OrderBy(t => t.Position))
        {
            entity.Position = position++;
        }
        _context.SaveChanges();
    }

    public void Remove(Guid id)
    {
        var entity = _context.Tasks.Find(id);
        if (entity is null)
        {
            return;
        }

        _context.Tasks.Remove(entity);
        _context.SaveChanges();
    }
}