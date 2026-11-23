using System;
using System.Globalization;
using Chisel.Core.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One task as the list shows it: the text, the done state, the due date, and an in-place edit. It only changes the
// properties that really changed, so a row that is not touched is never redrawn.
public sealed partial class TaskRowViewModel : ObservableObject
{
    private readonly TaskListViewModel _owner;
    private DateOnly _today;

    internal TaskRowViewModel(TaskListViewModel owner, TaskItem item, int order, DateOnly today)
    {
        _owner = owner;
        Item = item;
        Order = order;
        _today = today;
        Text = item.Text;
        IsDone = item.IsDone;
    }

    internal TaskItem Item { get; }

    // Where the task sits in the saved order (used to put a reopened task back in its place).
    internal int Order { get; set; }

    public Guid Id => Item.Id;

    [ObservableProperty]
    public partial string Text { get; private set; }

    [ObservableProperty]
    public partial bool IsDone { get; private set; }

    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty]
    public partial string EditText { get; set; } = string.Empty;

    public DateOnly? DueDate => Item.DueDate;

    public bool HasDue => Item.DueDate is not null;

    public bool IsOverdue => !IsDone && Item.DueDate is { } due && due < _today;

    public string DueText => Item.DueDate is not { } due
        ? string.Empty
        : (due.DayNumber - _today.DayNumber) switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            _ => due.ToString("ddd, MMM d", CultureInfo.CurrentCulture),
        };

    // The label a screen reader gets for the row.
    public string AutomationLabel => HasDue ? $"{Text}, due {DueText}" : Text;

    [RelayCommand]
    private void Toggle() => _owner.Toggle(this);

    [RelayCommand]
    private void Delete() => _owner.Delete(this);

    [RelayCommand]
    private void BeginEdit()
    {
        EditText = Text;
        IsEditing = true;
    }

    [RelayCommand]
    private void CommitEdit()
    {
        if (!IsEditing)
        {
            return;
        }
        IsEditing = false;
        var text = EditText.Trim();
        if (text.Length == 0 || text == Text)
        {
            return;
        }
        Item.Edit(text);
        Text = text;
        _owner.Save(Item);
        OnPropertyChanged(nameof(AutomationLabel));
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private void ClearDue() => _owner.SetDueDate(this, null);

    // After the item changed underneath (done state, due date, or a new day).
    internal void Sync(DateOnly today)
    {
        _today = today;
        IsDone = Item.IsDone;
        OnPropertyChanged(nameof(DueDate));
        OnPropertyChanged(nameof(HasDue));
        OnPropertyChanged(nameof(DueText));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(AutomationLabel));
    }
}
