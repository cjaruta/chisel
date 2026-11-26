using Chisel.Core.Tasks;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class TaskMapper
{
    public static TaskEntity ToEntity(this TaskItem task) => new()
    {
        Id = task.Id,
        Text = task.Text,
        IsDone = task.IsDone,
        DueDate = task.DueDate
    };

    public static TaskItem ToDomain(this TaskEntity entity) =>
        new(entity.Id, entity.Text, entity.IsDone, entity.DueDate);
}