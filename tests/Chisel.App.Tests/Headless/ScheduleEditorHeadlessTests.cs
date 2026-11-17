using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.Headless;

// "+ Block" / "+ Marker" open an editor inside the main window; saving shows the item on the grid.
public class ScheduleEditorHeadlessTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window window, string buttonName)
    {
        var button = Named<Button>(window, buttonName);
        if (button.Command is { } command)
        {
            command.Execute(button.CommandParameter);
        }
        else
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string textBoxName, string text)
    {
        Named<TextBox>(window, textBoxName).Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<string?> BlockTitles(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("blockTitle")).Select(t => t.Text);

    private static IEnumerable<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);

    private static ScheduleViewModel Schedule(MainWindow window) => ((MainViewModel)window.DataContext!).Schedule;

    [AvaloniaFact]
    public void Header_HasNowAddMarkerAndAddBlockButtons()
    {
        var window = UiSnapshot.ShowMainWindow();

        Assert.Equal("+ Block", Named<Button>(window, "AddBlockButton").Content);
        Assert.Equal("+ Marker", Named<Button>(window, "AddMarkerButton").Content);
        Assert.Equal("Now", Named<Button>(window, "NowButton").Content);
        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.False(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void AddBlock_OpensTheEditorInsideTheWindowWithTimesFilledIn()
    {
        var window = UiSnapshot.ShowMainWindow();

        Click(window, "AddBlockButton");

        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("10:00", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("11:00", Named<TextBox>(window, "BlockEndBox").Text);
        UiSnapshot.Render(window, "editor-block");
        window.Close();
    }

    [AvaloniaFact]
    public void AddBlock_SaveShowsTheNewBlockOnTheGridImmediately()
    {
        var window = UiSnapshot.ShowMainWindow();
        Click(window, "AddBlockButton");

        Type(window, "BlockTitleBox", "Lunch");
        Type(window, "BlockStartBox", "12:00");
        Type(window, "BlockEndBox", "12:45");
        Click(window, "BlockSaveButton");

        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Contains("Lunch", BlockTitles(window));
        window.Close();
    }

    [AvaloniaFact]
    public void AddBlock_InvalidInputShowsAnInlineMessage()
    {
        var window = UiSnapshot.ShowMainWindow();
        Click(window, "AddBlockButton");

        Type(window, "BlockTitleBox", "Lunch");
        Type(window, "BlockEndBox", "10:00");
        Click(window, "BlockSaveButton");

        var error = Named<TextBlock>(window, "BlockEditorError");
        Assert.True(error.IsVisible);
        Assert.Equal("End must be different from start. Overnight blocks are fine.", error.Text);
        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        UiSnapshot.Render(window, "editor-block-error");
        window.Close();
    }

    [AvaloniaFact]
    public void AddBlock_CancelClosesWithoutAddingAnything()
    {
        var window = UiSnapshot.ShowMainWindow();
        var before = BlockTitles(window).Count();
        Click(window, "AddBlockButton");
        Type(window, "BlockTitleBox", "Lunch");

        Click(window, "BlockCancelButton");

        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal(before, BlockTitles(window).Count());
        window.Close();
    }

    [AvaloniaFact]
    public void AddMarker_SaveShowsTheNewMarkerLineOnTheGrid()
    {
        var window = UiSnapshot.ShowMainWindow();
        Click(window, "AddMarkerButton");
        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        UiSnapshot.Render(window, "editor-marker");

        Type(window, "MarkerLabelBox", "Stretch");
        Type(window, "MarkerTimeBox", "4:00 PM");
        Named<ComboBox>(window, "MarkerKindBox").SelectedItem = MarkerKind.Checkpoint;
        Click(window, "MarkerSaveButton");

        Assert.False(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.Contains("16:00  Stretch", Texts(window));
        Assert.Contains(Schedule(window).GridMarkers, m => m.Label == "Stretch" && m.IsCheckpoint);
        window.Close();
    }

    [AvaloniaFact]
    public void AddMarker_InvalidInputShowsAnInlineMessage()
    {
        var window = UiSnapshot.ShowMainWindow();
        Click(window, "AddMarkerButton");

        Click(window, "MarkerSaveButton");

        var error = Named<TextBlock>(window, "MarkerEditorError");
        Assert.True(error.IsVisible);
        Assert.Equal("Enter a label.", error.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_ClosesTheEditor()
    {
        var window = UiSnapshot.ShowMainWindow();
        Click(window, "AddBlockButton");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TwelveHourMode_EditorTimesUseTheTwelveHourFormat()
    {
        var window = UiSnapshot.ShowMainWindow();
        ((MainViewModel)window.DataContext!).Settings.UseTwelveHour = true;

        Click(window, "AddBlockButton");

        Assert.Equal("10:00 AM", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("11:00 AM", Named<TextBox>(window, "BlockEndBox").Text);
        window.Close();
    }

    // --- Click to edit and delete ---

    // A real mouse click at the middle of the control, so hit testing is part of the test.
    private static void MouseClick(Window window, Control target)
    {
        var center = target.TranslatePoint(new Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Border GridBlock(Window window, string title) =>
        window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("gridBlock") && b.DataContext is ScheduleGridBlock block && block.Title == title);

    private static Border MarkerPill(Window window, string label) =>
        window.GetVisualDescendants().OfType<Border>()
            .Single(b => b.Classes.Contains("markerPill") && b.DataContext is ScheduleGridMarker marker && marker.Label == label);

    [AvaloniaFact]
    public void Header_NoLongerHasTheEditScheduleButton()
    {
        var window = UiSnapshot.ShowMainWindow();

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => Equals(b.Content, "Edit schedule"));
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingABlock_OpensTheEditorWithItsValuesAndADeleteButton()
    {
        var window = UiSnapshot.ShowMainWindow();

        MouseClick(window, GridBlock(window, "Workout"));

        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("Workout", Named<TextBox>(window, "BlockTitleBox").Text);
        Assert.Equal("14:00", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("15:00", Named<TextBox>(window, "BlockEndBox").Text);
        Assert.True(Named<Button>(window, "BlockDeleteButton").IsVisible);
        Assert.Contains("Edit block", Texts(window));
        UiSnapshot.Render(window, "editor-edit-block");
        window.Close();
    }

    [AvaloniaFact]
    public void NewBlockEditor_HasNoDeleteButton()
    {
        var window = UiSnapshot.ShowMainWindow();

        Click(window, "AddBlockButton");

        Assert.False(Named<Button>(window, "BlockDeleteButton").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingABlock_SaveChangesItOnTheGrid()
    {
        var window = UiSnapshot.ShowMainWindow();
        MouseClick(window, GridBlock(window, "Workout"));

        Type(window, "BlockTitleBox", "Run");
        Click(window, "BlockSaveButton");

        Assert.Contains("Run", BlockTitles(window));
        Assert.DoesNotContain("Workout", BlockTitles(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingABlock_DeleteRemovesItFromTheGrid()
    {
        var window = UiSnapshot.ShowMainWindow();
        MouseClick(window, GridBlock(window, "Workout"));

        Click(window, "BlockDeleteButton");

        Assert.False(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.DoesNotContain("Workout", BlockTitles(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingAMarker_OpensTheMarkerEditorAndDeleteRemovesIt()
    {
        var window = UiSnapshot.ShowMainWindow();

        MouseClick(window, MarkerPill(window, "Midday"));

        Assert.True(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.Equal("Midday", Named<TextBox>(window, "MarkerLabelBox").Text);
        Assert.Equal("12:00", Named<TextBox>(window, "MarkerTimeBox").Text);
        Assert.Equal(MarkerKind.Checkpoint, Named<ComboBox>(window, "MarkerKindBox").SelectedItem);
        Assert.Contains("Edit marker", Texts(window));
        UiSnapshot.Render(window, "editor-edit-marker");

        Click(window, "MarkerDeleteButton");

        Assert.False(Named<Border>(window, "MarkerEditorPanel").IsVisible);
        Assert.DoesNotContain(Schedule(window).GridMarkers, m => m.Label == "Midday");
        window.Close();
    }
}
