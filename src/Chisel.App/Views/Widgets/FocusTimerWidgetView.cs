using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Chisel.App.Theming;
using Chisel.App.ViewModels;
using Chisel.Core.Focus;

namespace Chisel.App.Views.Widgets;

// The focus timer widget: the dial (ring, phase, time, session dots), and when there is room the Start / Skip / Reset buttons, today's
// count and the task in focus. What is shown and every size come from FocusLayout. Without the buttons, clicking the dial starts and
// pauses. When a phase ends the card is tinted in the phase's color for a few seconds. The widget tells the view model it is watching
// only while it is on screen, so the per-second updates stop when it is hidden, minimized or removed.
public sealed class FocusTimerWidgetView : WidgetView
{
    private readonly Border _flash = new() { Name = "FocusFlash", CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2) };
    private readonly FocusDial _dial = new() { Name = "FocusDial", Focusable = true, Cursor = new Cursor(StandardCursorType.Hand) };
    private readonly StackPanel _controls = new() { Name = "FocusControls", Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _primary = new() { Name = "FocusPrimary" };
    private readonly Button _skip = new() { Name = "FocusSkip", Content = "Skip" };
    private readonly Button _reset = new() { Name = "FocusReset", Content = "Reset" };
    private readonly TextBlock _sessions = new() { Name = "FocusSessions", HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _task = new() { Name = "FocusTask", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _taskText = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Grid _stack = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), VerticalAlignment = VerticalAlignment.Center };

    private FocusTimerViewModel? _focus;
    private IDisposable? _windowState;
    private bool _minimized;
    private bool _watching;
    private Color? _flashColor;

    public FocusTimerWidgetView()
    {
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Focus timer");

        _primary.Classes.Add("accent");
        _primary.Click += (_, _) => _focus?.Primary();
        _skip.Click += (_, _) => _focus?.Skip();
        _reset.Click += (_, _) => _focus?.Reset();
        AutomationProperties.SetName(_skip, "Skip to the next phase");
        AutomationProperties.SetName(_reset, "Reset the cycle");
        foreach (var b in new[] { _primary, _skip, _reset })
        {
            b.Classes.Add("focusButton");
            _controls.Children.Add(b);
        }

        _sessions.Classes.Add("focusDim");
        _task.Classes.Add("focusTask");
        _task.Content = _taskText;
        _task.Click += (_, _) => OpenTaskMenu();

        _dial.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                Toggle();
            }
        };
        _dial.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                Toggle();
                e.Handled = true;
            }
        };

        Grid.SetRow(_controls, 1);
        Grid.SetRow(_sessions, 2);
        Grid.SetRow(_task, 3);
        _stack.Children.Add(_dial);
        _stack.Children.Add(_controls);
        _stack.Children.Add(_sessions);
        _stack.Children.Add(_task);

        var root = new Panel();
        root.Children.Add(_flash);
        root.Children.Add(_stack);
        Content = root;
    }

    public FocusLayout Layout => _dial.Layout;

    public FocusDial Dial => _dial;

    // Without the buttons the dial is the button.
    private void Toggle()
    {
        if (_focus is not null && !Layout.ShowControls)
        {
            _focus.Primary();
        }
    }

    private void OpenTaskMenu()
    {
        if (_focus is not { } focus)
        {
            return;
        }
        var menu = new MenuFlyout();
        foreach (var option in focus.TaskOptions())
        {
            var item = new MenuItem { Header = option.Name, Tag = option.Id };
            item.Click += (_, _) => focus.ChooseTask(option.Id);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No open tasks", IsEnabled = false });
        }
        if (focus.TaskId is not null)
        {
            var none = new MenuItem { Header = "No task" };
            none.Click += (_, _) => focus.ChooseTask(null);
            menu.Items.Add(new Separator());
            menu.Items.Add(none);
        }
        menu.ShowAt(_task);
    }

    // Lifecycle --------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _focus = (DataContext as MainViewModel)?.FocusTimer;
        if (_focus is null)
        {
            return;
        }
        _focus.PropertyChanged += OnChanged;
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _minimized = window.WindowState == WindowState.Minimized;
            _windowState = window.GetObservable(Window.WindowStateProperty).Subscribe(new StateObserver(this));
        }
        Follow();
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Watch(false);
        _windowState?.Dispose();
        _windowState = null;
        if (_focus is not null)
        {
            _focus.PropertyChanged -= OnChanged;
            _focus = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible))
        {
            Follow();
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            Update();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Update();
    }

    private sealed class StateObserver(FocusTimerWidgetView view) : IObserver<WindowState>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value)
        {
            view._minimized = value == WindowState.Minimized;
            view.Follow();
        }
    }

    private void Follow() => Watch(_focus is not null && IsEffectivelyVisible && !_minimized);

    private void Watch(bool on)
    {
        if (on == _watching || _focus is null)
        {
            return;
        }
        _watching = on;
        if (on)
        {
            _focus.Attach(this);
        }
        else
        {
            _focus.Detach(this);
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The view model raises Updates last after rebuilding what is shown; the rest change on their own.
        if (e.PropertyName is nameof(FocusTimerViewModel.Updates) or nameof(FocusTimerViewModel.IsFlashing) or nameof(FocusTimerViewModel.TaskName)
            or nameof(FocusTimerViewModel.Settings))
        {
            Update();
        }
    }

    // Showing ----------------------------------------------------------------------------------------------------------------

    private bool Dark => ActualThemeVariant == ThemeVariant.Dark;

    private void Update()
    {
        if (_focus is not { } focus)
        {
            return;
        }
        var layout = FocusLayout.For(Bounds.Width, Bounds.Height, focus.Settings.Face, focus.LongTime, focus.DotsTotal);
        var color = ThemeColors.FocusPhase(focus.Phase, Dark);

        _stack.Margin = new Thickness(layout.Pad);
        _dial.Height = layout.Diameter;
        _dial.Show(layout, focus.Fraction, focus.PhaseText, focus.TimeText, focus.DotsDone, focus.DotsTotal, color);
        AutomationProperties.SetName(_dial, focus.AutomationName);

        var full = layout.ShowControls;
        _controls.IsVisible = full;
        _sessions.IsVisible = full;
        _task.IsVisible = full;
        if (full)
        {
            _controls.Margin = new Thickness(0, FocusLayout.Gap, 0, FocusLayout.Gap);
            _controls.Height = FocusLayout.ControlsHeight;
            _primary.Content = focus.PrimaryText;
            AutomationProperties.SetName(_primary, focus.PrimaryText + " the timer");
            _sessions.Text = focus.SessionsText;
            _sessions.FontSize = FocusLayout.FooterFont;
            _sessions.Height = FocusLayout.FooterLineHeight;
            _taskText.Text = focus.TaskName is { } name ? "Focus: " + name : "Choose a task to focus on";
            _taskText.FontSize = FocusLayout.FooterFont;
            _task.Height = FocusLayout.FooterLineHeight;
            _task.MaxWidth = Math.Max(0, Bounds.Width - 2 * layout.Pad);
            AutomationProperties.SetName(_task, focus.TaskName is { } n ? $"Task in focus: {n}. Choose another" : "Choose a task to focus on");
        }

        var flash = focus.IsFlashing ? color : (Color?)null;
        if (flash != _flashColor)
        {
            _flashColor = flash;
            _flash.Background = flash is { } c ? new ImmutableSolidColorBrush(c.WithAlpha(0x26)) : Brushes.Transparent;
            _flash.BorderBrush = flash is { } b ? new ImmutableSolidColorBrush(b.WithAlpha(0xA0)) : Brushes.Transparent;
        }
    }
}
