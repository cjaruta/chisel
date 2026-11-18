using Avalonia;
using Avalonia.Animation;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// Pacing of the drop glow, home slot fade and edit-to-normal settle, and the shared remove badge placement.
public class TactilePolishHeadlessTests
{
    private static List<WidgetFrame> Frames(Avalonia.Controls.Window window) =>
        window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).ToList();

    private static TimeSpan DurationOf(WidgetFrame frame, Avalonia.AvaloniaProperty property) =>
        (TimeSpan)frame.Transitions!.Cast<dynamic>().Single(t => (object)t.Property == property).Duration;

    private static void Click(Avalonia.Controls.Window window, Control control)
    {
        var p = control.TranslatePoint(new Avalonia.Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(p);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Transitions_UseTheRefinedDurations()
    {
        var window = UiSnapshot.ShowMainWindow();
        var frame = Frames(window).First();

        Assert.Equal(TimeSpan.FromMilliseconds(220), DurationOf(frame, Avalonia.Controls.Border.BackgroundProperty));
        Assert.Equal(TimeSpan.FromMilliseconds(220), DurationOf(frame, Avalonia.Visual.RenderTransformProperty));
        // Leaving edit mode: the border settles over 250-300 ms.
        var settle = DurationOf(frame, Avalonia.Controls.Border.BorderBrushProperty);
        Assert.InRange(settle.TotalMilliseconds, 250, 300);
        Assert.True(LayoutHost.SnapHold.TotalMilliseconds >= 80);
        Assert.True(LayoutHost.SnapFade.TotalMilliseconds >= 220);
    }

    [AvaloniaFact]
    public void RemoveBadges_ShareTheSameMarginOnEveryWidget()
    {
        var window = UiSnapshot.ShowMainWindow();
        var frames = Frames(window);

        Assert.True(frames.Count >= 4);
        Assert.All(frames, f => Assert.Equal(new Avalonia.Thickness(8, 8, 0, 0), f.RemoveButton.Margin));
    }

    [AvaloniaFact]
    public void LeavingEditMode_RendersTheNormalState()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CustomizeLayoutButton"));
        var toolbar = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "LayoutToolbar");
        Click(window, toolbar.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Done"));

        Assert.DoesNotContain(Frames(window), f => f.Classes.Contains("editing"));
        UiSnapshot.Render(window, "after-edit-mode");
    }
}
