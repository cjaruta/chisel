using System.Globalization;
using Chisel.App.ViewModels;
using Chisel.Core.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class TaskListViewModelTests
{
    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> Items { get; } = new();

        public IReadOnlyList<TaskItem> GetAll() => Items.ToList();

        public void Add(TaskItem task) => Items.Add(task);

        public void Update(TaskItem task)
        {
            var index = Items.FindIndex(t => t.Id == task.Id);
            if (index >= 0)
            {
                Items[index] = task;
            }
        }

        public void Remove(Guid id) => Items.RemoveAll(t => t.Id == id);

        public void Reorder(IReadOnlyList<Guid> orderedIds)
        {
            var order = orderedIds.ToList();
            Items.Sort((x, y) => order.IndexOf(x.Id).CompareTo(order.IndexOf(y.Id)));
        }
    }

    // 2026-11-20 at 09:00 local.
    private static FakeTimeProvider Today()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(-5)).ToUniversalTime());
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-5), "test", "test"));
        return time;
    }

    private static (TaskListViewModel Vm, FakeTaskRepository Repo) Make(params TaskItem[] items)
    {
        var repo = new FakeTaskRepository();
        repo.Items.AddRange(items);
        return (new TaskListViewModel(repo, Today()), repo);
    }

    private static string[] Texts(IEnumerable<TaskRowViewModel> rows) => rows.Select(r => r.Text).ToArray();

    [Fact]
    public void Constructor_SplitsSavedTasksIntoOpenAndDone_KeepingTheirOrder()
    {
        var (vm, _) = Make(new TaskItem("A"), new TaskItem(Guid.NewGuid(), "B", true), new TaskItem("C"));

        Assert.Equal(new[] { "A", "C" }, Texts(vm.Open));
        Assert.Equal(new[] { "B" }, Texts(vm.Done));
        Assert.False(vm.IsEmpty);
        Assert.True(vm.HasDone);
        Assert.Equal("Completed (1)", vm.DoneHeader);
    }

    [Fact]
    public void EmptyList_IsEmpty_UntilATaskIsAdded()
    {
        var (vm, _) = Make();
        Assert.True(vm.IsEmpty);

        vm.NewTaskText = "First";
        vm.AddTaskCommand.Execute(null);

        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void AddTask_TrimsSavesAppendsAndClearsTheInput()
    {
        var (vm, repo) = Make(new TaskItem("A"));

        vm.NewTaskText = "  Buy milk  ";
        vm.AddTaskCommand.Execute(null);

        Assert.Equal(new[] { "A", "Buy milk" }, Texts(vm.Open));
        Assert.Equal(new[] { "A", "Buy milk" }, repo.Items.Select(t => t.Text).ToArray());
        Assert.Equal(string.Empty, vm.NewTaskText);
    }

    [Fact]
    public void AddTask_WithBlankText_DoesNothing()
    {
        var (vm, repo) = Make();

        vm.NewTaskText = "   ";
        vm.AddTaskCommand.Execute(null);

        Assert.Empty(repo.Items);
        Assert.Empty(vm.Open);
    }

    [Fact]
    public void Toggle_MovesTheTaskToDone_SavesIt_AndDoesNotRebuildTheOtherRows()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"), new TaskItem("C"));
        var a = vm.Open[0];
        var c = vm.Open[2];

        vm.Open[1].ToggleCommand.Execute(null);

        Assert.Equal(new[] { "A", "C" }, Texts(vm.Open));
        Assert.Equal(new[] { "B" }, Texts(vm.Done));
        Assert.True(vm.Done[0].IsDone);
        Assert.True(repo.Items.Single(t => t.Text == "B").IsDone);
        Assert.Same(a, vm.Open[0]);
        Assert.Same(c, vm.Open[1]);
    }

    [Fact]
    public void Reopening_PutsTheTaskBackWhereItWas()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"), new TaskItem("C"));
        vm.Open[1].ToggleCommand.Execute(null);

        vm.Done[0].ToggleCommand.Execute(null);

        Assert.Equal(new[] { "A", "B", "C" }, Texts(vm.Open));
        Assert.Empty(vm.Done);
        Assert.False(repo.Items.Single(t => t.Text == "B").IsDone);
    }

    [Fact]
    public void Edit_Commit_SavesTheNewText()
    {
        var (vm, repo) = Make(new TaskItem("Buy milk"));
        var row = vm.Open[0];

        row.BeginEditCommand.Execute(null);
        Assert.True(row.IsEditing);
        Assert.Equal("Buy milk", row.EditText);

        row.EditText = "  Buy oat milk ";
        row.CommitEditCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.Equal("Buy oat milk", row.Text);
        Assert.Equal("Buy oat milk", repo.Items[0].Text);
    }

    [Fact]
    public void Edit_CancelOrBlank_KeepsTheOldText()
    {
        var (vm, repo) = Make(new TaskItem("Buy milk"));
        var row = vm.Open[0];

        row.BeginEditCommand.Execute(null);
        row.EditText = "Something else";
        row.CancelEditCommand.Execute(null);
        Assert.Equal("Buy milk", row.Text);
        Assert.False(row.IsEditing);

        row.BeginEditCommand.Execute(null);
        row.EditText = "   ";
        row.CommitEditCommand.Execute(null);
        Assert.Equal("Buy milk", row.Text);
        Assert.Equal("Buy milk", repo.Items[0].Text);
    }

    [Fact]
    public void Delete_RemovesTheRowAndTheSavedTask()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"));

        vm.Open[0].DeleteCommand.Execute(null);

        Assert.Equal(new[] { "B" }, Texts(vm.Open));
        Assert.Equal(new[] { "B" }, repo.Items.Select(t => t.Text).ToArray());
    }

    [Fact]
    public void ClearCompleted_RemovesOnlyDoneTasks()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem(Guid.NewGuid(), "B", true), new TaskItem(Guid.NewGuid(), "C", true));

        vm.ClearCompletedCommand.Execute(null);

        Assert.Empty(vm.Done);
        Assert.False(vm.HasDone);
        Assert.Equal(new[] { "A" }, repo.Items.Select(t => t.Text).ToArray());
    }

    [Fact]
    public void MoveBy_ReordersAndSavesTheOrder_AndStopsAtTheEnds()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"), new TaskItem("C"));

        vm.MoveBy(vm.Open[2], -1);
        Assert.Equal(new[] { "A", "C", "B" }, Texts(vm.Open));
        Assert.Equal(new[] { "A", "C", "B" }, repo.Items.Select(t => t.Text).ToArray());

        vm.MoveBy(vm.Open[0], -1);
        vm.MoveBy(vm.Open[2], 1);
        Assert.Equal(new[] { "A", "C", "B" }, Texts(vm.Open));
    }

    [Fact]
    public void MoveTo_PutsTheRowAtTheGivenPlace()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"), new TaskItem("C"));

        vm.MoveTo(vm.Open[0], 2);

        Assert.Equal(new[] { "B", "C", "A" }, Texts(vm.Open));
        Assert.Equal(new[] { "B", "C", "A" }, repo.Items.Select(t => t.Text).ToArray());
    }

    [Fact]
    public void ReorderedTasks_KeepTheirOrderAfterAReload()
    {
        var (vm, repo) = Make(new TaskItem("A"), new TaskItem("B"), new TaskItem("C"));
        vm.MoveTo(vm.Open[2], 0);

        var reloaded = new TaskListViewModel(repo, Today());

        Assert.Equal(new[] { "C", "A", "B" }, Texts(reloaded.Open));
    }

    [Fact]
    public void DueDate_IsSaved_AndReadsAsTodayTomorrowOrADate()
    {
        var (vm, repo) = Make(new TaskItem("A"));
        var row = vm.Open[0];
        Assert.False(row.HasDue);

        vm.SetDueDate(row, new DateOnly(2026, 11, 20));
        Assert.Equal("Today", row.DueText);
        Assert.False(row.IsOverdue);
        Assert.Equal(new DateOnly(2026, 11, 20), repo.Items[0].DueDate);

        vm.SetDueDate(row, new DateOnly(2026, 11, 21));
        Assert.Equal("Tomorrow", row.DueText);

        var later = new DateOnly(2026, 12, 3);
        vm.SetDueDate(row, later);
        Assert.Equal(later.ToString("ddd, MMM d", CultureInfo.CurrentCulture), row.DueText);

        vm.SetDueDate(row, null);
        Assert.False(row.HasDue);
        Assert.Null(repo.Items[0].DueDate);
    }

    [Fact]
    public void PastDue_IsOverdue_UnlessTheTaskIsDone()
    {
        var (vm, _) = Make(new TaskItem("A"));
        var row = vm.Open[0];

        vm.SetDueDate(row, new DateOnly(2026, 11, 19));
        Assert.True(row.IsOverdue);
        Assert.Equal("Yesterday", row.DueText);

        row.ToggleCommand.Execute(null);
        Assert.False(row.IsOverdue);
    }

    [Fact]
    public void LoadedDueDates_ShowOnTheirRows()
    {
        var (vm, _) = Make(new TaskItem(Guid.NewGuid(), "A", false, new DateOnly(2026, 11, 21)));

        Assert.Equal("Tomorrow", vm.Open[0].DueText);
    }

    [Fact]
    public void DoneSection_StartsCollapsed_AndTogglesOpen()
    {
        var (vm, _) = Make(new TaskItem(Guid.NewGuid(), "A", true));
        Assert.False(vm.IsDoneExpanded);

        vm.ToggleDoneExpandedCommand.Execute(null);

        Assert.True(vm.IsDoneExpanded);
    }
}
