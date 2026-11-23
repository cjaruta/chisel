using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

// The saved faces on the Clock page's Face tab: a picker with small previews beside the built-in faces, and the buttons to save,
// apply, rename, update, duplicate and delete them.
public class ClockFacesHeadlessTests
{
    private static (MainWindow Window, MainViewModel Main) OpenFaceTab()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "clock");
        main.ClockSettings.SelectTabCommand.Execute(ClockSettingsTab.Face);
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static T Named<T>(MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Press(MainWindow w, string name)
    {
        var button = Named<Button>(w, name);
        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Button> Cards(MainWindow w, string gallery) =>
        Named<WrapPanel>(w, gallery).Children.OfType<Button>().ToList();

    [AvaloniaFact]
    public void TheSavedFaces_SitBesideTheBuiltInOnes_WithPreviews_AndAnEmptyHint()
    {
        var (window, main) = OpenFaceTab();

        Assert.Equal(ClockPresets.All.Count, Cards(window, "ClockPresetGallery").Count);
        Assert.Empty(Cards(window, "ClockCustomGallery"));
        Assert.True(Named<TextBlock>(window, "ClockNoSavedFaces").IsEffectivelyVisible);

        main.ClockFaces.NewName = "Evening";
        Press(window, "ClockFaceSave");

        var card = Assert.Single(Cards(window, "ClockCustomGallery"));
        Assert.Contains(card.GetVisualDescendants().OfType<Image>(), i => i.Source is not null && i.Width > 0);
        Assert.Contains("Evening", card.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.False(Named<TextBlock>(window, "ClockNoSavedFaces").IsEffectivelyVisible);
        Assert.Contains("selected", card.Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingASavedFace_AppliesItsLookAndLayout()
    {
        var (window, main) = OpenFaceTab();
        main.ClockSettings.PresetIndex = 1; // Roman
        main.ClockTools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Timer)]));
        main.ClockFaces.NewName = "Roman pair";
        Press(window, "ClockFaceSave");
        main.ClockSettings.PresetIndex = 5; // Dots
        main.ClockTools.SetSlots(ClockSlotsConfig.Default);
        Dispatcher.UIThread.RunJobs();

        Cards(window, "ClockCustomGallery").Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(NumeralStyle.Roman, main.ClockSettings.Style.Numerals);
        Assert.Equal(ClockTool.Timer, main.ClockTools.Slots.Slots[1].Tool);
        window.Close();
    }

    [AvaloniaFact]
    public void TheManagementButtons_FollowTheSelection_AndABuiltInFaceHasNone()
    {
        var (window, main) = OpenFaceTab();
        Assert.False(Named<Button>(window, "ClockFaceDelete").Command!.CanExecute(null)); // nothing selected

        main.ClockFaces.NewName = "Mine";
        Press(window, "ClockFaceSave");
        Assert.True(Named<Button>(window, "ClockFaceDelete").Command!.CanExecute(null));
        Assert.True(Named<Button>(window, "ClockFaceRename").Command!.CanExecute(null));

        main.ClockFaces.NewName = "Mine 2";
        Press(window, "ClockFaceDuplicate");
        Assert.Equal(2, main.ClockFaces.Custom.Count);

        Cards(window, "ClockPresetGallery")[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<Button>(window, "ClockFaceDelete").Command!.CanExecute(null));
        Assert.False(Named<Button>(window, "ClockFaceUpdate").Command!.CanExecute(null));
        Assert.Equal(2, main.ClockFaces.Custom.Count);
        window.Close();
    }

    [AvaloniaFact]
    public void ABadName_ShowsTheReasonUnderTheButtons()
    {
        var (window, main) = OpenFaceTab();

        main.ClockFaces.NewName = "";
        Press(window, "ClockFaceSave");

        var error = Named<TextBlock>(window, "ClockFaceError");
        Assert.True(error.IsEffectivelyVisible);
        Assert.Contains("name", error.Text, StringComparison.OrdinalIgnoreCase);
        window.Close();
    }
}
