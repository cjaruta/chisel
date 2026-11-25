using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Countdown;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class CountdownWidgetHeadlessTests
{
    // Saturday 2000-01-01 at 09:30 UTC (UiSnapshot.SampleNow).
    private readonly FakeTimeProvider _clock = new(UiSnapshot.SampleNow);

    private MainWindow Open(double weight = 1.5, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock, themeMode: themeMode);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.Countdown, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static CountdownWidgetView View(Window w) => w.GetVisualDescendants().OfType<CountdownWidgetView>().Single();

    private static T Named<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static string[] RowNames(Window w) => w.GetVisualDescendants().OfType<Button>()
        .Where(b => b.Name == "CountdownRow" && b.IsEffectivelyVisible).Select(b => AutomationProperties.GetName(b) ?? "").ToArray();

    private static void Add(Window window, string name, DateOnly date, string time = "", CountdownColor color = CountdownColor.None, string emoji = "")
    {
        var countdown = Main(window).Countdown;
        countdown.OpenAdd();
        countdown.FormName = name;
        countdown.FormDate = date.ToDateTime(TimeOnly.MinValue);
        countdown.FormTime = time;
        countdown.FormColor = color;
        countdown.FormEmoji = emoji;
        Assert.True(countdown.Save(), countdown.FormError);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Control button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private void Seed(Window window)
    {
        Add(window, "Trip", new DateOnly(2000, 1, 13), color: CountdownColor.Blue, emoji: "✈");
        Add(window, "Party", new DateOnly(2000, 1, 4), "18:00", CountdownColor.Orange);
        Add(window, "Launch", new DateOnly(2000, 3, 1), color: CountdownColor.Green);
        Add(window, "Dentist", new DateOnly(1999, 12, 30));
    }

    [AvaloniaFact]
    public void ShowsTheNearestEventLarge_AndTheOthersAsNamedRows()
    {
        var window = Open();
        Seed(window);

        Assert.Equal("Party, 3 days 8 hours remaining", AutomationProperties.GetName(Named<Button>(window, "CountdownHero")));
        // Upcoming by date, then past; the rows that fit are shown and the rest are counted, never clipped.
        var expected = new[] { "Trip, 12 days remaining", "Launch, 60 days remaining", "Dentist, 2 days ago" };
        var rows = RowNames(window);
        Assert.True(rows.Length >= 2, $"only {rows.Length} rows");
        Assert.Equal(expected.Take(rows.Length), rows);
        var more = Named<TextBlock>(window, "CountdownMore");
        Assert.Equal(rows.Length < 3, more.IsVisible);
        if (more.IsVisible)
        {
            Assert.Equal($"+{3 - rows.Length} more", more.Text);
        }
        Assert.NotEqual(CountdownSize.Single, View(window).Layout.Size);
        UiSnapshot.Render(window, "countdown");
        window.Close();
    }

    [AvaloniaFact]
    public void LightTheme_WithProgressBars_Renders()
    {
        var window = Open(weight: 3, themeMode: "light");
        Seed(window);

        Assert.Equal(CountdownSize.Detailed, View(window).Layout.Size);
        Assert.True(Named<Control>(window, "CountdownHeroProgress").IsVisible);
        UiSnapshot.Render(window, "countdown-light");
        window.Close();
    }

    [AvaloniaFact]
    public void ASmallWidget_ShowsOnlyTheNearestEvent()
    {
        var window = Open(weight: 0.4);
        Seed(window);

        Assert.Equal(CountdownSize.Single, View(window).Layout.Size);
        Assert.Empty(RowNames(window));
        Assert.True(Named<Control>(window, "CountdownHero").IsVisible);
        Assert.False(Named<Control>(window, "CountdownAdd").IsVisible);
        Assert.True(View(window).Layout.HeroNumberFont >= CountdownLayout.MinNumberFont);
        UiSnapshot.Render(window, "countdown-small");
        window.Close();
    }

    [AvaloniaFact]
    public void TheInlineForm_AddsAnEvent_AndShowsWhatIsWrong()
    {
        var window = Open();
        Click(Named<Button>(window, "CountdownAdd"));
        Assert.True(Named<Control>(window, "CountdownForm").IsVisible);

        Named<TextBox>(window, "CountdownName").Text = "";
        Click(Named<Button>(window, "CountdownSave"));
        Assert.Equal("Give the event a name.", Named<TextBlock>(window, "CountdownError").Text);

        Named<TextBox>(window, "CountdownName").Text = "Trip";
        Named<CalendarDatePicker>(window, "CountdownDate").SelectedDate = new DateTime(2000, 1, 13);
        Named<CheckBox>(window, "CountdownYearly").IsChecked = true;
        Click(Named<Button>(window, "CountdownSave"));

        Assert.False(Named<Control>(window, "CountdownForm").IsVisible);
        var trip = Assert.Single(Main(window).Countdown.Items);
        Assert.Equal(("Trip", new DateOnly(2000, 1, 13), CountdownRepeat.Yearly), (trip.Entry.Name, trip.Entry.Date, trip.Entry.Repeat));
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingAnEvent_OpensItForEditing_AndDeleteRemovesIt()
    {
        var window = Open();
        Add(window, "Trip", new DateOnly(2000, 1, 13));

        Click(Named<Button>(window, "CountdownHero"));
        Assert.Equal("Trip", Named<TextBox>(window, "CountdownName").Text);
        Click(Named<Button>(window, "CountdownDelete"));

        Assert.True(Main(window).Countdown.IsEmpty);
        Assert.True(Named<TextBlock>(window, "CountdownEmpty").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TheMinuteTimer_RunsOnlyWhileTheWidgetIsOnScreen()
    {
        var window = Open();
        var countdown = Main(window).Countdown;
        Assert.True(countdown.IsWatched);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(countdown.IsWatched);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(countdown.IsWatched);

        Main(window).Layout.Apply(Main(window).Layout.Layout.RemoveWidget(WidgetRegistry.Countdown));
        Dispatcher.UIThread.RunJobs();
        Assert.False(countdown.IsWatched);
        window.Close();
    }
}
