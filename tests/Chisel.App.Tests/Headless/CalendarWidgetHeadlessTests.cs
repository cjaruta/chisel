using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Calendar;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class CalendarWidgetHeadlessTests
{
    // Wednesday 2026-11-25 at noon, UTC.
    private static readonly DateOnly Today = new(2026, 11, 25);

    private static FakeTimeProvider Clock(int hour = 12) => new(new DateTimeOffset(2026, 11, 25, hour, 0, 0, TimeSpan.Zero));

    // Opens the window with the calendar added to the first column; weight is its share of that column's height.
    private static MainWindow Open(double weight = 1.5, FakeTimeProvider? clock = null, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: clock ?? Clock(), themeMode: themeMode);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.Calendar, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static CalendarWidgetView View(Window w) => w.GetVisualDescendants().OfType<CalendarWidgetView>().Single();

    private static T Named<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static List<Button> DayButtons(Window w) =>
        w.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "CalendarDay").ToList();

    private static Button DayButton(Window w, DateOnly date) =>
        DayButtons(w).Single(b => b.Tag is DateOnly d && d == date);

    private static void Click(Control button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Control view, Key key)
    {
        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ShowsSixWeeks_WithTodayHighlighted_AdjacentDaysMuted_AndNamedCells()
    {
        var window = Open();

        var days = DayButtons(window);
        Assert.Equal(42, days.Count);
        Assert.All(days, b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b))));
        Assert.Single(days, b => b.Classes.Contains("today"));
        Assert.Contains("today", DayButton(window, Today).Classes);
        Assert.All(days.Where(b => ((DateOnly)b.Tag!).Month != 11), b => Assert.Contains("muted", b.Classes));
        Assert.Equal(Main(window).Calendar.Title, Named<TextBlock>(window, "CalendarTitleText").Text);
        foreach (var name in new[] { "CalendarPrevious", "CalendarNext", "CalendarToday", "CalendarTitle" })
        {
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(Named<Button>(window, name))), name);
        }
        UiSnapshot.Render(window, "calendar-widget");
        window.Close();
    }

    [AvaloniaFact]
    public void Light_Renders_WithWeekNumbers()
    {
        var window = Open(themeMode: "light");
        Main(window).Calendar.Settings.ShowWeekNumbers = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(View(window).Layout.ShowWeekNumbers);
        UiSnapshot.Render(window, "calendar-widget-light");
        window.Close();
    }

    [AvaloniaFact]
    public void Small_IsCompact_WithoutWeekdayNamesOrButtons_AndGrowsDetail()
    {
        var window = Open(weight: 0.45);
        Assert.Equal(CalendarSize.Compact, View(window).Layout.Size);
        Assert.False(Named<Control>(window, "CalendarWeekdays").IsVisible);
        Assert.False(Named<Button>(window, "CalendarPrevious").IsVisible);
        Assert.False(Named<Button>(window, "CalendarToday").IsVisible);
        Assert.Equal(Main(window).Calendar.MonthName, Named<TextBlock>(window, "CalendarTitleText").Text);
        Assert.Equal(42, DayButtons(window).Count(b => b.IsEffectivelyVisible));
        UiSnapshot.Render(window, "calendar-widget-compact");

        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.RemoveWidget(WidgetRegistry.Calendar).AddWidget(WidgetRegistry.Calendar, 0, 1, 3));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(CalendarSize.Full, View(window).Layout.Size);
        Assert.True(Named<Control>(window, "CalendarWeekdays").IsVisible);
        Assert.True(Named<Button>(window, "CalendarToday").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ButtonsAndClicks_NavigateAndSelect()
    {
        var window = Open();
        var calendar = Main(window).Calendar;

        Click(Named<Button>(window, "CalendarNext"));
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.Month);
        Click(DayButton(window, new DateOnly(2026, 12, 10)));
        Assert.Equal(new DateOnly(2026, 12, 10), calendar.Selected);
        Assert.Contains("selected", DayButton(window, new DateOnly(2026, 12, 10)).Classes);

        Click(Named<Button>(window, "CalendarTitle"));
        Assert.Equal(new DateOnly(2026, 11, 1), calendar.Month);
        Click(Named<Button>(window, "CalendarPrevious"));
        Assert.Equal(new DateOnly(2026, 10, 1), calendar.Month);
        Click(Named<Button>(window, "CalendarToday"));
        Assert.Equal(Today, calendar.Selected);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_MovesTheSelection_AndMarksADay()
    {
        var window = Open();
        var view = View(window);
        var calendar = Main(window).Calendar;

        Press(view, Key.Right);
        Press(view, Key.Down);
        Assert.Equal(Today.AddDays(8), calendar.Selected);
        Press(view, Key.PageDown);
        Assert.Equal(new DateOnly(2027, 1, 3), calendar.Selected);
        Press(view, Key.Home);
        Assert.Equal(Today, calendar.Selected);
        Press(view, Key.M);
        Assert.True(calendar.Settings.IsMarked(Today));
        Assert.Contains("marked", DayButton(window, Today).Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void AtMidnight_TheTodayHighlightMoves()
    {
        var clock = Clock(hour: 23);
        var window = Open(clock: clock);

        clock.Advance(TimeSpan.FromHours(1));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("today", DayButton(window, Today.AddDays(1)).Classes);
        Assert.DoesNotContain("today", DayButton(window, Today).Classes);
        window.Close();
    }
}
