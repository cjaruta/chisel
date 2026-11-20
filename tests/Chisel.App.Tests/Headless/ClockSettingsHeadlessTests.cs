using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
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

    [AvaloniaFact]
    public void TheClockPage_ShowsThePresetPickerAndTheOptions()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "clock");
        Dispatcher.UIThread.RunJobs();

        var names = window.GetVisualDescendants().OfType<Control>().Select(c => c.Name).ToHashSet();
        foreach (var expected in new[] { "ClockPreset", "ClockAlignment", "ClockShape", "ClockMarkers", "ClockNumerals", "ClockHands", "ClockHourThickness", "ClockPalette", "ClockShowSeconds" })
        {
            Assert.Contains(expected, names);
        }
        UiSnapshot.Render(window, "clock-settings");
    }

    [AvaloniaFact]
    public void TheSlotsSection_ConfiguresTheWidgetLive_AndTheWidgetHasNoControlsForIt()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "clock");
        Dispatcher.UIThread.RunJobs();

        var all = window.GetVisualDescendants().OfType<Control>().ToList();
        Assert.Contains(all, c => c.Name == "ClockSlotCount");
        var list = all.OfType<ItemsControl>().Single(c => c.Name == "ClockSlotList");
        Assert.Equal(3, list.ItemCount);
        Assert.Equal(2, window.GetVisualDescendants().OfType<ClockSlotView>().Count());

        main.ClockSlotsSettings.SlotCountIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Single(window.GetVisualDescendants().OfType<ClockSlotView>());

        main.ClockSlotsSettings.SlotCountIndex = 2;
        main.ClockSlotsSettings.Slots[2].Scrolling = false;
        main.ClockSlotsSettings.Slots[2].PinnedIndex = (int)ClockTool.Stopwatch;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, window.GetVisualDescendants().OfType<ClockSlotView>().Count());
        Assert.Contains(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ClockStopwatchPanel" && c.IsEffectivelyVisible);
    }
}
