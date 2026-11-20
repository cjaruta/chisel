using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

public class ClockSettingsHeadlessTests
{
    [AvaloniaFact]
    public void TheClockWidget_FollowsTheClockSettings()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        var face = window.GetVisualDescendants().OfType<ClockFace>().Single();
        Assert.Equal(ClockAlignment.Center, face.Style.Alignment);

        main.ClockSettings.PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == "modern");
        // Alignment moves the face when its slot is wider than the face: a clock-only widget.
        main.ClockTools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]));
        Dispatcher.UIThread.RunJobs();
        face = window.GetVisualDescendants().OfType<ClockFace>().Single();
        main.ClockSettings.AlignmentIndex = (int)ClockAlignment.Right;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(NumeralStyle.Four, face.Style.Numerals);
        Assert.Equal(ClockAlignment.Right, face.Style.Alignment);
        Assert.True(face.FaceCenter.X > face.Bounds.Width / 2);
    }

    private static (MainWindow Window, MainViewModel Main) OpenClockPage()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "clock");
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static List<Control> All(MainWindow w) => w.GetVisualDescendants().OfType<Control>().ToList();

    private static T Named<T>(MainWindow w, string name) where T : Control => All(w).OfType<T>().Single(c => c.Name == name);

    private static bool Shown(MainWindow w, string name) => All(w).Where(c => c.Name == name).Any(c => c.IsEffectivelyVisible);

    private static void Press(MainWindow w, string name)
    {
        var button = Named<Button>(w, name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static readonly (ClockSettingsTab Tab, string Button, string Panel)[] Tabs =
    [
        (ClockSettingsTab.Layout, "ClockTabLayout", "ClockLayoutTab"),
        (ClockSettingsTab.Face, "ClockTabFace", "ClockFaceTab"),
        (ClockSettingsTab.Tools, "ClockTabTools", "ClockToolsTab"),
        (ClockSettingsTab.Behavior, "ClockTabBehavior", "ClockBehaviorTab"),
    ];

    [AvaloniaFact]
    public void ThePage_KeepsALivePreviewPinnedAtTheTop_AboveFourTabs()
    {
        var (window, main) = OpenClockPage();

        var preview = Named<ClockFace>(window, "ClockPreview");
        Assert.True(preview.IsEffectivelyVisible);
        Assert.Equal(main.ClockSettings.PreviewStyle, preview.Style);
        main.ClockSettings.AlignmentIndex = (int)ClockAlignment.Right;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ClockAlignment.Center, preview.Style.Alignment); // the preview stays centered
        foreach (var (_, button, _) in Tabs)
        {
            Assert.True(Shown(window, button));
        }

        main.ClockSettings.PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == "modern");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(NumeralStyle.Four, preview.Style.Numerals); // the preview follows every change

        var top = preview.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        foreach (var (tab, _, _) in Tabs)
        {
            main.ClockSettings.SelectTabCommand.Execute(tab);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(top, preview.TranslatePoint(new Point(0, 0), window)!.Value.Y); // it never moves, whichever tab
        }
    }

    [AvaloniaFact]
    public void TheTabs_OpenOnLayout_AndEachShowsOnlyItsOwnPanel()
    {
        var (window, main) = OpenClockPage();
        Assert.Equal(ClockSettingsTab.Layout, main.ClockSettings.Tab);

        foreach (var (tab, button, panel) in Tabs)
        {
            Press(window, button);
            Assert.Equal(tab, main.ClockSettings.Tab);
            foreach (var (_, _, other) in Tabs)
            {
                Assert.Equal(other == panel, Shown(window, other));
            }
        }
    }

    [AvaloniaFact]
    public void Layout_HasTheSegmentedAlignmentAndSlotCount_WhichSaveAndFollowTheWidget()
    {
        var (window, main) = OpenClockPage();
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();

        Press(window, "ClockAlignRight");
        Assert.Equal(ClockAlignment.Right, main.ClockSettings.Style.Alignment);
        Assert.True(Named<Button>(window, "ClockAlignRight").Classes.Contains("selected"));
        Assert.False(Named<Button>(window, "ClockAlignCenter").Classes.Contains("selected"));

        Press(window, "ClockSlotCount3");
        Assert.Equal(3, main.ClockTools.Slots.Slots.Count);
        Assert.Equal(3, window.GetVisualDescendants().OfType<ClockSlotView>().Count());
        Press(window, "ClockSlotCount1");
        Assert.Single(main.ClockTools.Slots.Slots);
        Assert.True(Named<Button>(window, "ClockSlotCount1").Classes.Contains("selected"));
        UiSnapshot.Render(window, "clock-settings-layout");
    }

    [AvaloniaFact]
    public void Face_ShowsAGalleryOfPresetThumbnailsFirst_AndClickingOneAppliesIt()
    {
        var (window, main) = OpenClockPage();
        Press(window, "ClockTabFace");

        foreach (var preset in ClockPresets.All)
        {
            Assert.True(Shown(window, "ClockPreset_" + preset.Id));
            Assert.NotNull(Named<Image>(window, "ClockPresetThumb_" + preset.Id).Source);
        }
        var gallery = Named<WrapPanel>(window, "ClockPresetGallery");
        var firstCustomize = Named<Expander>(window, "ClockCustomize_Dial");
        Assert.True(gallery.TranslatePoint(new Point(0, 0), window)!.Value.Y < firstCustomize.TranslatePoint(new Point(0, 0), window)!.Value.Y); // thumbnails first

        Press(window, "ClockPreset_roman");
        Assert.Equal(NumeralStyle.Roman, main.ClockSettings.Style.Numerals);
        Assert.True(Named<Button>(window, "ClockPreset_roman").Classes.Contains("selected"));
        Assert.False(Named<Button>(window, "ClockPreset_minimal").Classes.Contains("selected"));
    }

    [AvaloniaFact]
    public void Face_HasCollapsedCustomizeGroups_WithEveryOptionInside_AndReset()
    {
        var (window, main) = OpenClockPage();
        Press(window, "ClockTabFace");

        var groups = new Dictionary<string, string[]>
        {
            ["ClockCustomize_Dial"] = ["ClockShape", "ClockBorder"],
            ["ClockCustomize_Markers"] = ["ClockMarkers", "ClockQuarters"],
            ["ClockCustomize_Numerals"] = ["ClockNumerals"],
            ["ClockCustomize_Hands"] = ["ClockHands", "ClockHourThickness", "ClockMinuteThickness", "ClockSecondThickness"],
            ["ClockCustomize_Colors"] = ["ClockPalette", "ClockDialColor", "ClockMarkerColor", "ClockNumeralColor", "ClockHourColor", "ClockMinuteColor", "ClockSecondColor"],
        };
        foreach (var (group, controls) in groups)
        {
            var expander = Named<Expander>(window, group);
            Assert.False(expander.IsExpanded);
            expander.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            foreach (var name in controls)
            {
                Assert.True(Shown(window, name), $"{name} should be reachable in {group}");
            }
            expander.IsExpanded = false;
        }

        // Reset to preset only does something once the look differs from a preset (the default look is not one).
        Press(window, "ClockPreset_roman");
        Assert.False(Named<Button>(window, "ClockResetPreset").Command!.CanExecute(null));
        Press(window, "ClockPreset_roman");
        main.ClockSettings.HourThickness = 9;
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<Button>(window, "ClockResetPreset").Command!.CanExecute(null));
        Press(window, "ClockResetPreset");
        Assert.Equal("roman", ClockPresets.IdOf(main.ClockSettings.Style));
        UiSnapshot.Render(window, "clock-settings-face");
    }

    [AvaloniaFact]
    public void Tools_HasOneRowPerSlotThatIsOn_WithChipsForAScrollSet()
    {
        var (window, main) = OpenClockPage();
        Press(window, "ClockTabTools");

        var list = Named<ItemsControl>(window, "ClockSlotList");
        Assert.Equal(3, list.ItemCount);
        var rows = list.GetVisualDescendants().OfType<Grid>().Where(g => g.IsEffectivelyVisible && g.ColumnDefinitions.Count == 2 && g.ColumnDefinitions[0].Width.Value == 76).ToList();
        Assert.Equal(2, rows.Count);

        var chips = All(window).OfType<ToggleButton>().Where(t => t.Classes.Contains("chip") && t.IsEffectivelyVisible).ToList();
        Assert.Equal(["Timer", "Stopwatch", "Alarm", "Empty"], chips.Where(c => c.IsChecked == true).Select(c => (string)c.Content!));
        Assert.Equal(5, chips.Count);

        chips.Single(c => (string)c.Content! == "Timer").IsChecked = false;
        Assert.DoesNotContain(ClockTool.Timer, main.ClockTools.Slots.Slots[1].Tools);

        Press(window, "ClockTabLayout");
        Press(window, "ClockSlotCount3");
        Press(window, "ClockTabTools");
        Assert.Equal(3, list.GetVisualDescendants().OfType<Grid>().Count(g => g.IsEffectivelyVisible && g.ColumnDefinitions.Count == 2 && g.ColumnDefinitions[0].Width.Value == 76));
        UiSnapshot.Render(window, "clock-settings-tools");
    }

    [AvaloniaFact]
    public void Behavior_HasTheSecondsHandAndTheTimeFormat_BothSaved()
    {
        var (window, main) = OpenClockPage();
        Press(window, "ClockTabBehavior");

        Named<CheckBox>(window, "ClockShowSeconds").IsChecked = false;
        Assert.False(main.ClockSettings.Style.ShowSeconds);

        Named<RadioButton>(window, "ClockFormat12").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(main.Settings.UseTwelveHour);
        UiSnapshot.Render(window, "clock-settings-behavior");
    }

    // Nothing should need long scrolling at the dialog's normal size: the tabs fit, and the face tab (gallery and collapsed groups) fits.
    [AvaloniaFact]
    public void NoTabNeedsScrolling_AtTheNormalDialogSize_WithTheGroupsCollapsed()
    {
        var (window, _) = OpenClockPage();
        foreach (var (_, button, panel) in Tabs)
        {
            Press(window, button);
            var scroll = Named<ScrollViewer>(window, panel);
            Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + 1, $"{panel} needs scrolling: content {scroll.Extent.Height:0} in {scroll.Viewport.Height:0}");
        }
    }

    [AvaloniaFact]
    public void OpeningAGroup_ScrollsOnlyTheTabBelowThePreview_NeverThePreviewOrTheTabs()
    {
        var (window, main) = OpenClockPage();
        Press(window, "ClockTabFace");
        var preview = Named<ClockFace>(window, "ClockPreview");
        var tabs = Named<Button>(window, "ClockTabFace");
        var before = (preview.TranslatePoint(new Point(0, 0), window)!.Value.Y, tabs.TranslatePoint(new Point(0, 0), window)!.Value.Y);

        Named<Expander>(window, "ClockCustomize_Colors").IsExpanded = true;
        Named<Expander>(window, "ClockCustomize_Hands").IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        var scroll = Named<ScrollViewer>(window, "ClockFaceTab");
        scroll.Offset = new Vector(0, 9999);
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroll.Extent.Height > scroll.Viewport.Height); // opening groups is what scrolls
        Assert.Equal(before, (preview.TranslatePoint(new Point(0, 0), window)!.Value.Y, tabs.TranslatePoint(new Point(0, 0), window)!.Value.Y));
    }
}
