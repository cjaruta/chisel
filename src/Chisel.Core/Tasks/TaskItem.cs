namespace Chisel.Core.Tasks;

public class TaskItem
{
    public TaskItem(string text)
    {
        Id = Guid.NewGuid();
        Text = text;
        IsDone = false;
    }

    public Guid Id { get; }
    public string Text { get; private set; }
    public bool IsDone { get; private set; }
    public DateOnly? DueDate { get; private set; }

    public void Complete() => IsDone = true;
    public void Reopen() => IsDone = false;
    public void Edit(string newText) => Text = newText;
    public void SetDueDate(DateOnly? date) => DueDate = date;

    public TaskItem(Guid id, string text, bool isDone, DateOnly? dueDate = null)
    {
        Id = id;
        Text = text;
        IsDone = isDone;
        DueDate = dueDate;
    }
}