using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Tests.Headless;

// The Profiles button in the edit toolbar: three slots, each with Save Profile, Load and Delete.
public class LayoutProfileHeadlessTests
{
    [AvaloniaFact]
    public void TheEditToolbar_OffersThreeProfileSlots_AndSavingOneMarksItSaved()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ProfilesButton");
        Assert.True(button.IsEffectivelyVisible);
        button.Flyout!.ShowAt(button);
        Dispatcher.UIThread.RunJobs();

        var flyout = (Flyout)button.Flyout!;
        var list = (ItemsControl)((StackPanel)flyout.Content!).Children.OfType<ItemsControl>().Single();
        Assert.Equal(3, list.Items.Count);

        main.Layout.Profiles[0].SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(main.Layout.Profiles[0].IsSaved);
        Assert.Equal("Saved", main.Layout.Profiles[0].Status);
        Assert.Equal("Empty", main.Layout.Profiles[1].Status);

        UiSnapshot.Render(window, "layout-profiles-toolbar");
        flyout.Hide();
        window.Close();
    }
}
