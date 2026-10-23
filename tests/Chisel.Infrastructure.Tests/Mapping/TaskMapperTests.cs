using Chisel.Core.Tasks;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class TaskMapperTests
{
    [Fact]
    public void ToEntity_CopiesIdTextAndDoneFlag()
    {
        var task = new TaskItem(Guid.NewGuid(), "Buy milk", true);

        var entity = task.ToEntity();

        Assert.Equal(task.Id, entity.Id);
        Assert.Equal("Buy milk", entity.Text);
        Assert.True(entity.IsDone);
    }

    [Fact]
    public void ToDomain_CopiesIdTextAndDoneFlag()
    {
        var entity = new TaskEntity { Id = Guid.NewGuid(), Text = "Buy milk", IsDone = true };

        var task = entity.ToDomain();

        Assert.Equal(entity.Id, task.Id);
        Assert.Equal("Buy milk", task.Text);
        Assert.True(task.IsDone);
    }
}