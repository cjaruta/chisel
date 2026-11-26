using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// The slider under Settings > General sets the space between widgets, live: from nearly none up to roomy.
public class WidgetSpacingHeadlessTests
{
    private static MainWindow Show() => UiSnapshot.ShowMainWindow();

    // The gaps between the cards: vertically in a column that stacks several, and between the two columns.
    private static (double Vertical, double Horizontal) Gaps(MainWindow window)
    {
        var frames = window.GetVisualDescendants().OfType<WidgetFrame>().Where(f => f.IsEffectivelyVisible)
            .Select(f => (Top: f.TranslatePoint(default, window)!.Value, f.Bounds.Size)).ToList();
        var left = frames.OrderBy(f => f.Top.X).First();
        var right = frames.Where(f => f.Top.X > left.Top.X + 10).OrderBy(f => f.Top.Y).ToList();
        return (right[1].Top.Y - (right[0].Top.Y + right[0].Size.Height), right[0].Top.X - (left.Top.X + left.Size.Width));
    }

    [AvaloniaFact]
    public void TheSlider_SetsTheGapBetweenWidgets_FromHairlineToRoomy()
    {
        var window = Show();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.SelectedTab = SettingsTab.Appearance;
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var slider = window.GetVisualDescendants().OfType<Slider>().Single(s => s.Name == "WidgetSpacingSlider");
        Assert.Equal((2, 32), (slider.Minimum, slider.Maximum));

        var gaps = new List<(double V, double H)>();
        foreach (var spacing in new[] { 2.0, 16, 32 })
        {
            slider.Value = spacing;
            Dispatcher.UIThread.RunJobs();
            var (v, h) = Gaps(window);
            gaps.Add((v, h));
            Assert.Equal(spacing, v, 1);
            Assert.Equal(spacing, h, 1);
        }
        main.Settings.IsOpen = false;
        foreach (var (spacing, name) in new[] { (2.0, "narrowest"), (32.0, "widest") })
        {
            slider.Value = spacing;
            Dispatcher.UIThread.RunJobs();
            UiSnapshot.Render(window, $"widget-spacing-{name}");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void AHairlineGap_StillHasAHandleThatIsEasyToGrab()
    {
        var window = Show();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.WidgetSpacing = 2;
        main.Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var hit = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("resizeHit") && b.IsEffectivelyVisible);
        Assert.True(hit.Bounds.Height >= 8, "the handle is too thin to grab");
        window.Close();
    }
}
