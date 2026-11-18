using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

public class ClockWidgetHeadlessTests
{
    [AvaloniaFact]
    public void TheClock_IsInTheTray_CanBeAdded_AndDrawsAFaceWithThreeHands()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == WidgetRegistry.Clock);
        Assert.Empty(window.GetVisualDescendants().OfType<ClockFace>());

        main.Layout.AddWidgetFromTray(WidgetRegistry.Clock);
        Dispatcher.UIThread.RunJobs();

        var face = window.GetVisualDescendants().OfType<ClockFace>().Single();
        Assert.Equal(3, face.HandCount);
        Assert.Equal((285d, 180d, 0d), (face.HourAngle, face.MinuteAngle, face.SecondAngle));
        Assert.False(face.IsHitTestVisible);
        main.Layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(window.GetVisualDescendants().OfType<ClockFace>());
        UiSnapshot.Render(window, "clock-widget");
    }
}
