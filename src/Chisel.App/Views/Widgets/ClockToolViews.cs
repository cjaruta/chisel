using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// A tool's content as built for one slot, with whatever it has to release when the slot goes away.
internal sealed class BuiltTool : IDisposable
{
    private readonly List<IDisposable> _disposables;

    public Control Control { get; }

    public BuiltTool(Control control, List<IDisposable> disposables)
    {
        Control = control;
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}

internal sealed class ActionDisposable(Action action) : IDisposable
{
    public void Dispose() => action();
}

// Builds what each tool looks like inside a slot: plain controls over the widget (no panels or dividers of their own), bound to the
// view models. The wheel is handled by the slot, never here, and nothing here reacts to it: the wheel only cycles tools.
internal static class ClockToolViews
{
    public static BuiltTool Build(ClockTool tool, MainViewModel main, Control host)
    {
        var disposables = new List<IDisposable>();
        Control control = tool switch
        {
            ClockTool.Clock => Face(main, disposables),
            ClockTool.Timer => Timer(main.ClockTools, host, disposables),
            ClockTool.Stopwatch => Stopwatch(main.ClockTools),
            ClockTool.Alarm => Alarm(main.Alarms, disposables),
            _ => new Panel { Name = "ClockEmpty" },
        };
        return new BuiltTool(control, disposables);
    }

    private static ClockFace Face(MainViewModel main, List<IDisposable> disposables)
    {
        var face = new ClockFace { Name = "ClockFace", IsHitTestVisible = false };
        disposables.Add(face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.StyleProperty, new Binding(nameof(ClockSettingsViewModel.Style)) { Source = main.ClockSettings }));
        return face;
    }

    private static Control Timer(ClockToolsViewModel tools, Control host, List<IDisposable> disposables)
    {
        var text = new TextBlock { Name = "ClockTimerText", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center };
        text.Bind(TextBlock.TextProperty, new Binding(nameof(ClockToolsViewModel.TimerText)) { Source = tools });
        var start = new Button { Name = "ClockTimerStart", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        start.Bind(ContentControl.ContentProperty, new Binding(nameof(ClockToolsViewModel.TimerButtonText)) { Source = tools });
        start.Click += (_, _) => tools.TimerStartPauseCommand.Execute(null);

        var setters = Row(
            Tool("ClockTimerAdd1m", "+1m", () => tools.AddTimerSecondsCommand.Execute(60)),
            Tool("ClockTimerAdd5m", "+5m", () => tools.AddTimerSecondsCommand.Execute(300)),
            Tool("ClockTimerAdd10m", "+10m", () => tools.AddTimerSecondsCommand.Execute(600)),
            Tool("ClockTimerClear", "Clear", () => tools.ClearTimerCommand.Execute(null)));
        setters.Bind(Visual.IsVisibleProperty, new Binding(nameof(ClockToolsViewModel.CanSetTimer)) { Source = tools });

        // The time takes the accent colour when the timer is done.
        void Recolor() => text.Foreground = Theming.ThemeService.Brush(host, tools.TimerStatus == TimerStatus.Finished ? "AccentBrush" : "TextBrush");
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClockToolsViewModel.TimerStatus))
            {
                Recolor();
            }
        }
        tools.PropertyChanged += OnChanged;
        disposables.Add(new ActionDisposable(() => tools.PropertyChanged -= OnChanged));
        Recolor();

        return Panel("ClockTimerPanel", text, setters, Row(start, Tool("ClockTimerReset", "Reset", () => tools.TimerResetCommand.Execute(null))));
    }

    private static Control Stopwatch(ClockToolsViewModel tools)
    {
        var text = new TextBlock { Name = "ClockStopwatchText", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center };
        text.Bind(TextBlock.TextProperty, new Binding(nameof(ClockToolsViewModel.StopwatchText)) { Source = tools });
        var start = new Button { Name = "ClockStopwatchStart", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        start.Bind(ContentControl.ContentProperty, new Binding(nameof(ClockToolsViewModel.StopwatchButtonText)) { Source = tools });
        start.Click += (_, _) => tools.StopwatchStartPauseCommand.Execute(null);
        var lap = Tool("ClockStopwatchLap", "Lap", () => tools.StopwatchLapCommand.Execute(null));
        lap.Bind(InputElement.IsEnabledProperty, new Binding(nameof(ClockToolsViewModel.StopwatchRunning)) { Source = tools });
        var laps = new ScrollViewer { MaxHeight = 70, Content = new ItemsControl { Name = "ClockLaps", FontSize = 12, ItemsSource = tools.LapTexts } };

        return Panel("ClockStopwatchPanel", text, Row(start, lap, Tool("ClockStopwatchReset", "Reset", () => tools.StopwatchResetCommand.Execute(null))), laps);
    }

    // The alarm list with a small form to add one; while an alarm rings it gives way to the alarm, with Dismiss and Snooze.
    private static Control Alarm(AlarmsViewModel alarms, List<IDisposable> disposables)
    {
        var time = new TextBox { Name = "ClockAlarmTime", Width = 80, Watermark = "HH:MM", FontSize = 13 };
        time.Bind(TextBox.TextProperty, new Binding(nameof(AlarmsViewModel.NewTime)) { Source = alarms, Mode = BindingMode.TwoWay });
        var label = new TextBox { Name = "ClockAlarmLabel", Watermark = "Label", MinWidth = 100, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
        label.Bind(TextBox.TextProperty, new Binding(nameof(AlarmsViewModel.NewLabel)) { Source = alarms, Mode = BindingMode.TwoWay });
        var add = new Button { Name = "ClockAlarmAdd", Content = "Add", Command = alarms.AddCommand };

        var days = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var day in alarms.Days)
        {
            var toggle = new ToggleButton { Name = "ClockAlarmDay_" + day.Day, Content = day.Title, FontSize = 11, Padding = new Thickness(5, 2) };
            toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(DayToggle.IsOn)) { Source = day, Mode = BindingMode.TwoWay });
            days.Children.Add(toggle);
        }

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
        top.Children.Add(time);
        top.Children.Add(label);
        top.Children.Add(add);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(add, 2);
        var form = new StackPanel { Spacing = 6 };
        form.Children.Add(top);
        form.Children.Add(days);

        var list = new ItemsControl
        {
            Name = "ClockAlarmList",
            ItemsSource = alarms.Items,
            ItemTemplate = new FuncDataTemplate<AlarmItemViewModel>((item, _) =>
            {
                var on = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                on.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(AlarmItemViewModel.Enabled)) { Source = item, Mode = BindingMode.TwoWay });
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = item.TimeText, FontSize = 18, FontWeight = FontWeight.SemiBold });
                text.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(item.Label) ? item.DaysText : item.Label + " · " + item.DaysText, FontSize = 11, Opacity = 0.7 });
                var delete = new Button { Content = "x", FontSize = 11, Padding = new Thickness(6, 1), VerticalAlignment = VerticalAlignment.Center };
                delete.Click += (_, _) => item?.DeleteCommand.Execute(null);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 3) };
                row.Children.Add(on);
                row.Children.Add(text);
                row.Children.Add(delete);
                Grid.SetColumn(text, 1);
                Grid.SetColumn(delete, 2);
                return row;
            }),
        };
        var normal = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        normal.Children.Add(form);
        var scroll = new ScrollViewer { Content = list };
        normal.Children.Add(scroll);
        Grid.SetRow(scroll, 1);
        normal.Bind(Visual.IsVisibleProperty, new Binding(nameof(AlarmsViewModel.Ringing)) { Source = alarms, Converter = ObjectConverters.IsNull });

        var ringingTitle = new TextBlock { Name = "ClockAlarmRingingTitle", FontSize = 28, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        ringingTitle.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.RingingTitle)) { Source = alarms });
        var ringingTime = new TextBlock { FontSize = 18, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center };
        ringingTime.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.RingingTime)) { Source = alarms });
        var dismiss = new Button { Name = "ClockAlarmDismiss", Content = "Dismiss", Command = alarms.DismissRingingCommand };
        var snooze = new Button { Name = "ClockAlarmSnooze", Content = $"Snooze {AlarmsViewModel.SnoozeMinutes} min", Command = alarms.SnoozeCommand };
        var ringing = new StackPanel { Name = "ClockAlarmRinging", Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        ringing.Children.Add(ringingTitle);
        ringing.Children.Add(ringingTime);
        ringing.Children.Add(Row(dismiss, snooze));
        ringing.Bind(Visual.IsVisibleProperty, new Binding(nameof(AlarmsViewModel.Ringing)) { Source = alarms, Converter = ObjectConverters.IsNotNull });

        var host = new Grid { Name = "ClockAlarmPanel", Margin = new Thickness(8) };
        host.Children.Add(normal);
        host.Children.Add(ringing);
        return host;
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

    // A tool's content, scaled down (never up) so it fits a small slot.
    private static Viewbox Panel(string name, params Control[] items)
    {
        var stack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        stack.Children.AddRange(items);
        return new Viewbox { Name = name, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = stack };
    }
}
