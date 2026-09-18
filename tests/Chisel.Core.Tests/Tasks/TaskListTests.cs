using Chisel.Core.Tasks;
using Xunit;

namespace Chisel.Core.Tests.Tasks;

public class TaskListTests
{
    [Fact]
    public void Add_NewTask_AppearsInItems()
    {
        // Arrange
        var list = new TaskList();

        // Act
        list.Add("Buy groceries");

        // Assert
        Assert.Single(list.Items);
        Assert.Equal("Buy groceries", list.Items[0].Text);
    }

    [Fact]
    public void Reorder_MoveLastTaskToFirst_ChangesItemsOrder()
    {
        // Arrange
        var list = new TaskList();
        list.Add("First");
        list.Add("Second");
        var second = list.Items[1];

        // Act
        list.Reorder(second.Id, 0);

        // Assert
        Assert.Equal("Second", list.Items[0].Text);
        Assert.Equal("First", list.Items[1].Text);
    }

    [Fact]
    public void ClearCompleted_MixOfDoneAndNotDone_RemovesOnlyDoneTasks()
    {
        // Arrange
        var list = new TaskList();
        list.Add("Done task");
        list.Add("Still open");
        list.Items[0].Complete();

        // Act
        list.ClearCompleted();

        // Assert
        Assert.Single(list.Items);
        Assert.Equal("Still open", list.Items[0].Text);
    }
}