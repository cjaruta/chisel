using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

// The top bar at any window width, and the Settings dialog's size and pages.
public class TopBarAndSettingsHeadlessTests
{
    private static Control Named(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    private static Rect InWindow(MainWindow w, Control c) =>
        new(c.TranslatePoint(new Point(0, 0), w)!.Value, c.Bounds.Size);

    private static bool Inside(Rect outer, Rect inner) =>
        inner.X >= outer.X - 0.5 && inner.Y >= outer.Y - 0.5 && inner.Right <= outer.Right + 0.5 && inner.Bottom <= outer.Bottom + 0.5;

    // In edit mode the toolbar never covers the clock, the layout and settings buttons or the widgets, and nothing is cut
    // off: it sits between the clock and the buttons when there is room, and on its own line below them when there isn't.
    [AvaloniaTheory]
    [InlineData(1200)]
    [InlineData(860)]
    [InlineData(640)]
    [InlineData(480)]
    public void EditToolbar_FitsTheWindow_WithoutCoveringAnything(double width)
    {
        var window = UiSnapshot.ShowMainWindow(width: width);
        ((MainViewModel)window.DataContext!).Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, $"edit-topbar-{width}");

        var page = new Rect(window.Bounds.Size);
        var toolbar = InWindow(window, Named(window, "LayoutToolbar"));
        Assert.True(Inside(page, toolbar), $"toolbar {toolbar} outside {page}");
        foreach (var name in new[] { "ClockPanel", "CustomizeLayoutButton", "SettingsButton" })
        {
            Assert.False(toolbar.Intersects(InWindow(window, Named(window, name))), $"toolbar covers {name}");
        }
        Assert.True(toolbar.Bottom <= InWindow(window, Named(window, "LayoutHost")).Y + 0.5, "toolbar covers the widgets");
        var buttons = Named(window, "LayoutToolbarItems").GetVisualChildren().OfType<Button>().ToList();
        foreach (var button in buttons)
        {
            Assert.True(Inside(toolbar, InWindow(window, button)), $"{button.Name} sticks out of the toolbar");
        }
        // One row: no button left hanging on a line of its own.
        Assert.Single(buttons.Select(b => Math.Round(InWindow(window, b).Y)).Distinct());

        // The library opens below the toolbar, never under it.
        ((MainViewModel)window.DataContext!).Library.ToggleCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(InWindow(window, Named(window, "LibraryPanel")).Y >= toolbar.Bottom - 0.5 || toolbar.Right <= InWindow(window, Named(window, "LibraryPanel")).X,
            "library under the toolbar");
        window.Close();
    }

    [AvaloniaFact]
    public void TheClock_GetsSmallerOnNarrowWindows()
    {
        var wide = UiSnapshot.ShowMainWindow(width: 1200);
        var narrow = UiSnapshot.ShowMainWindow(width: 560);

        Assert.True(((TextBlock)Named(narrow, "ClockTime")).FontSize < ((TextBlock)Named(wide, "ClockTime")).FontSize);
        Assert.True(InWindow(narrow, Named(narrow, "ClockPanel")).Right < InWindow(narrow, Named(narrow, "CustomizeLayoutButton")).X);
        wide.Close();
        narrow.Close();
    }

    // Larger than before on a normal window, and never bigger than the window.
    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1600, 1000)]
    [InlineData(760, 560)]
    public void SettingsDialog_GrowsWithTheWindow_AndAlwaysFits(double width, double height)
    {
        var window = UiSnapshot.ShowMainWindow(width: width, height: height);
        ((MainViewModel)window.DataContext!).Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var dialog = InWindow(window, Named(window, "SettingsDialog"));
        Assert.True(Inside(new Rect(window.Bounds.Size).Deflate(8), dialog), $"dialog {dialog}");
        if (width >= 1200)
        {
            Assert.True(dialog.Width > 720 && dialog.Height > 520, $"dialog {dialog}");
        }
        window.Close();
    }

    // Colors and spacing have their own Appearance tab; General keeps the clock and system options.
    [AvaloniaFact]
    public void Appearance_HoldsModeAccentAndSpacing_GeneralHoldsClockAndSystem()
    {
        var window = UiSnapshot.ShowMainWindow();
        var settings = ((MainViewModel)window.DataContext!).Settings;
        settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "settings-general");

        var general = Named(window, "GeneralPage");
        Assert.Contains(general.GetVisualDescendants(), c => c is Control { Name: "SystemSection" });
        Assert.DoesNotContain(general.GetVisualDescendants(), c => c is Control { Name: "ThemeModeList" or "AccentSwatches" or "WidgetSpacingSlider" });

        settings.SelectTabCommand.Execute(SettingsTab.Appearance);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "settings-appearance");

        var appearance = Named(window, "AppearancePage");
        Assert.True(appearance.IsVisible);
        foreach (var name in new[] { "ThemeModeList", "AccentSwatches", "AccentPicker", "WidgetSpacingSlider" })
        {
            Assert.Contains(appearance.GetVisualDescendants(), c => c is Control control && control.Name == name);
        }
        // Every mode is visible without scrolling the page.
        var modes = InWindow(window, Named(window, "ThemeModeList"));
        Assert.True(Inside(InWindow(window, appearance), modes), $"modes {modes} run past the page");
        window.Close();
    }
}
