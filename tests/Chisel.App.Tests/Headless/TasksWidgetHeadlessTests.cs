using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// The rebuilt Tasks widget: a pinned add box, rows with an in-place editor, a collapsed Completed section, an empty
// state, and keyboard reorder. Drawn and driven in the real main window.
public class TasksWidgetHeadlessTests
{
    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static TasksWidgetView View(Window window) => window.GetVisualDescendants().OfType<TasksWidgetView>().Single();

    private static T Named<T>(Window window, string name) where T : Control =>
        View(window).GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<Border> Rows(Window window) =>
        View(window).GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("taskRow") && b.IsEffectivelyVisible).ToList();

    private static MainWindow Show(double width = 1200, double height = 760)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void TheAddBox_IsPinnedAboveTheList_AndKeepsFocusForRapidEntry()
    {
        var window = Show();
        var box = Named<TextBox>(window, "NewTaskBox");
        var firstRow = Rows(window)[0];
        Assert.True(box.TranslatePoint(new Point(0, 0), window)!.Value.Y < firstRow.TranslatePoint(new Point(0, 0), window)!.Value.Y);

        box.Focus();
        box.Text = "Plan the week";
        Main(window).Tasks.AddTaskCommand.Execute(null);
        box.Text = "Book the dentist";
        Main(window).Tasks.AddTaskCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(box.IsFocused);
        Assert.Equal("", box.Text);
        var texts = Main(window).Tasks.Open.Select(r => r.Text).ToArray();
        Assert.Equal(new[] { "Plan the week", "Book the dentist" }, texts[^2..]);
        window.Close();
    }

    [AvaloniaFact]
    public void Rows_HaveACheckbox_AnAutomationName_AndTheCompletedOnesAreCollapsed()
    {
        var window = Show();
        var tasks = Main(window).Tasks;

        Assert.Equal(tasks.Open.Count, Rows(window).Count);
        Assert.Equal(tasks.Open[0].Text, AutomationProperties.GetName(Rows(window)[0]));
        Assert.NotEmpty(Rows(window)[0].GetVisualDescendants().OfType<CheckBox>());

        Assert.True(tasks.HasDone);
        Assert.False(Named<ItemsControl>(window, "DoneItems").IsVisible);
        Assert.Equal("Completed (1)", tasks.DoneHeader);

        Named<Button>(window, "DoneToggle").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<ItemsControl>(window, "DoneItems").IsVisible);

        Named<Button>(window, "ClearDoneButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<Grid>(window, "DoneBar").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void WithNoTasks_ShowsAFriendlyMessage()
    {
        var window = Show();
        var tasks = Main(window).Tasks;
        Assert.False(Named<TextBlock>(window, "EmptyText").IsVisible);

        foreach (var row in tasks.Open.Concat(tasks.Done).ToList())
        {
            row.DeleteCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<TextBlock>(window, "EmptyText").IsVisible);
        Assert.Contains("Add your first task", Named<TextBlock>(window, "EmptyText").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void F2_EditsTheRowInPlace_AndEnterSavesIt()
    {
        var window = Show();
        var row = Main(window).Tasks.Open[0];
        var border = Rows(window)[0];
        border.Focus();

        window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.IsEditing);

        row.EditText = "Write the monthly plan";
        row.CommitEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(row.IsEditing);
        Assert.Equal("Write the monthly plan", AutomationProperties.GetName(Rows(window)[0]));
        window.Close();
    }

    [AvaloniaFact]
    public void AltDown_MovesTheFocusedRowDownOne()
    {
        var window = Show();
        var tasks = Main(window).Tasks;
        var first = tasks.Open[0].Text;
        var second = tasks.Open[1].Text;
        Rows(window)[0].Focus();

        window.KeyPress(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(second, tasks.Open[0].Text);
        Assert.Equal(first, tasks.Open[1].Text);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760, true)]
    [InlineData(640, 560, true)]
    [InlineData(1200, 760, false)]
    public void Snapshot_Tasks(int width, int height, bool headerShown)
    {
        var window = Show(width, height);
        var tasks = Main(window).Tasks;
        tasks.SetDueDate(tasks.Open[0], tasks.Today().AddDays(-1));
        tasks.SetDueDate(tasks.Open[1], tasks.Today());
        tasks.IsDoneExpanded = true;
        if (!headerShown)
        {
            Main(window).Layout.EnterEditCommand.Execute(null);
            Main(window).Layout.ToggleHeader("tasks");
            Main(window).Layout.DoneCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();

        UiSnapshot.Render(window, $"tasks-{width}x{height}-{(headerShown ? "header" : "noheader")}");
        window.Close();
    }
}
