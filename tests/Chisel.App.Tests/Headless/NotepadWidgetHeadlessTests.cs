using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Notes;

namespace Chisel.App.Tests.Headless;

// The Notes widget as a notepad: one full-size plain-text editor, a pad switcher that stays in the card whether or not its
// title bar is shown, and the file actions behind the More menu.
public class NotepadWidgetHeadlessTests
{
    private sealed class FakeFiles : INotepadFiles
    {
        public NotepadFile? ToOpen { get; set; }
        public List<(string Name, string Text)> Saved { get; } = new();
        public Task<NotepadFile?> OpenAsync() => Task.FromResult(ToOpen);

        public Task<bool> SaveAsync(string suggestedName, string text)
        {
            Saved.Add((suggestedName, text));
            return Task.FromResult(true);
        }
    }

    private const string Sample = "Shopping\n\n  - milk\n  - bread\n\nCall the dentist ✓";

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static WidgetFrame Frame(Window window) =>
        window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).Single(f => f.WidgetId == "notes");

    private static NotesWidgetView View(Window window) => window.GetVisualDescendants().OfType<NotesWidgetView>().Single();

    private static T Named<T>(Window window, string name) where T : Control =>
        View(window).GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static MainWindow Show(double width = 1200, double height = 760, INotepadFiles? files = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, notepadFiles: files);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void SetHeader(MainWindow window, bool shown)
    {
        var layout = Main(window).Layout;
        layout.EnterEditCommand.Execute(null);
        if (Frame(window).ShowHeader != shown)
        {
            layout.ToggleHeader("notes");
        }
        layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheOldStickyNoteForm_IsGone_AndOneEditorFillsTheCard()
    {
        var window = Show();
        var editor = Named<TextBox>(window, "PadEditor");

        Assert.Empty(View(window).GetVisualDescendants().OfType<Button>().Where(b => b.Content as string is "Save"));
        Assert.True(editor.AcceptsReturn);
        var frame = InWindow(Frame(window), window);
        var box = InWindow(editor, window);
        Assert.True(box.Width > frame.Width * 0.8, "the editor spans the card");
        Assert.True(box.Bottom > frame.Bottom - 24, "the editor reaches the bottom of the card");
        window.Close();
    }

    [AvaloniaFact]
    public void Typing_OutsideEditMode_WorksAndIsSavedByAutosave()
    {
        var window = Show();
        var editor = Named<TextBox>(window, "PadEditor");
        var startBody = Main(window).Notes.Text;

        editor.Focus();
        editor.SelectAll();
        window.KeyTextInput("Plain words");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Plain words", editor.Text);
        Assert.Equal("Plain words", Main(window).Notes.Text);
        Assert.NotEqual(startBody, Main(window).Notes.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void SelectingAnotherPad_ChangesTheEditorText_AndKeepsEarlierTyping()
    {
        var window = Show();
        var main = Main(window);
        var first = main.Notes.SelectedPad!;
        var other = main.Notes.Pads.First(p => p.Id != first.Id);
        main.Notes.Text = "typed before switching";

        Named<ComboBox>(window, "PadPicker").SelectedItem = other;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(other.Body, Named<TextBox>(window, "PadEditor").Text);
        Assert.Equal("typed before switching", first.Body);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void PadSwitcherAndEditor_FitInsideTheCard_WithTheHeaderShownOrHidden(bool headerShown)
    {
        var window = Show();
        SetHeader(window, headerShown);

        var frame = InWindow(Frame(window), window);
        var bar = InWindow(Named<Grid>(window, "PadBar"), window);
        var editor = InWindow(Named<TextBox>(window, "PadEditor"), window);

        Assert.Equal(headerShown, Frame(window).HeaderShown);
        Assert.True(Named<Grid>(window, "PadBar").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "NewPadButton").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "PadMenuButton").IsEffectivelyVisible);
        Assert.True(bar.Top >= frame.Top && bar.Right <= frame.Right + 0.5, "the switcher is inside the card");
        Assert.True(editor.Top >= bar.Bottom - 0.5 && editor.Bottom <= frame.Bottom + 0.5, "the editor sits under the switcher inside the card");
        // The title row is the whole difference: hidden, the switcher sits where the row was.
        if (headerShown)
        {
            Assert.True(bar.Top >= frame.Top + WidgetFrame.HeaderHeight, "under the title bar");
        }
        else
        {
            Assert.True(bar.Top < frame.Top + WidgetFrame.HeaderHeight, "no title row above the switcher");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void TheMoreMenu_HoldsTheFileAndViewActions()
    {
        var window = Show();
        var menu = (MenuFlyout)Named<Button>(window, "PadMenuButton").Flyout!;

        var headers = menu.Items.OfType<MenuItem>().Select(i => i.Header as string).ToArray();

        Assert.Equal(new[] { "Rename pad…", "Delete pad…", "Open text file…", "Save as text file…", "Monospace font", "Word wrap" }, headers);
        window.Close();
    }

    [AvaloniaFact]
    public void SaveAsTextFile_ExportsTheWholePad_AndOpenImportsACopyAsANewPad()
    {
        var files = new FakeFiles { ToOpen = new NotepadFile("from disk.txt", "a\r\n\r\n    b") };
        var window = Show(files: files);
        var main = Main(window);
        main.Notes.Text = Sample;

        main.Notes.SaveAsTextFileCommand.Execute(null);
        main.Notes.OpenTextFileCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Sample, Assert.Single(files.Saved).Text);
        Assert.Equal("from disk", main.Notes.SelectedPad!.Title);
        Assert.Equal("a\r\n\r\n    b", Named<TextBox>(window, "PadEditor").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void Rename_AndDelete_UseInlineRowsUnderTheSwitcher()
    {
        var window = Show();
        var notes = Main(window).Notes;
        Assert.False(Named<Grid>(window, "RenameBar").IsEffectivelyVisible);

        notes.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<Grid>(window, "RenameBar").IsEffectivelyVisible);
        notes.CancelRenameCommand.Execute(null);

        notes.RequestDeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<Grid>(window, "DeleteBar").IsEffectivelyVisible);
        Assert.Contains(notes.SelectedPad!.Title, Named<TextBlock>(window, "DeleteQuestion").Text);
        notes.CancelDeleteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<Grid>(window, "DeleteBar").IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void MonospaceAndWordWrap_ChangeTheEditor()
    {
        var window = Show();
        var editor = Named<TextBox>(window, "PadEditor");
        Assert.Equal(TextWrapping.Wrap, editor.TextWrapping);
        var proportional = editor.FontFamily;

        Main(window).Notes.WordWrap = false;
        Main(window).Notes.Monospace = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TextWrapping.NoWrap, editor.TextWrapping);
        Assert.NotEqual(proportional, editor.FontFamily);
        window.Close();
    }

    [AvaloniaFact]
    public void HidingTheWidget_SavesWhatWasTyped()
    {
        var window = Show();
        var main = Main(window);
        var pad = main.Notes.SelectedPad!;
        main.Notes.Text = "typed just before hiding";

        main.Layout.EnterEditCommand.Execute(null);
        main.Layout.RemoveWidget("notes");
        main.Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<WidgetFrame>(), f => f.WidgetId == "notes");
        Assert.Equal("typed just before hiding", pad.Body);
        window.Close();
    }

    [AvaloniaFact]
    public void InEditMode_TheEditorIsInert_SoTheCardCanBeDragged_AndTypingWorksAgainAfterDone()
    {
        var window = Show();
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(InWindowHitTestable(window), "the editor does not take the pointer while editing the layout");

        main.Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var editor = Named<TextBox>(window, "PadEditor");
        editor.Focus();
        editor.SelectAll();
        window.KeyTextInput("typing again");
        Assert.Equal("typing again", editor.Text);
        window.Close();
    }

    private static bool InWindowHitTestable(MainWindow window)
    {
        var editor = Named<TextBox>(window, "PadEditor");
        var point = InWindow(editor, window).Center;
        return (window.InputHitTest(point) as Visual)?.GetSelfAndVisualAncestors().Contains(editor) == true;
    }

    [AvaloniaTheory]
    [InlineData(1200, 760, true)]
    [InlineData(1200, 760, false)]
    [InlineData(640, 560, true)]
    [InlineData(640, 560, false)]
    public void Snapshot_Notepad(int width, int height, bool headerShown)
    {
        var window = Show(width, height);
        Main(window).Notes.Text = Sample;
        SetHeader(window, headerShown);
        Dispatcher.UIThread.RunJobs();

        UiSnapshot.Render(window, $"notepad-{width}x{height}-{(headerShown ? "header" : "noheader")}");
        window.Close();
    }
}
