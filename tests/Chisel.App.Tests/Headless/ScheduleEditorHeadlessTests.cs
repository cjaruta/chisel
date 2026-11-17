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
}
