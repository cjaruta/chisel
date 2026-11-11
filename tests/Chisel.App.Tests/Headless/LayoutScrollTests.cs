using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Chisel.App.Tests.Headless;

public class LayoutScrollTests
{
    [AvaloniaTheory]
    [InlineData("SidePanel")]
    [InlineData("ScheduleList")]
    public void Panel_SitsInAScrollViewerThatFitsInsideTheWindow(string panelName)
    {
        var window = UiSnapshot.ShowMainWindow(1000, 500);

        var panel = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == panelName);
        var scroller = panel.FindAncestorOfType<ScrollViewer>();

        Assert.NotNull(scroller);
        var bottom = scroller!.TranslatePoint(new Point(0, scroller.Bounds.Height), window)!.Value.Y;
        Assert.True(bottom <= window.ClientSize.Height, $"{panelName} scroller ends at {bottom}, past the window height {window.ClientSize.Height}");
        window.Close();
    }

    [AvaloniaFact]
    public void SidePanel_ScrollsWhenTheWindowIsTooShort()
    {
        var window = UiSnapshot.ShowMainWindow(1000, 500);

        var panel = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "SidePanel");
        var scroller = panel.FindAncestorOfType<ScrollViewer>()!;

        Assert.True(scroller.Extent.Height > scroller.Viewport.Height);
        window.Close();
    }
}
