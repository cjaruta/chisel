using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

// Quick links in the top bar (small icon buttons beside the layout and settings buttons, the rest in a menu) and the
// Quick links page in Settings.
public class QuickLinksTopBarHeadlessTests
{
    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<Button> LinkButtons(Window window) =>
        Named<ItemsControl>(window, "QuickLinkBar").GetVisualDescendants().OfType<Button>().ToList();

    private static MainWindow Show(double width = 1200, double height = 760, Action<string>? open = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, openUrl: open);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void OpenSettingsTab(MainWindow window)
    {
        Main(window).Settings.ToggleCommand.Execute(null);
        Main(window).Settings.SelectTabCommand.Execute(SettingsTab.QuickLinks);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheSavedLinks_AreIconButtonsLeftOfTheLayoutAndSettingsButtons_WithTooltipsAndNames()
    {
        var window = Show();
        var buttons = LinkButtons(window);

        Assert.Equal(new[] { "Open Example", "Open Docs" }, buttons.Select(AutomationProperties.GetName).ToArray());
        Assert.All(buttons, b => Assert.NotNull(ToolTip.GetTip(b)));
        var customize = Named<Button>(window, "CustomizeLayoutButton").TranslatePoint(new Point(0, 0), window)!.Value;
        Assert.All(buttons, b => Assert.True(b.TranslatePoint(new Point(0, 0), window)!.Value.X < customize.X));
        Assert.True(Named<Border>(window, "QuickLinksDivider").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingALink_OpensItsTarget()
    {
        var opened = new List<string>();
        var window = Show(open: opened.Add);

        Main(window).QuickLinks.Visible[1].OpenCommand.Execute(null);

        Assert.Equal(new[] { "https://example.com/docs" }, opened.ToArray());
        window.Close();
    }

    [AvaloniaFact]
    public void WithNoLinks_TheBarAndItsDividerTakeNoRoom()
    {
        var window = Show();
        foreach (var link in Main(window).QuickLinks.Links.ToList())
        {
            link.DeleteCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<ItemsControl>(window, "QuickLinkBar").IsVisible);
        Assert.False(Named<Border>(window, "QuickLinksDivider").IsVisible);
        Assert.False(Named<Button>(window, "MoreLinksButton").IsVisible);
        // Hidden controls are not laid out, so no gap is left beside the corner buttons.
        Assert.False(Named<ItemsControl>(window, "QuickLinkBar").IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "QuickLinksDivider").IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TooManyLinks_PutTheRestInAMoreMenu()
    {
        var window = Show();
        var quick = Main(window).QuickLinks;
        for (var i = 0; i < 6; i++)
        {
            quick.NewLinkLabel = $"Site {i}";
            quick.NewLinkTarget = $"https://site{i}.example.com";
            quick.SaveQuickLinkCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(QuickLinksViewModel.DefaultMaxVisible, LinkButtons(window).Count);
        Assert.Equal(8, quick.Links.Count);
        Assert.True(Named<Button>(window, "MoreLinksButton").IsVisible);
        Assert.Equal(2, quick.Overflow.Count);
        Assert.Equal("More quick links", AutomationProperties.GetName(Named<Button>(window, "MoreLinksButton")));
        window.Close();
    }

    [AvaloniaFact]
    public void SettingsPage_AddsALink_ShownInTheListAndTheTopBar()
    {
        var window = Show();
        OpenSettingsTab(window);
        Assert.True(Named<ScrollViewer>(window, "QuickLinksPage").IsVisible);
        Assert.False(Named<ScrollViewer>(window, "GeneralPage").IsVisible);

        Named<TextBox>(window, "LinkNameBox").Text = "Weather";
        Named<TextBox>(window, "LinkTargetBox").Text = "https://weather.example.com";
        Named<Button>(window, "SaveLinkButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Main(window).QuickLinks.Links, l => l.Label == "Weather");
        Assert.Equal(3, LinkButtons(window).Count);
        Assert.Equal("", Named<TextBox>(window, "LinkNameBox").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void SettingsPage_ShowsWhatIsWrongWithAnAddress()
    {
        var window = Show();
        OpenSettingsTab(window);

        Named<TextBox>(window, "LinkNameBox").Text = "Bad";
        Named<TextBox>(window, "LinkTargetBox").Text = "javascript:alert(1)";
        Named<Button>(window, "SaveLinkButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var error = Named<TextBlock>(window, "LinkErrorText");
        Assert.True(error.IsVisible);
        Assert.Contains("http", error.Text);
        Assert.Equal(2, Main(window).QuickLinks.Links.Count);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(640, 560)]
    public void Snapshot_TopBarAndSettingsPage(int width, int height)
    {
        var window = Show(width, height);
        var quick = Main(window).QuickLinks;
        quick.NewLinkLabel = "Calendar";
        quick.NewLinkTarget = "https://calendar.example.com";
        quick.SaveQuickLinkCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, $"quicklinks-topbar-{width}x{height}");

        OpenSettingsTab(window);
        UiSnapshot.Render(window, $"quicklinks-settings-{width}x{height}");
        window.Close();
    }
}
