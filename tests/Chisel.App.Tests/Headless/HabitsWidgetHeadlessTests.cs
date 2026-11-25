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
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class HabitsWidgetHeadlessTests
{
    private readonly FakeTimeProvider _clock = new(UiSnapshot.SampleNow);
    private static readonly DateOnly Today = DateOnly.FromDateTime(UiSnapshot.SampleNow.Date);

    private MainWindow Open(double weight = 3, string? themeMode = null)
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock, themeMode: themeMode);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.Habits, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static HabitsWidgetView View(Window w) => w.GetVisualDescendants().OfType<HabitsWidgetView>().Single();

    private static T Named<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static List<CheckBox> Checks(Window w) => w.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Name == "HabitCheck" && c.IsVisible).ToList();

    private static void Click(Control button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Seed(HabitsViewModel habits)
    {
        habits.Add("Read", "📚", CountdownColor.None, HabitDays.Every);
        habits.Add("Walk", null, CountdownColor.Green, HabitDays.Every);
        habits.Add("Gym", null, CountdownColor.Blue, HabitDays.Monday | HabitDays.Wednesday | HabitDays.Friday);
        var read = habits.Items[0].Id;
        for (var i = 1; i <= 12; i++)
        {
            if (i != 4)
            {
                habits.Toggle(read, Today.AddDays(-i));
            }
        }
        habits.Toggle(read, Today);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ALargeWidget_ShowsTheSummaryTheChecksTheStreaksAndTheGrid()
    {
        var window = Open();
        Seed(Main(window).Habits);

        Assert.Equal(HabitDetail.Grid, View(window).Layout.Detail);
        Assert.Equal("1 of 2 done today", Named<TextBlock>(window, "HabitSummary").Text);
        var checks = Checks(window);
        Assert.Equal(3, checks.Count);
        Assert.Equal("Read, done, 4 day streak", AutomationProperties.GetName(checks[0]));
        Assert.True(checks[0].IsChecked);
        Assert.Equal("4 day streak · best 8", window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HabitStreak").Text);
        Assert.Equal(35, window.GetVisualDescendants().OfType<HabitHistory>().First().Days);
        UiSnapshot.Render(window, "habits");
        window.Close();
    }

    [AvaloniaFact]
    public void SpaceToggles_AndArrowKeysMoveBetweenHabits_KeepingFocus()
    {
        var window = Open();
        Seed(Main(window).Habits);
        var checks = Checks(window);
        checks[0].Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(checks[1], window.FocusManager!.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Main(window).Habits.Items[1].DoneToday);
        Assert.Same(checks[1], window.FocusManager!.GetFocusedElement());
        Assert.Equal("All 2 done today", Named<TextBlock>(window, "HabitSummary").Text);

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(checks[0], window.FocusManager!.GetFocusedElement());
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingAPastDay_TogglesIt_ButNotTheFuture()
    {
        var window = Open();
        Seed(Main(window).Habits);
        var history = window.GetVisualDescendants().OfType<HabitHistory>().First();
        var readId = Main(window).Habits.Items[0].Id;
        var missed = Today.AddDays(-4);

        Assert.Equal(missed, history.DayAt(history.CellCenter(missed)));
        var at = history.TranslatePoint(history.CellCenter(missed), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Main(window).Habits.Items.Single(i => i.Id == readId).Habit.IsDone(missed));
        Assert.Equal(13, Main(window).Habits.Items[0].Streak.Current);
        Assert.Null(history.DayAt(new Point(history.Bounds.Width + 5, history.Bounds.Height - 1)));
        Assert.False(Main(window).Habits.Toggle(readId, Today.AddDays(1)));
        window.Close();
    }

    [AvaloniaFact]
    public void AllDone_ShowsTheQuietCheck_InTheLightTheme()
    {
        var window = Open(themeMode: "light");
        var habits = Main(window).Habits;
        Seed(habits);
        habits.Toggle(habits.Items[1].Id);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<HabitRing>(window, "HabitSummaryRing").AllDone);
        Assert.Equal("All 2 done today", Named<TextBlock>(window, "HabitSummary").Text);
        UiSnapshot.Render(window, "habits-light");
        window.Close();
    }

    [AvaloniaFact]
    public void TheAddForm_AddsAHabit_AndShowsErrors()
    {
        var window = Open();
        Main(window).Habits.Add("Read", null, CountdownColor.None, HabitDays.Every);
        Dispatcher.UIThread.RunJobs();

        Click(Named<Button>(window, "HabitAdd"));
        Assert.True(Named<Control>(window, "HabitForm").IsVisible);
        Named<TextBox>(window, "HabitName").Text = "read";
        Click(Named<Button>(window, "HabitSave"));
        Assert.Equal("You already have a habit with that name.", Named<TextBlock>(window, "HabitError").Text);

        Named<TextBox>(window, "HabitName").Text = "Stretch";
        Click(Named<Button>(window, "HabitSave"));
        Assert.False(Named<Control>(window, "HabitForm").IsVisible);
        Assert.Equal(new[] { "Read", "Stretch" }, Main(window).Habits.Items.Select(i => i.Name));
        Assert.Equal(2, Checks(window).Count);
        window.Close();
    }

    [AvaloniaFact]
    public void ASmallWidget_ShowsOnlyTheRingAndCount_AndAMediumOneTheList()
    {
        var main = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock);
        Seed(Main(main).Habits);
        foreach (var (w, h, detail) in new[] { (150.0, 110.0, HabitDetail.Summary), (260.0, 220.0, HabitDetail.List) })
        {
            var view = new HabitsWidgetView { DataContext = Main(main) };
            var window = new Window { Width = w, Height = h, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(detail, view.Layout.Detail);
            var checks = window.GetVisualDescendants().OfType<CheckBox>().Count(c => c.IsVisible);
            Assert.Equal(detail == HabitDetail.Summary ? 0 : 3, checks);
            Assert.Equal("1/2", window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HabitCount").Text);
            window.Close();
        }
        main.Close();
    }

    [AvaloniaFact]
    public void TheRegistry_ListsTheHabitTracker()
    {
        var definition = WidgetRegistry.Get(WidgetRegistry.Habits)!;
        Assert.Equal(("Habits", typeof(HabitsWidgetView)), (definition.Title, definition.ViewType));
        Assert.Contains(WidgetRegistry.Habits, WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }
}
