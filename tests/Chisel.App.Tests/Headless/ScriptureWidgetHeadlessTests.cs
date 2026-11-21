using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;
using Chisel.Core.Scripture;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class ScriptureWidgetHeadlessTests
{
    private static readonly ScriptureCatalog Catalog = ScriptureCatalog.Bundled;

    private sealed class MemoryLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    // A clock on a day whose passage is the given one (noon UTC, so the local date is the same wherever the tests run).
    private static FakeTimeProvider ClockFor(ScripturePassage passage)
    {
        var index = Catalog.Passages.ToList().IndexOf(passage);
        var number = 700_000 - 700_000 % Catalog.Passages.Count + index;
        var date = DateOnly.FromDayNumber(number);
        return new FakeTimeProvider(new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
    }

    private static ScripturePassage Longest => Catalog.Passages.MaxBy(p => p.Text.Length)!;

    private static ScripturePassage Shortest => Catalog.Passages.MinBy(p => p.Text.Length)!;

    private static ScripturePassage John => Catalog.Passages.Single(p => p.Reference == "John 3:16-17");

    // Opens the window with the daily verse added to the first column; weight is its share of that column's height.
    private static MainWindow Open(ScripturePassage passage, double width = 1200, double height = 760, double weight = 1,
        MemoryLayoutRepository? repository = null, Action<string>? openUrl = null, FakeTimeProvider? clock = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, clock: clock ?? ClockFor(passage), layoutRepository: repository, openUrl: openUrl);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.Scripture, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static T? Named<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    private static ScriptureWidgetView View(Window w) => w.GetVisualDescendants().OfType<ScriptureWidgetView>().Single();

    private static WidgetFrame Frame(Window w) =>
        w.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).Single(f => f.WidgetId == WidgetRegistry.Scripture);

    private static void Click(Control button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void IsInTheTray_NotOnTheDashboardByDefault_AndCanBeAdded()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = Main(window);
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == WidgetRegistry.Scripture);
        Assert.Empty(window.GetVisualDescendants().OfType<ScriptureWidgetView>());

        main.Layout.AddWidgetFromTray(WidgetRegistry.Scripture);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(window.GetVisualDescendants().OfType<ScriptureWidgetView>());
        window.Close();
    }

    [AvaloniaFact]
    public void ShowsTheVerseFirst_ThenTheReferenceAndWeb()
    {
        var window = Open(Shortest);
        var text = Named<TextBlock>(window, "ScriptureText")!;
        var reference = Named<TextBlock>(window, "ScriptureReference")!;
        Assert.Equal(Shortest.Text, text.Text);
        Assert.Equal(Shortest.Reference, reference.Text);
        Assert.Equal("WEB", Named<TextBlock>(window, "ScriptureTranslation")!.Text);
        Assert.True(text.TranslatePoint(default, window)!.Value.Y < reference.TranslatePoint(default, window)!.Value.Y);
        Assert.Null(Named<Button>(window, "ScriptureMore"));
        window.Close();
    }

    [AvaloniaFact]
    public void HidingTheHeader_RemovesTheWholeRow()
    {
        var window = Open(Shortest);
        var frame = Frame(window);
        Assert.InRange(View(window).TranslatePoint(default, frame)!.Value.Y, WidgetFrame.HeaderHeight, WidgetFrame.HeaderHeight + 3);

        frame.ShowHeader = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(frame.HeaderShown);
        Assert.Null(frame.Header.GetVisualParent());
        Assert.True(View(window).TranslatePoint(default, frame)!.Value.Y < 3);
        window.Close();
    }

    [AvaloniaFact]
    public void ALongPassageInASmallCard_OffersAnExpandedView_InsteadOfShrinkingOrCuttingItOff()
    {
        var window = Open(Longest, width: 640, height: 700, weight: 0.5);
        var text = Named<TextBlock>(window, "ScriptureText")!;
        var more = Named<Button>(window, "ScriptureMore");
        Assert.NotNull(more);
        Assert.True(text.FontSize >= 15, "text keeps a readable size");
        Assert.EndsWith("…", text.Text);
        Assert.StartsWith(text.Text![..^1].TrimEnd(), Longest.Text);
        Assert.True(text.Text.Length < Longest.Text.Length);
        Assert.Null(Named<ScrollViewer>(window, "ScriptureExpanded"));

        Click(more!);
        Assert.NotNull(Named<ScrollViewer>(window, "ScriptureExpanded"));
        var full = Named<TextBlock>(window, "ScriptureText")!;
        Assert.Equal(Longest.Text, full.Text);
        Assert.Equal(0, full.MaxLines);
        Assert.Equal(text.FontSize, full.FontSize);

        Click(Named<Button>(window, "ScriptureLess")!);
        Assert.Null(Named<ScrollViewer>(window, "ScriptureExpanded"));
        Assert.NotNull(Named<Button>(window, "ScriptureMore"));
        window.Close();
    }

    [AvaloniaFact]
    public void ACardTooShortForOneLine_ScrollsTheFullVerse_AtTheSameSize()
    {
        var window = Open(Longest, width: 640, height: 520, weight: 0.3);
        var scroll = Named<ScrollViewer>(window, "ScriptureExpanded");
        Assert.NotNull(scroll);
        var text = Named<TextBlock>(window, "ScriptureText")!;
        Assert.Equal(Longest.Text, text.Text);
        Assert.Equal(0, text.MaxLines);
        Assert.True(text.FontSize >= 15);
        Assert.True(scroll!.Extent.Height > scroll.Viewport.Height);
        Assert.Null(Named<Button>(window, "ScriptureMore"));
        Assert.NotNull(Named<Button>(window, "ScriptureCopy"));
        Assert.NotNull(Named<Button>(window, "ScriptureContext"));
        window.Close();
    }

    [AvaloniaFact]
    public void AMediumPassage_FitsInAWideCard_WithoutTheExpandedView()
    {
        var passage = Catalog.Passages.MinBy(p => Math.Abs(p.Text.Length - 450))!;
        var window = Open(passage, width: 1400, height: 900);
        Assert.Null(Named<Button>(window, "ScriptureMore"));
        Assert.Equal(passage.Text, Named<TextBlock>(window, "ScriptureText")!.Text);
        Assert.Equal(0, Named<TextBlock>(window, "ScriptureText")!.MaxLines);
        window.Close();
    }

    [AvaloniaFact]
    public void Copy_WritesTheVerseReferenceAndTranslation()
    {
        var window = Open(Shortest);
        string? copied = null;
        Main(window).Scripture.WriteClipboard = text => copied = text;
        Click(Named<Button>(window, "ScriptureCopy")!);
        Assert.Equal(Catalog.CopyText(Shortest), copied);
        Assert.Equal("Copied", Named<Button>(window, "ScriptureCopy")!.Content);
        window.Close();
    }

    [AvaloniaFact]
    public void ReadInContext_OpensTheChapter()
    {
        string? opened = null;
        var window = Open(Shortest, openUrl: url => opened = url);
        Click(Named<Button>(window, "ScriptureContext")!);
        Assert.Equal(Catalog.ChapterUrl(Shortest), opened);
        window.Close();
    }

    [AvaloniaFact]
    public void TheVerseStaysTheSame_OnResize_HideAndShow_AndReopen()
    {
        var repository = new MemoryLayoutRepository();
        var passage = Catalog.Passages[7];
        var clock = ClockFor(passage);
        var window = Open(passage, repository: repository, clock: clock);
        Assert.Equal(passage.Reference, Named<TextBlock>(window, "ScriptureReference")!.Text);

        window.Width = 800;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(passage.Reference, Named<TextBlock>(window, "ScriptureReference")!.Text);

        var layout = Main(window).Layout;
        layout.EnterEditCommand.Execute(null);
        layout.RemoveWidget(WidgetRegistry.Scripture);
        layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(Named<TextBlock>(window, "ScriptureReference"));
        layout.EnterEditCommand.Execute(null);
        layout.AddWidgetFromTray(WidgetRegistry.Scripture);
        layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(passage.Reference, Named<TextBlock>(window, "ScriptureReference")!.Text);
        window.Close();

        var reopened = UiSnapshot.ShowMainWindow(clock: clock, layoutRepository: repository);
        Assert.Equal(passage.Reference, Named<TextBlock>(reopened, "ScriptureReference")!.Text);
        reopened.Close();
    }

    [AvaloniaFact]
    public void AtLocalMidnight_TheOpenWidgetMovesToTheNextPassage()
    {
        var clock = ClockFor(Catalog.Passages[7]);
        clock.Advance(TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(59)));
        var window = Open(Catalog.Passages[7], clock: clock);
        Assert.Equal(Catalog.Passages[7].Reference, Named<TextBlock>(window, "ScriptureReference")!.Text);

        clock.Advance(TimeSpan.FromMinutes(2));
        Main(window).Tick();
        Dispatcher.UIThread.RunJobs();
                Assert.Equal(Catalog.Passages[8].Reference, Named<TextBlock>(window, "ScriptureReference")!.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void RendersCompactWideAndLongLayouts()
    {
        var compact = Open(John, width: 640, height: 520, weight: 0.3);
        UiSnapshot.Render(compact, "scripture-compact");
        compact.Close();

        var wide = Open(John);
        UiSnapshot.Render(wide, "scripture-wide");
        wide.Close();

        var tiny = Open(Longest, width: 640, height: 520, weight: 0.3);
        UiSnapshot.Render(tiny, "scripture-long-tiny");
        tiny.Close();

        var longCompact = Open(Longest, width: 640, height: 700, weight: 0.5);
        UiSnapshot.Render(longCompact, "scripture-long-compact");
        Click(Named<Button>(longCompact, "ScriptureMore")!);
        UiSnapshot.Render(longCompact, "scripture-long-expanded");
        longCompact.Close();

        var longWide = Open(Longest);
        UiSnapshot.Render(longWide, "scripture-long-wide");
        longWide.Close();
    }
}
