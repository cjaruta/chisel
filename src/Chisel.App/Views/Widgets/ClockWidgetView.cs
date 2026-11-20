using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// The clock widget: the analog face, and over it one panel with a mode strip (Clock / Timer / Stopwatch) that the home screen
// can open. Collapsed it is just the face, a small "Tools" button and, while a timer or stopwatch is running, a chip with its
// time. In edit mode the panel gives way to the tool settings (which tools are on, where the strip goes). While it is on screen
// the view follows MainViewModel.AnalogClock and ClockTools; as a hidden tab of a group it stops (and so stops redrawing)
// until it is shown again. The face ignores the mouse so the widget stays a drag handle in edit mode.
public sealed class ClockWidgetView : WidgetView
{
    private static readonly Thickness BodyPadding = new(0, 0, 0, 12);

    private readonly ClockFace _face = new() { Name = "ClockFace", IsHitTestVisible = false };
    private readonly Grid _root = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
    private readonly StackPanel _strip = new() { Name = "ClockStrip", Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4, Margin = new Thickness(0, 4) };
    private readonly Button _open = new() { Name = "ClockOpenButton", Content = "Tools", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, FontSize = 11, Padding = new Thickness(8, 2) };
    private readonly Button _close = new() { Name = "ClockCloseButton", Content = "Close", FontSize = 12, Padding = new Thickness(8, 2) };
    private readonly Border _chip = new() { Name = "ClockChip", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
    private readonly TextBlock _chipText = new() { FontSize = 12 };
    private readonly TextBlock _timerText = new() { Name = "ClockTimerText", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _timerStart = new() { Name = "ClockTimerStart", MinWidth = 80 };
    private readonly StackPanel _timerSetters = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
    private readonly TextBlock _stopwatchText = new() { Name = "ClockStopwatchText", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _stopwatchStart = new() { Name = "ClockStopwatchStart", MinWidth = 80 };
    private readonly Button _lap = new() { Name = "ClockStopwatchLap", Content = "Lap" };
    private readonly ItemsControl _laps = new() { Name = "ClockLaps", FontSize = 12 };
    private readonly Viewbox _timerPanel;
    private readonly Viewbox _stopwatchPanel;
    private readonly StackPanel _config = new() { Name = "ClockConfig", Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    private IDisposable[] _bindings = [];
    private MainViewModel? _main;
    private ClockToolsViewModel? _tools;

    public ClockWidgetView()
    {
        // The heading sits 12px above this view's top (header margin and its spare height). Pad the bottom by the
        // same amount so the face has equal space above and below.
        Padding = BodyPadding;

        _timerStart.Click += (_, _) => _tools?.TimerStartPauseCommand.Execute(null);
        _stopwatchStart.Click += (_, _) => _tools?.StopwatchStartPauseCommand.Execute(null);
        _lap.Click += (_, _) => _tools?.StopwatchLapCommand.Execute(null);
        _open.Click += (_, _) => _tools?.ToggleExpandedCommand.Execute(null);
        _close.Click += (_, _) => _tools?.ToggleExpandedCommand.Execute(null);
        _chip.PointerPressed += (_, e) =>
        {
            _tools?.ToggleExpandedCommand.Execute(null);
            e.Handled = true;
        };
        _chip.Child = _chipText;

        _timerSetters.Children.Add(Tool("ClockTimerAdd1m", "+1m", () => _tools?.AddTimerSecondsCommand.Execute(60)));
        _timerSetters.Children.Add(Tool("ClockTimerAdd5m", "+5m", () => _tools?.AddTimerSecondsCommand.Execute(300)));
        _timerSetters.Children.Add(Tool("ClockTimerAdd10m", "+10m", () => _tools?.AddTimerSecondsCommand.Execute(600)));
        _timerSetters.Children.Add(Tool("ClockTimerClear", "Clear", () => _tools?.ClearTimerCommand.Execute(null)));

        _timerPanel = Panel("ClockTimerPanel", _timerText, _timerSetters,
            Row(_timerStart, Tool("ClockTimerReset", "Reset", () => _tools?.TimerResetCommand.Execute(null))));
        _stopwatchPanel = Panel("ClockStopwatchPanel", _stopwatchText, Row(_stopwatchStart, _lap,
            Tool("ClockStopwatchReset", "Reset", () => _tools?.StopwatchResetCommand.Execute(null))), _laps);

        var body = new Grid();
        body.Children.Add(_face);
        body.Children.Add(_timerPanel);
        body.Children.Add(_stopwatchPanel);
        body.Children.Add(_chip);
        body.Children.Add(_open);
        body.Children.Add(_config);
        _root.Children.Add(_strip);
        _root.Children.Add(body);
        Grid.SetRow(body, 1);
        Content = _root;
    }

    // Whether the face is following the clock (it is while the widget is shown).
    public bool IsFollowingClock => _bindings.Length > 0;

    // With no title bar the face is centered in the whole card, so the bottom padding goes.
    protected override void OnHeaderShownChanged() => Padding = HeaderShown ? BodyPadding : new Thickness(0);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _main = DataContext as MainViewModel;
        Follow(IsEffectivelyVisible);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Follow(false);
        _main = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_main is not null && (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible)))
        {
            Follow(IsEffectivelyVisible);
        }
    }

    private void Follow(bool on)
    {
        if (on && _main is { } main && _bindings.Length == 0)
        {
            _bindings =
            [
                _face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = main.AnalogClock }),
                _face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = main.AnalogClock }),
                _face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = main.AnalogClock }),
                _face.Bind(ClockFace.StyleProperty, new Binding(nameof(ClockSettingsViewModel.Style)) { Source = main.ClockSettings }),
            ];
            _tools = main.ClockTools;
            _tools.PropertyChanged += OnToolsChanged;
            main.Layout.PropertyChanged += OnLayoutChanged;
            _laps.ItemsSource = _tools.LapTexts;
            BuildStrip();
            BuildConfig();
            Sync();
        }
        else if (!on)
        {
            foreach (var binding in _bindings)
            {
                binding.Dispose();
            }
            _bindings = [];
            if (_tools is not null)
            {
                _tools.PropertyChanged -= OnToolsChanged;
                _tools = null;
            }
            if (_main is { } m)
            {
                m.Layout.PropertyChanged -= OnLayoutChanged;
            }
        }
    }

    private void OnToolsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClockToolsViewModel.Tabs) or nameof(ClockToolsViewModel.EnabledModes))
        {
            BuildStrip();
            BuildConfig();
        }
        Sync();
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.IsEditing))
        {
            Sync();
        }
    }

    // Brings every part in line with the view model: which tool shows, the strip, the chip and the texts.
    private void Sync()
    {
        if (_tools is not { } tools || _main is not { } main)
        {
            return;
        }
        var editing = main.Layout.IsEditing;
        var open = tools.IsExpanded && !editing;
        var mode = tools.Mode;

        _face.IsVisible = !open || mode == ClockMode.Clock;
        _face.Opacity = editing ? 0.2 : 1;
        _timerPanel.IsVisible = open && mode == ClockMode.Timer;
        _stopwatchPanel.IsVisible = open && mode == ClockMode.Stopwatch;
        _strip.IsVisible = open;
        Grid.SetRow(_strip, tools.StripAtBottom ? 2 : 0);
        _open.IsVisible = !editing && !open && ToolModes(tools).Count > 0;
        _config.IsVisible = editing;

        _chip.IsVisible = !editing && tools.ShowChip;
        _chipText.Text = tools.ChipText;
        _chip.Background = Theming.ThemeService.Brush(this, "RaisedBrush");
        _chipText.Foreground = Theming.ThemeService.Brush(this, tools.TimerStatus == TimerStatus.Finished ? "AccentBrush" : "TextBrush");

        _timerText.Text = tools.TimerText;
        _timerText.Foreground = Theming.ThemeService.Brush(this, tools.TimerStatus == TimerStatus.Finished ? "AccentBrush" : "TextBrush");
        _timerStart.Content = tools.TimerButtonText;
        _timerSetters.IsVisible = tools.CanSetTimer;
        _stopwatchText.Text = tools.StopwatchText;
        _stopwatchStart.Content = tools.StopwatchButtonText;
        _lap.IsEnabled = tools.StopwatchRunning;
        foreach (var tab in _strip.Children.OfType<Button>().Where(b => b.Tag is ClockMode))
        {
            tab.Foreground = Theming.ThemeService.Brush(this, (ClockMode)tab.Tag! == mode ? "AccentBrush" : "DimTextBrush");
        }
    }

    // The tools that can be opened: every enabled mode except the plain clock.
    private static System.Collections.Generic.IReadOnlyList<ClockMode> ToolModes(ClockToolsViewModel tools) =>
        tools.EnabledModes.Where(m => m is ClockMode.Timer or ClockMode.Stopwatch).ToList();

    private void BuildStrip()
    {
        if (_tools is not { } tools)
        {
            return;
        }
        _strip.Children.Clear();
        foreach (var mode in ClockMode.Clock.Yield().Concat(ToolModes(tools)))
        {
            var tab = new Button { Name = "ClockTab_" + mode, Content = mode.ToString(), Tag = mode, FontSize = 12, Padding = new Thickness(8, 2) };
            tab.Click += (_, _) => _tools?.SelectModeCommand.Execute(mode);
            _strip.Children.Add(tab);
        }
        _strip.Children.Add(_close);
    }

    // Edit mode: a check box per tool and a button that moves the strip.
    private void BuildConfig()
    {
        if (_tools is not { } tools)
        {
            return;
        }
        _config.Children.Clear();
        foreach (var mode in new[] { ClockMode.Timer, ClockMode.Stopwatch })
        {
            var box = new CheckBox { Name = "ClockToolToggle_" + mode, Content = mode.ToString(), IsChecked = tools.Config.IsEnabled(mode), FontSize = 12 };
            box.IsCheckedChanged += (_, _) => _tools?.SetToolEnabled(mode, box.IsChecked == true);
            _config.Children.Add(box);
        }
        var placement = new Button { Name = "ClockStripPlacement", FontSize = 12, Content = tools.StripAtBottom ? "Mode strip: bottom" : "Mode strip: top" };
        placement.Click += (_, _) =>
        {
            if (_tools is { } t)
            {
                t.SetStrip(t.StripAtBottom ? StripPlacement.Top : StripPlacement.Bottom);
                placement.Content = t.StripAtBottom ? "Mode strip: bottom" : "Mode strip: top";
            }
        };
        _config.Children.Add(placement);
    }

    private static Button Tool(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text, FontSize = 12, Padding = new Thickness(8, 2) };
        button.Click += (_, _) => click();
        return button;
    }

    private static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6 };
        row.Children.AddRange(items);
        return row;
    }

    // A tool's content, scaled down (never up) so it fits a small widget.
    private static Viewbox Panel(string name, params Control[] items)
    {
        var stack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        stack.Children.AddRange(items);
        return new Viewbox { Name = name, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = stack };
    }
}

internal static class ClockModeExtensions
{
    public static System.Collections.Generic.IEnumerable<T> Yield<T>(this T item)
    {
        yield return item;
    }
}
