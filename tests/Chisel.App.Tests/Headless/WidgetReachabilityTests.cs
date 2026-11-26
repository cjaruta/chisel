using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.Headless;

// Every widget, at its smallest and at larger sizes, keeps each of its buttons and inputs usable: on screen and at least
// half visible, or inside a scroll area that can scroll to it. (A stopwatch whose Reset is clipped away fails this.)
public class WidgetReachabilityTests(ITestOutputHelper output)
{
    // Target card sizes in pixels; "min" uses the widget's own minimum width and the shortest card height.
    public static TheoryData<string> Sizes => new() { "min", "short-wide", "low", "small", "medium", "tall-narrow", "large" };

    private const double ShortestCard = WidgetRegistry.MinimumHeight;

    private static (double Width, double Height) Target(string size, WidgetDefinition widget) => size switch
    {
        "min" => (widget.MinWidth, ShortestCard),
        "short-wide" => (560, ShortestCard),
        "low" => (420, 160),
        "small" => (Math.Max(widget.MinWidth, 260), 200),
        "medium" => (360, 280),
        "tall-narrow" => (widget.MinWidth, double.PositiveInfinity),
        _ => (double.PositiveInfinity, double.PositiveInfinity),
    };

    // The widget alone in the first column at the target size; other widgets fill the rest so the sizes are real layout sizes.
    private static WidgetLayout LayoutFor(string id, double width, double height)
    {
        var fillers = new[] { "scripture", "tasks", "notes" }.Where(f => f != id).ToArray();
        var first = new List<WidgetPlacement> { new(id, double.IsInfinity(height) ? 1 : height) };
        if (!double.IsInfinity(height))
        {
            first.Add(new WidgetPlacement(fillers[0], 620 - height));
        }
        var columns = new List<LayoutColumn> { new(double.IsInfinity(width) ? 1 : width, first) };
        if (!double.IsInfinity(width))
        {
            columns.Add(new LayoutColumn(1160 - width, new[] { new WidgetPlacement(fillers[1], 1) }));
        }
        var shown = columns.SelectMany(c => c.Widgets).SelectMany(w => w.MemberIds).ToHashSet();
        return new WidgetLayout(columns, WidgetRegistry.All.Select(w => w.Id).Where(w => !shown.Contains(w)));
    }

    private static bool IsInteractive(Control c) =>
        c is Button or TextBox or ComboBox or Slider or CalendarDatePicker or AutoCompleteBox or NumericUpDown or VolumeSlider
        && !c.GetVisualAncestors().Any(a => a is ScrollBar or TextBox or ComboBox or CalendarDatePicker or AutoCompleteBox or NumericUpDown);

    // Where a visual is drawn in the window, render transforms (scaling) included.
    private static Rect OnScreen(Visual visual, Window window) =>
        new Rect(visual.Bounds.Size).TransformToAABB(visual.TransformToVisual(window)!.Value);

    // The part of the control the user can see, after every clipping ancestor up to the window.
    private static Rect Visible(Control control, Window window)
    {
        var rect = OnScreen(control, window);
        foreach (var ancestor in control.GetVisualAncestors().OfType<Visual>())
        {
            if (ancestor.ClipToBounds || ancestor is Avalonia.Controls.Presenters.ScrollContentPresenter || ancestor is Window)
            {
                var clip = OnScreen(ancestor, window);
                rect = rect.Intersect(clip);
            }
        }
        return rect;
    }

    private static bool Scrollable(Control control) =>
        control.GetVisualAncestors().OfType<ScrollViewer>().Any(s =>
            s.Extent.Height > s.Viewport.Height + 1 || s.Extent.Width > s.Viewport.Width + 1);

    private List<string> Problems(Window window, WidgetFrame frame, string where)
    {
        var problems = new List<string>();
        foreach (var control in frame.GetVisualDescendants().OfType<Control>().Where(IsInteractive)
                     .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0)
                     .Where(c => !c.GetVisualAncestors().Contains(frame.EditBar)))
        {
            if (Scrollable(control))
            {
                continue;
            }
            var seen = Visible(control, window);
            var full = OnScreen(control, window);
            var label = control.Name ?? (control as ContentControl)?.Content as string ?? control.GetType().Name;
            if (seen.Width * seen.Height < full.Width * full.Height * 0.5)
            {
                problems.Add($"{where}: {label} {(int)full.Width}x{(int)full.Height} only {(int)seen.Width}x{(int)seen.Height} visible");
            }
            else if (full.Height < 12 || full.Width < 12)
            {
                problems.Add($"{where}: {label} shrunk to {full.Width:0}x{full.Height:0}, too small to use");
            }
        }
        return problems;
    }

    private static ClockSlotsConfig Pinned(params ClockTool[] tools) => new(tools.Select(ClockSlotConfig.Pinned).ToList());

    // Each widget's views worth checking: its default view, plus the clock's tools.
    private static IEnumerable<(string Name, Action<MainViewModel> Set)> Scenes(string id) => id switch
    {
        WidgetRegistry.Clock => new (string, Action<MainViewModel>)[]
        {
            ("clock", _ => { }),
            ("timer", m => m.ClockSlots.Slots[^1].Select(ClockTool.Timer)),
            ("stopwatch", m => m.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch)),
            ("stopwatch-running", m => { m.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch); m.ClockTools.StopwatchStartPauseCommand.Execute(null); }),
            ("stopwatch-analog", m => { m.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog; m.ClockSlots.Slots[^1].Select(ClockTool.Stopwatch); }),
            ("pinned-clock-stopwatch-alarm", m => m.ClockTools.SetSlots(Pinned(ClockTool.Clock, ClockTool.Stopwatch, ClockTool.Alarm))),
            ("pinned-all-four", m => m.ClockTools.SetSlots(Pinned(ClockTool.Clock, ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm))),
            ("pinned-analog-stopwatch-timer", m =>
            {
                m.ClockSettings.StopwatchIndex = (int)StopwatchDisplay.Analog;
                m.ClockTools.SetSlots(Pinned(ClockTool.Stopwatch, ClockTool.Timer));
            }),
            ("alarm", m => m.ClockSlots.Slots[^1].Select(ClockTool.Alarm)),
        },
        WidgetRegistry.Calculator => new (string, Action<MainViewModel>)[]
        {
            ("default", _ => { }),
            ("advanced", m => m.CalculatorSettings.Advanced = true),
        },
        WidgetRegistry.Countdown => new (string, Action<MainViewModel>)[]
        {
            ("default", _ => { }),
            ("form", m => m.Countdown.OpenAdd()),
        },
        WidgetRegistry.Habits => new (string, Action<MainViewModel>)[]
        {
            ("default", _ => { }),
            ("form", m => m.Habits.OpenAdd()),
            ("items", m =>
            {
                foreach (var name in new[] { "Read", "Walk", "Water", "Stretch" })
                {
                    m.Habits.Add(name, "", Chisel.Core.Countdown.CountdownColor.None, Chisel.Core.Habits.HabitDays.Every);
                }
            }),
        },
        WidgetRegistry.FocusTimer => new (string, Action<MainViewModel>)[]
        {
            ("default", _ => { }),
            ("running", m => m.FocusTimer.Start()),
        },
        WidgetRegistry.ClipboardHistory => new (string, Action<MainViewModel>)[]
        {
            ("default", _ => { }),
            ("recording", m => m.ClipboardHistory.Start()),
        },
        _ => new (string, Action<MainViewModel>)[] { ("default", _ => { }) },
    };

    private List<string> Check(WidgetDefinition widget, string scene, Action<MainViewModel> set, string size)
    {
        var window = UiSnapshot.ShowMainWindow(media: new FakeMediaService { Current = FakeMediaService.Sample() },
            volume: new FakeVolumeService());
        var main = (MainViewModel)window.DataContext!;
        var (width, height) = Target(size, widget);
        main.Layout.Apply(LayoutFor(widget.Id, width, height));
        Dispatcher.UIThread.RunJobs();
        set(main);
        Dispatcher.UIThread.RunJobs();
        var frame = window.GetVisualDescendants().OfType<WidgetFrame>().Single(f => f.WidgetId == widget.Id);
        var found = Problems(window, frame, $"{widget.Id}/{scene} at {(int)frame.Bounds.Width}x{(int)frame.Bounds.Height}");
        if (found.Count > 0 || (Environment.GetEnvironmentVariable("CHISEL_RENDER_WIDGETS")?.Split(',').Contains(widget.Id) ?? false))
        {
            UiSnapshot.Render(window, $"reach-{size}-{widget.Id}-{scene}");
        }
        window.Close();
        return found;
    }

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public void EveryWidget_KeepsItsControlsReachable(string size)
    {
        var problems = new List<string>();
        // Tool changes take effect at once, so the check sees the tool that was picked, not the slide between two.
        var slide = SlideHost.SlideDuration;
        SlideHost.SlideDuration = TimeSpan.Zero;
        try
        {
            foreach (var widget in WidgetRegistry.All)
            {
                foreach (var (scene, set) in Scenes(widget.Id))
                {
                    problems.AddRange(Check(widget, scene, set, size));
                }
            }
        }
        finally
        {
            SlideHost.SlideDuration = slide;
        }
        output.WriteLine(string.Join("\n", problems));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
