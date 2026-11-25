using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clipboard;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.Headless;

public class ClipboardHistoryWidgetHeadlessTests
{
    private readonly FakeClipboard _clipboard = new();

    private MainWindow Open(double weight = 3, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(themeMode: themeMode, clipboard: _clipboard);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.ClipboardHistory, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Settle();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static ClipboardHistoryWidgetView View(Window w) => w.GetVisualDescendants().OfType<ClipboardHistoryWidgetView>().Single();

    private static T Named<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private void Copy(string text)
    {
        _clipboard.Copy(text);
        Settle();
    }

    private static string[] RowNames(Window w) =>
        Named<ListBox>(w, "ClipList").GetVisualDescendants().OfType<ListBoxItem>().Select(i => AutomationProperties.GetName(i) ?? "").ToArray();

    [AvaloniaFact]
    public void Registry_HasClipboardHistory_OffTheDashboardByDefault()
    {
        var definition = WidgetRegistry.Get(WidgetRegistry.ClipboardHistory)!;
        Assert.Equal("Clipboard history", definition.Title);
        Assert.Equal(typeof(ClipboardHistoryWidgetView), definition.ViewType);
        Assert.Contains("off until", definition.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WidgetRegistry.ClipboardHistory, WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }

    [AvaloniaFact]
    public void StartsOff_WithAPlainExplanation_AndStartBeginsRecording()
    {
        var window = Open();
        _clipboard.Copy("copied before start");
        Settle();

        var intro = Named<TextBlock>(window, "ClipIntro");
        Assert.True(intro.IsEffectivelyVisible);
        Assert.Contains("only text", intro.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("can't catch everything", intro.Text);
        Assert.False(_clipboard.IsListening);

        Click(Named<Button>(window, "ClipStart"));
        Copy("Shopping list for Saturday");

        Assert.True(_clipboard.IsListening);
        Assert.False(intro.IsEffectivelyVisible);
        var row = Assert.Single(RowNames(window));
        Assert.Contains("Shopping list for Saturday", row);
        Assert.Contains("26 characters", row);
        window.Close();
    }

    [AvaloniaFact]
    public void ARowsAutomationName_ReadsOnlyThePreview()
    {
        var window = Open();
        Main(window).ClipboardHistory.Start();
        var tail = "TAIL-" + new string('q', 10);
        Copy(new string('w', 300) + tail);

        var row = Assert.Single(RowNames(window));
        Assert.DoesNotContain(tail, row);
        Assert.Contains("315 characters", row);
        window.Close();
    }

    [AvaloniaFact]
    public void RemovingTheWidget_StopsRecordingAndReleasesTheListener()
    {
        var window = Open();
        Click(Named<Button>(window, "ClipStart"));
        Assert.True(_clipboard.IsListening);

        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.RemoveWidget(WidgetRegistry.ClipboardHistory));
        Settle();

        Assert.False(_clipboard.IsListening);
        Assert.False(Main(window).ClipboardHistory.IsRecording);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_ArrowsMove_EnterCopies_DeleteRemoves()
    {
        var window = Open();
        Main(window).ClipboardHistory.Start();
        Copy("older");
        Copy("newer");
        var list = Named<ListBox>(window, "ClipList");
        list.SelectedIndex = 0;
        Settle();
        list.ContainerFromIndex(0)!.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Settle();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();

        Assert.Equal("older", _clipboard.Current.Text);
        Assert.Equal(new[] { "newer", "older" }, Main(window).ClipboardHistory.Items.Select(i => i.Text)); // its own copy isn't recorded
        Assert.True(Named<TextBlock>(window, "ClipCopied").IsEffectivelyVisible);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Settle();
        Assert.Equal(new[] { "newer" }, Main(window).ClipboardHistory.Items.Select(i => i.Text));
        window.Close();
    }

    [AvaloniaFact]
    public void PauseAndClearAll_WorkFromTheToolbar()
    {
        var window = Open();
        Click(Named<Button>(window, "ClipStart"));
        Copy("kept");

        Click(Named<Button>(window, "ClipPause"));
        Copy("while paused");
        Assert.False(_clipboard.IsListening);
        Assert.Single(RowNames(window));

        Click(Named<Button>(window, "ClipClear"));
        Settle();
        Assert.Empty(Main(window).ClipboardHistory.Items);
        window.Close();
    }

    [AvaloniaFact]
    public void Options_ExplainWhereKeptHistoryGoes_AndTheLimits()
    {
        var window = Open();
        Click(Named<Button>(window, "ClipOptions"));
        Settle();

        var keep = Named<CheckBox>(window, "ClipKeep");
        Assert.True(keep.IsEffectivelyVisible);
        Assert.False(keep.IsChecked);
        keep.IsChecked = true;
        Settle();

        Assert.True(Main(window).ClipboardHistory.KeepHistory);
        Assert.Contains("clipboard-history.json", Named<TextBlock>(window, "ClipKeepNote").Text);
        Assert.Contains("can't catch everything", Named<TextBlock>(window, "ClipFilterNote").Text);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("clipboard-latest", 200, 90, ClipboardDetail.Latest)]
    [InlineData("clipboard-list", 240, 220, ClipboardDetail.List)]
    [InlineData("clipboard-full", 1200, 760, ClipboardDetail.Full)]
    public void Sizes_ShowTheRightDetail_WithNothingClipped(string name, double columnWidth, double height, ClipboardDetail detail)
    {
        var window = Open();
        Main(window).ClipboardHistory.Start();
        Copy("Meeting moved to 3pm on Friday");
        Copy("https://example.com/articles/some-long-article-title");
        Copy("A much longer note that will not fit on one line in a narrow widget, so it ends with an ellipsis");

        var layout = Main(window).Layout;
        var single = new WidgetLayout(new[]
        {
            new LayoutColumn(columnWidth, new[] { new WidgetPlacement(WidgetRegistry.ClipboardHistory, 1) }),
            new LayoutColumn(1200 - columnWidth, new[] { new WidgetPlacement(WidgetRegistry.Schedule, 1) }),
        }, hiddenWidgetIds: WidgetRegistry.All.Select(w => w.Id).Where(id => id != WidgetRegistry.ClipboardHistory && id != WidgetRegistry.Schedule));
        layout.Apply(single);
        window.Height = height + 120;
        Settle();
        var view = View(window);

        Assert.Equal(detail, view.Layout.Detail);
        Assert.Equal(detail == ClipboardDetail.Full, Named<TextBox>(window, "ClipSearch").IsEffectivelyVisible);
        Assert.Equal(detail >= ClipboardDetail.List, Named<ListBox>(window, "ClipList").IsEffectivelyVisible);
        Assert.Equal(detail == ClipboardDetail.Latest, Named<Control>(window, "ClipLatest").IsEffectivelyVisible);
        foreach (var control in view.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Name?.StartsWith("Clip") == true))
        {
            var topLeft = control.TranslatePoint(default, view)!.Value;
            Assert.True(topLeft.Y + control.Bounds.Height <= view.Bounds.Height + 0.5, $"{control.Name} runs past the bottom");
            Assert.True(topLeft.X + control.Bounds.Width <= view.Bounds.Width + 0.5, $"{control.Name} runs past the right edge");
        }
        UiSnapshot.Render(window, name);
        window.Close();
    }

    [AvaloniaFact]
    public void Render_OffAndLight()
    {
        var window = Open(themeMode: "light");
        UiSnapshot.Render(window, "clipboard-off-light");
        window.Close();
    }
}
