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

// Split view on the real window: panes side by side with the usual header metrics, the toggle in each mode, the
// narrow fallback, the splitter, and that every pane is live in split view while hidden tabs stay idle in tabs.
public class GroupSplitViewHeadlessTests
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

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static Point Center(Visual visual, Window window) => InWindow(visual, window).Center;

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static WidgetPlacement Card(GroupViewMode mode, params string[] ids) =>
        new(ids[0], 1, null, ids.Select(id => new GroupMember(id)).ToList(), 0, mode);

    // One column holding just this card; everything else waits in the tray.
    private static WidgetLayout Only(WidgetPlacement card)
    {
        var placed = card.MemberIds.ToHashSet();
        return new WidgetLayout(new[] { new LayoutColumn(1, new[] { card }) },
            WidgetRegistry.All.Select(w => w.Id).Where(id => !placed.Contains(id)));
    }

    private static MainWindow Open(WidgetPlacement card, double width = 1200, IMediaService? media = null,
        ISpectrumSource? spectrum = null, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, 760, media: media, spectrum: spectrum, themeMode: themeMode);
        Main(window).Weather.Show(WeatherState.Ready, Rain);
        Main(window).Layout.Apply(Only(card));
        Settle();
        return window;
    }

    private static GroupViewMode SavedMode(Window window, string id) =>
        Main(window).Layout.Layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Contains(id)).ViewMode;

    private static readonly string[] ClockWeather = { WidgetRegistry.Clock, WidgetRegistry.Weather };
    private static readonly string[] ClockWeatherMedia = { WidgetRegistry.Clock, WidgetRegistry.Weather, WidgetRegistry.Media };

    [AvaloniaFact]
    public void SplitMode_LaysPanesOutSideBySide_WithNoTabStrip_AndEveryPaneVisible()
    {
        var window = Open(Card(GroupViewMode.Split, ClockWeather));
        var frame = Frame(window, WidgetRegistry.Clock);
        var clock = InWindow(frame.Pane(WidgetRegistry.Clock), window);
        var weather = InWindow(frame.Pane(WidgetRegistry.Weather), window);

        Assert.True(frame.IsSplitShown);
        Assert.DoesNotContain(frame.TabStrip, frame.GetVisualDescendants());
        Assert.Equal(clock.Top, weather.Top, 0.5);
        Assert.Equal(clock.Height, weather.Height, 0.5);
        Assert.Equal(GroupSplit.Gap, weather.Left - clock.Right, 0.5);
        Assert.Equal(clock.Width, weather.Width, 1);
        Assert.All(frame.Views, v => Assert.True(v.IsEffectivelyVisible));
        Assert.Equal(CornerRadiusOf(frame.Pane(WidgetRegistry.Clock)), CornerRadiusOf(frame.Pane(WidgetRegistry.Weather)));
        window.Close();
    }

    private static CornerRadius CornerRadiusOf(Border border) => border.CornerRadius;

    [AvaloniaFact]
    public void PanesFollowTheirWeights_AndFillTheCard()
    {
        var card = new WidgetPlacement(WidgetRegistry.Clock, 1, null,
            new[] { new GroupMember(WidgetRegistry.Clock, null, 1), new GroupMember(WidgetRegistry.Weather, null, 3) }, 0, GroupViewMode.Split);
        var window = Open(card);
        var frame = Frame(window, WidgetRegistry.Clock);
        var clock = frame.Pane(WidgetRegistry.Clock).Bounds.Width;
        var weather = frame.Pane(WidgetRegistry.Weather).Bounds.Width;

        Assert.Equal(3, weather / clock, 0.05);
        Assert.Equal(frame.Bounds.Width - frame.BorderThickness.Left - frame.BorderThickness.Right, clock + GroupSplit.Gap + weather, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void EveryPaneHeader_HasTheSameMetricsAsAPlainCardsHeader()
    {
        var window = UiSnapshot.ShowMainWindow(1400, 760);
        Main(window).Layout.Apply(new WidgetLayout(new[]
        {
            new LayoutColumn(2, new[] { Card(GroupViewMode.Split, ClockWeather) }),
            new LayoutColumn(1, new[] { new WidgetPlacement("scripture", 1, true) }),
        }, WidgetRegistry.All.Select(w => w.Id).Where(id => id != "scripture" && !ClockWeather.Contains(id))));
        Settle();
        var frame = Frame(window, WidgetRegistry.Clock);
        var plain = Frame(window, "scripture");

        foreach (var id in ClockWeather)
        {
            var header = frame.PaneHeader(id);
            Assert.Equal(WidgetFrame.HeaderHeight, header.Bounds.Height, 0.5);
            Assert.Equal(plain.Header.Padding, header.Padding);
            Assert.Equal(plain.Header.BorderThickness, header.BorderThickness);
            Assert.Equal(plain.Header.CornerRadius, header.CornerRadius);
            Assert.Equal(((ISolidColorBrush)plain.Header.Background!).Color, ((ISolidColorBrush)header.Background!).Color);
            Assert.Equal(((ISolidColorBrush)plain.Header.BorderBrush!).Color, ((ISolidColorBrush)header.BorderBrush!).Color);
            Assert.Equal(frame.Pane(id).Bounds.Width - 2, header.Bounds.Width, 0.5);
        }
        Assert.All(frame.Views, v => Assert.True(v.HeaderShown));
        window.Close();
    }

    [AvaloniaFact]
    public void TheToggle_SitsAtTheRightEndOfTheHeaderInTabs_AndInTheGapInSplit()
    {
        var window = Open(Card(GroupViewMode.Tabs, ClockWeather));
        var frame = Frame(window, WidgetRegistry.Clock);
        var header = InWindow(frame.Header, window);
        var button = InWindow(frame.ViewModeButton, window);

        Assert.True(frame.ViewModeButton.IsVisible);
        Assert.Equal("Split view", AutomationProperties.GetName(frame.ViewModeButton));
        Assert.InRange(button.Right, header.Right - 20, header.Right);
        Assert.InRange(button.Center.Y, header.Top, header.Bottom);

        Click(window, Center(frame.ViewModeButton, window));

        Assert.True(frame.IsSplitShown);
        Assert.Equal(GroupViewMode.Split, SavedMode(window, WidgetRegistry.Clock));
        var clock = InWindow(frame.Pane(WidgetRegistry.Clock), window);
        var weather = InWindow(frame.Pane(WidgetRegistry.Weather), window);
        button = InWindow(frame.ViewModeButton, window);
        Assert.Equal("Tabs view", AutomationProperties.GetName(frame.ViewModeButton));
        Assert.InRange(button.Center.X, clock.Right, weather.Left);
        Assert.InRange(button.Center.Y, clock.Top, clock.Top + WidgetFrame.HeaderHeight);

        Click(window, Center(frame.ViewModeButton, window));
        Assert.False(frame.IsSplitShown);
        Assert.Equal(GroupViewMode.Tabs, SavedMode(window, WidgetRegistry.Clock));
        window.Close();
    }

    [AvaloniaFact]
    public void ThePlainCardHasNoToggle()
    {
        var window = UiSnapshot.ShowMainWindow();
        Assert.All(Host(window).Frames.SelectMany(c => c), f => Assert.False(f.ViewModeButton.IsVisible));
        window.Close();
    }

    [AvaloniaFact]
    public void TheToggle_WorksInEditMode_AsPartOfTheWorkingCopy()
    {
        var window = Open(Card(GroupViewMode.Tabs, ClockWeather));
        var layout = Main(window).Layout;
        var frame = Frame(window, WidgetRegistry.Clock);
        layout.EnterEditCommand.Execute(null);
        Settle();

        Click(window, Center(frame.ViewModeButton, window));
        Assert.True(frame.IsSplitShown);
        Assert.Equal(GroupViewMode.Split, SavedMode(window, WidgetRegistry.Clock));

        // In the gap, above the drag layer, still clickable in edit mode.
        Click(window, Center(frame.ViewModeButton, window));
        Assert.False(frame.IsSplitShown);

        Click(window, Center(frame.ViewModeButton, window));
        layout.CancelCommand.Execute(null);
        Settle();
        Assert.False(Frame(window, WidgetRegistry.Clock).IsSplitShown);
        window.Close();
    }

    [AvaloniaFact]
    public void TheToggle_IsDisabledWithATooltip_ForTooManyWidgets_AndForNarrowCards()
    {
        var four = Open(Card(GroupViewMode.Tabs, "tasks", "notes", WidgetRegistry.Clock, WidgetRegistry.Weather));
        var frame = Frame(four, "tasks");
        Assert.False(frame.ViewModeButton.IsEnabled);
        Assert.Contains("at most 3", (string)ToolTip.GetTip(frame.ViewModeButton)!);
        four.Close();

        var narrow = Open(Card(GroupViewMode.Tabs, ClockWeatherMedia), width: 640);
        frame = Frame(narrow, WidgetRegistry.Clock);
        Assert.False(frame.ViewModeButton.IsEnabled);
        Assert.Contains("too narrow", (string)ToolTip.GetTip(frame.ViewModeButton)!);
        narrow.Close();

        var fine = Open(Card(GroupViewMode.Tabs, ClockWeather));
        Assert.True(Frame(fine, WidgetRegistry.Clock).ViewModeButton.IsEnabled);
        Assert.Equal("Show the widgets side by side", (string)ToolTip.GetTip(Frame(fine, WidgetRegistry.Clock).ViewModeButton)!);
        fine.Close();
    }

    [AvaloniaFact]
    public void ANarrowCard_ShowsTabs_WithoutChangingTheSavedMode_AndReturnsToSplitWhenThereIsRoom()
    {
        var window = Open(Card(GroupViewMode.Split, ClockWeatherMedia), width: 640);
        var frame = Frame(window, WidgetRegistry.Clock);

        Assert.False(frame.IsSplitShown);
        Assert.Contains(frame.TabStrip, frame.GetVisualDescendants());
        Assert.Equal(GroupViewMode.Split, SavedMode(window, WidgetRegistry.Clock));
        Assert.False(frame.Views[1].IsEffectivelyVisible);

        window.Width = 1200;
        Settle();
        window.UpdateLayout();
        Settle();

        Assert.True(frame.IsSplitShown);
        Assert.All(frame.Views, v => Assert.True(v.IsEffectivelyVisible));
        Assert.Equal(GroupViewMode.Split, SavedMode(window, WidgetRegistry.Clock));
        window.Close();
    }

    [AvaloniaFact]
    public void TheSplitter_ChangesTheWidths_AndStopsAtEveryMinimumWidth()
    {
        var window = Open(Card(GroupViewMode.Split, ClockWeather));
        var frame = Frame(window, WidgetRegistry.Clock);
        var clockMin = WidgetRegistry.Get(WidgetRegistry.Clock)!.MinWidth;
        var weatherMin = WidgetRegistry.Get(WidgetRegistry.Weather)!.MinWidth;
        var splitter = frame.PaneSplitter(0);
        var start = Center(splitter, window);

        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(60, 0), RawInputModifiers.LeftMouseButton);
        Settle();
        Assert.True(frame.Pane(WidgetRegistry.Clock).Bounds.Width > frame.Pane(WidgetRegistry.Weather).Bounds.Width);

        window.MouseMove(start + new Vector(2000, 0), RawInputModifiers.LeftMouseButton);
        Settle();
        Assert.Equal(weatherMin, frame.Pane(WidgetRegistry.Weather).Bounds.Width, 1);

        window.MouseMove(start + new Vector(-2000, 0), RawInputModifiers.LeftMouseButton);
        Settle();
        Assert.Equal(clockMin, frame.Pane(WidgetRegistry.Clock).Bounds.Width, 1);
        window.MouseUp(start + new Vector(-2000, 0), MouseButton.Left);
        Settle();

        var saved = Main(window).Layout.Layout.Columns[0].Widgets[0].Members!;
        Assert.True(saved[0].WidthWeight < saved[1].WidthWeight);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSplitter_CanBeUsedFromTheKeyboard_AndHasAnAccessibleName()
    {
        var window = Open(Card(GroupViewMode.Split, ClockWeather));
        var frame = Frame(window, WidgetRegistry.Clock);
        var splitter = frame.PaneSplitter(0);
        var before = frame.Pane(WidgetRegistry.Clock).Bounds.Width;

        Assert.True(splitter.Focusable);
        Assert.Equal("Resize Clock and Weather", AutomationProperties.GetName(splitter));
        splitter.Focus();
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Settle();

        Assert.True(frame.Pane(WidgetRegistry.Clock).Bounds.Width > before);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        Settle();
        Assert.True(frame.Pane(WidgetRegistry.Clock).Bounds.Width < before);
        window.Close();
    }

    [AvaloniaFact]
    public void TheMoveOutArrow_WorksInSplitView_AndTheRestStaysSplit()
    {
        var window = Open(Card(GroupViewMode.Split, ClockWeatherMedia));
        var layout = Main(window).Layout;
        layout.EnterEditCommand.Execute(null);
        Settle();
        var frame = Frame(window, WidgetRegistry.Clock);

        Assert.True(frame.UnmergeButton(WidgetRegistry.Weather).IsVisible);
        Click(window, Center(frame.UnmergeButton(WidgetRegistry.Weather), window));

        var rest = Frame(window, WidgetRegistry.Clock);
        Assert.Equal(new[] { WidgetRegistry.Clock, WidgetRegistry.Media }, rest.MemberIds);
        Assert.True(rest.IsSplitShown);
        Assert.False(Frame(window, WidgetRegistry.Weather).IsGroup);
        window.Close();
    }

    [AvaloniaFact]
    public void InSplitView_EveryPaneIsLive_AndHiddenTabsGoIdleAgainWhenSwitchingBack()
    {
        var media = new FakeMediaService { Current = FakeMediaService.Sample() };
        var source = new RecordingSource();
        var window = Open(Card(GroupViewMode.Tabs, ClockWeatherMedia), media: media, spectrum: source);
        var frame = Frame(window, WidgetRegistry.Clock);
        var clock = (ClockWidgetView)frame.Views[0];
        var weather = (WeatherWidgetView)frame.Views[1];
        var mediaView = (MediaWidgetView)frame.Views[2];
        var motion = WeatherMotionLayer.Of(weather)!;
        Assert.False(motion.IsAnimating);
        Assert.False(source.IsRunning);

        Main(window).Layout.SetViewMode(WidgetRegistry.Clock, GroupViewMode.Split, frame.Bounds.Width);
        Settle();

        Assert.True(frame.IsSplitShown);
        Assert.True(clock.IsFollowingClock);
        Assert.True(motion.IsAnimating);
        Assert.True(source.IsRunning);
        Assert.True(mediaView.GetVisualDescendants().OfType<SpectrumBars>().Single().IsCapturing);

        Main(window).Layout.SetViewMode(WidgetRegistry.Clock, GroupViewMode.Tabs, frame.Bounds.Width);
        Settle();

        Assert.False(frame.IsSplitShown);
        Assert.True(clock.IsFollowingClock);
        Assert.False(motion.IsAnimating);
        Assert.False(source.IsRunning);
        Assert.Empty(mediaView.GetVisualDescendants().OfType<SpectrumBars>().Where(b => b.IsCapturing));
        window.Close();
    }

    [AvaloniaFact]
    public void ADraftInASplitPane_SurvivesSwitchingToTabsAndBack()
    {
        var window = Open(Card(GroupViewMode.Split, "tasks", "notes"));
        var main = Main(window);
        main.Tasks.NewTaskText = "Draft task";

        main.Layout.SetViewMode("tasks", GroupViewMode.Tabs, 900);
        Settle();
        main.Layout.SetViewMode("tasks", GroupViewMode.Split, 900);
        Settle();

        Assert.Equal("Draft task", main.Tasks.NewTaskText);
        Assert.True(Frame(window, "tasks").IsSplitShown);
        window.Close();
    }
}
