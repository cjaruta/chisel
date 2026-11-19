using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.App.Views.Widgets.Backdrop;
using Chisel.Core.Layout;
using Chisel.Core.Media;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

// Tabbed groups on the real window: the shared header, one visible body, hidden tabs that are fully idle,
// merging and unmerging by pointer, tab reordering, and the tabs at the narrowest width.
public class WidgetGroupHeadlessTests
{
    private sealed class RecordingSource : ISpectrumSource
    {
        public int BandCount => 8;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void ReadLevels(Span<float> levels, double elapsedSeconds) => levels.Fill(IsRunning ? 0.5f : 0);
        public void Dispose() { }
    }

    private static readonly WeatherSnapshot Rain = new(14, 12, WeatherCondition.Rain, 16, 9,
        Enumerable.Range(0, 8).Select(i => new HourlyWeather(new TimeOnly(10 + i, 0), WeatherCondition.Rain, 14 - i)).ToList());

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static List<WidgetFrame> Frames(Window window) => Host(window).Frames.SelectMany(c => c).ToList();

    private static WidgetFrame Frame(Window window, string id) => Frames(window).Single(f => f.WidgetId == id);

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static T Single<T>(Window window) where T : Visual => window.GetVisualDescendants().OfType<T>().Single();

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value
            - new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), visual.Bounds.Size);

    private static Point Center(Visual visual, Window window) => InWindow(visual, window).Center;

    private static Point Within(Window window, string id, double fraction)
    {
        var bounds = InWindow(Frame(window, id), window);
        return new Point(bounds.Center.X, bounds.Top + bounds.Height * fraction);
    }

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static void Press(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        Settle();
    }

    private static void MoveTo(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Settle();
    }

    private static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static MainWindow Open(Func<WidgetLayout, WidgetLayout> arrange, double width = 1200, double height = 760,
        IMediaService? media = null, ISpectrumSource? spectrum = null, IVolumeService? volume = null,
        string? themeMode = null, bool weatherReady = true)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, media: media, spectrum: spectrum, volume: volume, themeMode: themeMode);
        var main = Main(window);
        if (weatherReady)
        {
            main.Weather.Show(WeatherState.Ready, Rain);
        }
        main.Layout.Apply(arrange(main.Layout.Layout));
        Settle();
        return window;
    }

    private static WidgetLayout WithAllInColumn(WidgetLayout layout, params string[] ids)
    {
        foreach (var id in ids)
        {
            layout = layout.AddWidget(id, 0, layout.Columns[0].Widgets.Count, 1);
        }
        return layout;
    }

    // Clock, Weather and Media on one card, in that order, the clock active.
    private static WidgetLayout ClockWeatherMedia(WidgetLayout layout) =>
        WithAllInColumn(layout, WidgetRegistry.Clock, WidgetRegistry.Weather, WidgetRegistry.Media)
            .Combine(WidgetRegistry.Weather, WidgetRegistry.Clock).Combine(WidgetRegistry.Media, WidgetRegistry.Clock);

    private static WidgetLayout NotesAndTasks(WidgetLayout layout) => layout.Combine("notes", "tasks");

    [AvaloniaFact]
    public void TheGroupHeader_HasTheSameMetricsAsAPlainCardsHeader()
    {
        var window = Open(NotesAndTasks);
        var group = Frame(window, "tasks");
        var plain = Frame(window, "links");
        plain.ShowHeader = true;
        Settle();

        Assert.True(group.IsGroup);
        Assert.Equal(WidgetFrame.HeaderHeight, group.Header.Bounds.Height, 0.5);
        Assert.Equal(plain.Header.Bounds.Height, group.Header.Bounds.Height, 0.5);
        Assert.Equal(plain.Header.Padding, group.Header.Padding);
        Assert.Equal(plain.Header.BorderThickness, group.Header.BorderThickness);
        Assert.Equal(((ISolidColorBrush)plain.Header.Background!).Color, ((ISolidColorBrush)group.Header.Background!).Color);
        Assert.Equal(((ISolidColorBrush)plain.Header.BorderBrush!).Color, ((ISolidColorBrush)group.Header.BorderBrush!).Color);
        Assert.Equal(plain.Header.CornerRadius, group.Header.CornerRadius);
        Assert.Equal(plain.Header.Bounds.Left, group.Header.Bounds.Left, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void AGroupAlwaysShowsItsHeader_AndTheHeaderToggleIsNotOffered()
    {
        var window = Open(layout => layout.Combine("schedule", "notes"));
        var group = Frame(window, "notes");

        Assert.True(group.Header.IsVisible);
        Assert.Contains(group.Header, group.GetVisualDescendants());
        Assert.False(group.HeaderButton.IsVisible);
        Assert.All(group.Views, v => Assert.True(v.HeaderShown));

        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        Assert.False(group.HeaderButton.IsVisible);
        Assert.True(Frame(window, "links").HeaderButton.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ARememberedHeaderChoice_ComesBack_WhenAWidgetLeavesTheGroup()
    {
        var window = Open(layout => layout.Combine("schedule", "notes"));
        var main = Main(window);
        Assert.True(Frame(window, "notes").View.HeaderShown);

        main.Layout.EnterEditCommand.Execute(null);
        main.Layout.Unmerge("schedule");
        Settle();

        Assert.False(Frame(window, "schedule").ShowHeader);
        Assert.False(Frame(window, "schedule").View.HeaderShown);
        Assert.True(Frame(window, "notes").View.HeaderShown);
        window.Close();
    }

    [AvaloniaFact]
    public void OnlyTheActiveMember_IsShown_AndClickingATabSwitchesAtOnce()
    {
        var window = Open(NotesAndTasks);
        var group = Frame(window, "tasks");
        var tasks = group.Views[0];
        var notes = group.Views[1];

        Assert.True(tasks.IsEffectivelyVisible);
        Assert.False(notes.IsEffectivelyVisible);
        Assert.True(group.TabButton("tasks").Classes.Contains("active"));
        Assert.False(group.TabButton("notes").Classes.Contains("active"));

        Click(window, Center(group.TabButton("notes"), window));

        Assert.False(tasks.IsEffectivelyVisible);
        Assert.True(notes.IsEffectivelyVisible);
        Assert.Equal("notes", group.ActiveId);
        Assert.True(group.TabButton("notes").Classes.Contains("active"));
        window.Close();
    }

    [AvaloniaFact]
    public void Tabs_UseAccentWhenActive_SecondaryWhenNot_AndHaveAccessibleNames()
    {
        var window = Open(NotesAndTasks);
        var group = Frame(window, "tasks");
        Color Text(string id) => ((ISolidColorBrush)group.TabButton(id).GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
            .First().Foreground!).Color;
        Color Brush(string key) => ((ISolidColorBrush)Chisel.App.Theming.ThemeService.Brush(group, key)).Color;

        Assert.Equal(Brush("AccentTextBrush"), Text("tasks"));
        Assert.Equal(Brush("DimTextBrush"), Text("notes"));
        Assert.Equal("Tasks", AutomationProperties.GetName(group.TabButton("tasks")));
        Assert.Equal("Notes", AutomationProperties.GetName(group.TabButton("notes")));
        Assert.True(group.TabButton("notes").Focusable);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraftTypedInAHiddenTab_SurvivesSwitching_AndTheSelectionIsNotLost()
    {
        var window = Open(NotesAndTasks);
        var main = Main(window);
        var group = Frame(window, "tasks");

        main.Notes.NewNoteTitle = "Draft title";
        main.Tasks.NewTaskText = "Draft task";
        Click(window, Center(group.TabButton("notes"), window));
        Click(window, Center(group.TabButton("tasks"), window));

        Assert.Equal("Draft title", main.Notes.NewNoteTitle);
        Assert.Equal("Draft task", main.Tasks.NewTaskText);
        var box = group.Views[1].GetVisualDescendants().OfType<TextBox>().First();
        Assert.Equal("Draft title", box.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHiddenWeatherMediaAndClockTabs_AreFullyIdle()
    {
        var media = new FakeMediaService { Current = FakeMediaService.Sample() };
        var source = new RecordingSource();
        var window = Open(ClockWeatherMedia, media: media, spectrum: source);
        var main = Main(window);
        var group = Frame(window, WidgetRegistry.Clock);
        var clock = (ClockWidgetView)group.Views[0];
        var weather = (WeatherWidgetView)group.Views[1];
        var mediaView = (MediaWidgetView)group.Views[2];
        var layer = WeatherMotionLayer.Of(weather)!;

        // Clock is showing: the others do nothing.
        Assert.True(clock.IsFollowingClock);
        Assert.False(layer.IsAnimating);
        Assert.False(source.IsRunning);
        Assert.Empty(mediaView.GetVisualDescendants().OfType<SpectrumBars>().Where(b => b.IsCapturing));
        Assert.Empty(mediaView.GetVisualDescendants().OfType<AlbumGradientBackground>().Where(g => g.IsRunning));

        // The media session keeps going in the background.
        main.Refresh();
        Assert.True(main.Media.IsActive);
        Assert.True(main.Weather.IsActive);

        main.Layout.SelectTab(WidgetRegistry.Weather);
        Settle();
        Assert.False(clock.IsFollowingClock);
        Assert.True(layer.IsAnimating);
        Assert.False(source.IsRunning);
        Assert.Empty(mediaView.GetVisualDescendants().OfType<AlbumGradientBackground>().Where(g => g.IsRunning));

        main.Layout.SelectTab(WidgetRegistry.Media);
        Settle();
        Assert.False(layer.IsAnimating);
        Assert.True(source.IsRunning);
        Assert.True(mediaView.GetVisualDescendants().OfType<SpectrumBars>().Single().IsCapturing);

        main.Layout.SelectTab(WidgetRegistry.Clock);
        Settle();
        Assert.False(source.IsRunning);
        Assert.Empty(mediaView.GetVisualDescendants().OfType<SpectrumBars>().Where(b => b.IsCapturing));
        Assert.Empty(mediaView.GetVisualDescendants().OfType<AlbumGradientBackground>().Where(g => g.IsRunning));
        Assert.False(layer.IsAnimating);
        Assert.True(clock.IsFollowingClock);
        window.Close();
    }

    [AvaloniaFact]
    public void ScrollingTheWheel_ChangesTheVolume_OnlyWhileTheMediaTabIsVisible()
    {
        var media = new FakeMediaService { Current = FakeMediaService.Sample() };
        var volume = new FakeVolumeService();
        var window = Open(layout => WithAllInColumn(layout, WidgetRegistry.Weather, WidgetRegistry.Media)
            .Combine(WidgetRegistry.Media, WidgetRegistry.Weather), media: media, volume: volume);
        Main(window).MediaSettings.ScrollVolume = true;
        var group = Frame(window, WidgetRegistry.Weather);
        var before = volume.Level;
        var at = Center(group, window);

        window.MouseMove(at);
        window.MouseWheel(at, new Vector(0, 1));
        Settle();
        Assert.Equal(before, volume.Level);

        Main(window).Layout.SelectTab(WidgetRegistry.Media);
        Settle();
        window.MouseMove(at);
        window.MouseWheel(at, new Vector(0, 1));
        Settle();
        Assert.NotEqual(before, volume.Level);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingAWidgetOntoAnotherCard_HighlightsIt_AndMergesOnDrop()
    {
        var window = Open(l => l);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        var from = Center(Frame(window, "notes").EditBar, window);
        var over = Within(window, "tasks", 0.5);

        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        MoveTo(window, over);

        Assert.Same(Frame(window, "tasks"), Host(window).MergeTarget);
        var preview = Single<Border>(window, "DropPreview");
        Assert.True(preview.IsVisible);
        Assert.Equal(InWindow(Frame(window, "tasks"), window).Width, preview.Bounds.Width, 1);
        Assert.Contains("Tab with Tasks", Single<Border>(window, "DropPreviewCaption").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

        Release(window, over);

        var group = Frame(window, "tasks");
        Assert.True(group.IsGroup);
        Assert.Equal(new[] { "tasks", "notes" }, group.MemberIds);
        Assert.Equal("tasks", group.ActiveId);
        Assert.Null(Host(window).MergeTarget);
        Assert.DoesNotContain(Frames(window), f => f.WidgetId == "notes");
        window.Close();
    }

    private static Border Single<T>(Window window, string name) where T : Border =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == name);

    [AvaloniaFact]
    public void DroppingOnTheHeader_AlsoMerges()
    {
        var window = Open(l => l);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        var from = Center(Frame(window, "links").EditBar, window);
        var tasks = InWindow(Frame(window, "tasks"), window);
        var onHeader = new Point(tasks.Center.X, tasks.Top + WidgetFrame.HeaderHeight / 2 + 8);

        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        MoveTo(window, onHeader);
        Assert.Same(Frame(window, "tasks"), Host(window).MergeTarget);
        Release(window, onHeader);

        Assert.True(Frame(window, "tasks").IsGroup);
        window.Close();
    }

    [AvaloniaFact]
    public void DroppingNearTheEdgeOfACard_StillInsertsInsteadOfMerging()
    {
        var window = Open(l => l);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        var from = Center(Frame(window, "links").EditBar, window);
        var edge = Within(window, "tasks", 0.05);

        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        MoveTo(window, edge);
        Assert.Null(Host(window).MergeTarget);
        Release(window, edge);

        Assert.All(Frames(window), f => Assert.False(f.IsGroup));
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeDuringAMergeDrag_ChangesNothing()
    {
        var window = Open(l => l);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        var from = Center(Frame(window, "notes").EditBar, window);

        Press(window, from);
        MoveTo(window, new Point(from.X + 10, from.Y + 10));
        MoveTo(window, Within(window, "tasks", 0.5));
        Assert.NotNull(Host(window).MergeTarget);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Settle();

        Assert.Null(Host(window).MergeTarget);
        Assert.All(Frames(window), f => Assert.False(f.IsGroup));
        window.Close();
    }

    [AvaloniaFact]
    public void TheUnmergeButton_ShowsOnlyInEditMode_AndMovesAWidgetBackOut()
    {
        var window = Open(NotesAndTasks);
        var group = Frame(window, "tasks");
        Assert.False(group.UnmergeButton("notes").IsVisible);

        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        Assert.True(group.UnmergeButton("notes").IsVisible);
        Assert.True(group.UnmergeButton("tasks").IsVisible);

        Click(window, Center(group.UnmergeButton("notes"), window));

        Assert.All(Frames(window), f => Assert.False(f.IsGroup));
        Assert.Contains(Frames(window), f => f.WidgetId == "notes");
        Assert.Contains(Frames(window), f => f.WidgetId == "tasks");
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingATab_InEditMode_ReordersTheTabs()
    {
        var window = Open(NotesAndTasks);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();
        var group = Frame(window, "tasks");
        var from = Center(group.TabButton("tasks"), window);
        var to = Center(group.TabButton("notes"), window);

        Press(window, from);
        MoveTo(window, new Point(from.X + 12, from.Y));
        MoveTo(window, to);
        Release(window, to);

        Assert.Equal(new[] { "notes", "tasks" }, Frame(window, "tasks").MemberIds.ToArray());
        Assert.Equal("tasks", Frame(window, "tasks").ActiveId);
        window.Close();
    }

    [AvaloniaFact]
    public void PressingAndMovingOnATab_OutsideEditMode_DoesNotDragAnything()
    {
        var window = Open(NotesAndTasks);
        var group = Frame(window, "tasks");
        var from = Center(group.TabButton("tasks"), window);
        var to = Center(group.TabButton("notes"), window);

        Press(window, from);
        MoveTo(window, to);
        Release(window, to);

        Assert.Equal(new[] { "tasks", "notes" }, group.MemberIds.ToArray());
        Assert.False(Main(window).Layout.IsDraggingWidget);
        window.Close();
    }

    [AvaloniaFact]
    public void FiveTabsAtTheMinimumWidth_ShareTheSpaceEvenly_AndTruncateWithAnEllipsis()
    {
        var ids = new[] { "tasks", "notes", "links", "schedule", "clock" };
        var window = Open(_ => new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[]
            {
                new WidgetPlacement("tasks", 1, null, ids.Select(id => new GroupMember(id)).ToList(), 0),
            }),
        }, new[] { WidgetRegistry.Weather, WidgetRegistry.Media }), width: 300, height: 600);
        var group = Frame(window, "tasks");

        Assert.Equal(5, group.MemberIds.Count);
        var cells = group.TabStrip.Children.OfType<Grid>().ToList();
        Assert.Equal(5, cells.Count);
        Assert.All(cells, c => Assert.InRange(c.Bounds.Width, cells[0].Bounds.Width - 1, cells[0].Bounds.Width + 1));
        Assert.True(cells.Sum(c => c.Bounds.Width) <= group.Header.Bounds.Width + 1);

        var links = group.TabButton("links").GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(TextTrimming.CharacterEllipsis, links.TextTrimming);
        links.Measure(new Size(double.PositiveInfinity, 100));
        Assert.True(links.DesiredSize.Width > cells[0].Bounds.Width - 20, "the long title does not fit its tab, so it is cut");
        window.Close();
    }

    [AvaloniaFact]
    public void RemovingAGroup_InEditMode_TakesAllItsWidgetsOffTheDashboard()
    {
        var window = Open(NotesAndTasks);
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();

        Click(window, Center(Frame(window, "tasks").RemoveButton, window));

        Assert.DoesNotContain(Frames(window), f => f.WidgetId is "tasks" or "notes");
        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == "tasks");
        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == "notes");
        window.Close();
    }

    [AvaloniaFact]
    public void CancelAndDone_WorkOnTheWholeLayoutIncludingGroups()
    {
        var window = Open(l => l);
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        main.Layout.BeginWidgetDrag("notes");
        main.Layout.DropOnWidget("tasks");
        Settle();
        Assert.True(Frame(window, "tasks").IsGroup);

        main.Layout.CancelCommand.Execute(null);
        Settle();
        Assert.All(Frames(window), f => Assert.False(f.IsGroup));
        Assert.Equal(4, Frames(window).Count);

        main.Layout.EnterEditCommand.Execute(null);
        main.Layout.BeginWidgetDrag("notes");
        main.Layout.DropOnWidget("tasks");
        main.Layout.DoneCommand.Execute(null);
        Settle();
        Assert.True(Frame(window, "tasks").IsGroup);
        Assert.Equal(new[] { "tasks", "notes" }, Frame(window, "tasks").MemberIds.ToArray());
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("default")]
    [InlineData("light")]
    public void TheGroupsCard_RendersInTheShownMode(string mode)
    {
        var window = Open(NotesAndTasks, themeMode: mode);

        Assert.Equal(mode, Main(window).Settings.SelectedThemeMode.Id);
        Assert.NotNull(UiSnapshot.Render(window, $"group-{mode}-smoke"));
        window.Close();
    }
}
