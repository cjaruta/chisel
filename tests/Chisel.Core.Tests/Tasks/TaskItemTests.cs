using Chisel.Core.Tasks;
using Xunit;

namespace Chisel.Core.Tests.Tasks;

public class TaskItemTests
{
    [Fact]
    public void Constructor_NewTask_StartsNotDone()
    {
        // Arrange
        var task = new TaskItem("Buy groceries");

        // Act
        var isDone = task.IsDone;

        // Assert
        Assert.False(isDone);
    }

    [Fact]
    public void Complete_NotDoneTask_MarksItDone()
    {
        // Arrange
        var task = new TaskItem("Buy groceries");

        // Act
        task.Complete();

        // Assert
        Assert.True(task.IsDone);
    }

    [Fact]
    public void Reopen_DoneTask_MarksItNotDone()
    {
        // Arrange
        var task = new TaskItem("Buy groceries");
        task.Complete();

        // Act
        task.Reopen();

        // Assert
        Assert.False(task.IsDone);
    }

    [Fact]
    public void Edit_NewText_UpdatesText()
    {
        // Arrange
        var task = new TaskItem("Buy groceries");

        // Act
        task.Edit("Buy groceries and milk");

        // Assert
        Assert.Equal("Buy groceries and milk", task.Text);
    }
}