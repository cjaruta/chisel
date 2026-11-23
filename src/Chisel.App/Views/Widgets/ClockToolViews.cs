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

// A tool's content as built for one slot: what to release when the slot goes away, and how to fit it to the room it has.
internal sealed class BuiltTool : IDisposable
{
    private readonly List<IDisposable> _disposables;
    private readonly Action<ClockTypeScale> _rescale;

    public Control Control { get; }

    public BuiltTool(Control control, List<IDisposable> disposables, Action<ClockTypeScale> rescale)
    {
        Control = control;
        _disposables = disposables;
        _rescale = rescale;
        rescale(ClockTypeScale.For(ClockTypeScale.Reference, ClockTypeScale.Reference));
    }

    // Sizes the fonts, buttons and spacing from the slot's size (see ClockTypeScale).
    public void Rescale(ClockTypeScale scale) => _rescale(scale);

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
// view models. Nothing is a fixed size: fonts, buttons and spacing all come from one ClockTypeScale worked out from the slot, the same
// family and relative sizes as the clock beside them, and rows of buttons wrap when the slot is narrow. The wheel is handled by the slot,
// never here, and nothing here reacts to it: the wheel only cycles tools.
internal static class ClockToolViews
{
    public static BuiltTool Build(ClockTool tool, MainViewModel main, Control host)
    {
        var disposables = new List<IDisposable>();
        Action<ClockTypeScale> rescale = _ => { };
        Control control = tool switch
        {
            ClockTool.Clock => Face(main, disposables),
            ClockTool.Timer => Timer(main.ClockTools, host, disposables, out rescale),
            ClockTool.Stopwatch => Stopwatch(main, disposables, out rescale),
            ClockTool.Alarm => Alarm(main.Alarms, disposables, out rescale),
            _ => new Panel { Name = "ClockEmpty" },
        };
        return new BuiltTool(control, disposables, rescale);
    }

    private static ClockFace Face(MainViewModel main, List<IDisposable> disposables)
    {
        var face = new ClockFace { Name = "ClockFace", IsHitTestVisible = false };
        disposables.Add(face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.Hour24AngleProperty, new Binding(nameof(AnalogClockViewModel.Hour24Angle)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(Avalonia.Automation.AutomationProperties.NameProperty, new Binding(nameof(AnalogClockViewModel.Description)) { Source = main.AnalogClock }));
        disposables.Add(face.Bind(ClockFace.StyleProperty, new Binding(nameof(ClockSettingsViewModel.Style)) { Source = main.ClockSettings }));
        return face;
    }

    private static Control Timer(ClockToolsViewModel tools, Control host, List<IDisposable> disposables, out Action<ClockTypeScale> rescale)
    {
        var text = Display("ClockTimerText");
        text.Bind(TextBlock.TextProperty, new Binding(nameof(ClockToolsViewModel.TimerText)) { Source = tools });
        var start = Primary("ClockTimerStart");
        start.Bind(ContentControl.ContentProperty, new Binding(nameof(ClockToolsViewModel.TimerButtonText)) { Source = tools });
        start.Click += (_, _) => tools.TimerStartPauseCommand.Execute(null);
        var reset = Secondary("ClockTimerReset", "Reset", () => tools.TimerResetCommand.Execute(null));

        var presets = new[]
        {
            Secondary("ClockTimerAdd1m", "+1m", () => tools.AddTimerSecondsCommand.Execute(60)),
            Secondary("ClockTimerAdd5m", "+5m", () => tools.AddTimerSecondsCommand.Execute(300)),
            Secondary("ClockTimerAdd10m", "+10m", () => tools.AddTimerSecondsCommand.Execute(600)),
            Secondary("ClockTimerClear", "Clear", () => tools.ClearTimerCommand.Execute(null)),
        };
        var setters = Wrap(presets);
        setters.Bind(Visual.IsVisibleProperty, new Binding(nameof(ClockToolsViewModel.CanSetTimer)) { Source = tools });
        var actions = Wrap(start, reset);

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

        var stack = Stack(text, setters, actions);
        rescale = s =>
        {
            text.FontSize = s.Display;
            Fit(stack, s);
            foreach (var button in presets)
            {
                Fit(button, s, false);
            }
            Fit(reset, s, false);
            Fit(start, s, true);
            Fit(setters, s);
            Fit(actions, s);
        };
        return Centered("ClockTimerPanel", stack);
    }

    private static Control Stopwatch(MainViewModel main, List<IDisposable> disposables, out Action<ClockTypeScale> rescale)
    {
        var tools = main.ClockTools;
        var settings = main.ClockSettings;
        var text = Display("ClockStopwatchText");
        text.Bind(TextBlock.TextProperty, new Binding(nameof(ClockToolsViewModel.StopwatchText)) { Source = tools });
        var start = Primary("ClockStopwatchStart");
        start.Bind(ContentControl.ContentProperty, new Binding(nameof(ClockToolsViewModel.StopwatchButtonText)) { Source = tools });
        start.Click += (_, _) => tools.StopwatchStartPauseCommand.Execute(null);
        var lap = Secondary("ClockStopwatchLap", "Lap", () => tools.StopwatchLapCommand.Execute(null));
        lap.Bind(InputElement.IsEnabledProperty, new Binding(nameof(ClockToolsViewModel.StopwatchRunning)) { Source = tools });
        var reset = Secondary("ClockStopwatchReset", "Reset", () => tools.StopwatchResetCommand.Execute(null));
        var actions = Wrap(start, lap, reset);
        var lapList = new ItemsControl { Name = "ClockLaps", ItemsSource = tools.LapTexts };
        var laps = new ScrollViewer { Content = lapList, HorizontalAlignment = HorizontalAlignment.Center };

        // The analog face (when the look asks for it and there is room): a dial above the readout. Hidden, it costs nothing.
        var face = new StopwatchFace { Name = "ClockStopwatchFace", IsHitTestVisible = false, IsVisible = false, ElapsedSource = tools.StopwatchElapsedSeconds, HorizontalAlignment = HorizontalAlignment.Center };
        disposables.Add(face.Bind(StopwatchFace.StyleProperty, new Binding(nameof(ClockSettingsViewModel.Style)) { Source = settings }));
        var automation = new Binding(nameof(ClockToolsViewModel.StopwatchText)) { Source = tools, StringFormat = "Stopwatch, {0}" };
        disposables.Add(face.Bind(Avalonia.Automation.AutomationProperties.NameProperty, automation));
        disposables.Add(text.Bind(Avalonia.Automation.AutomationProperties.NameProperty, automation));
        var analogWanted = false;
        var room = 0.0;
        void ShowFace()
        {
            face.IsVisible = analogWanted && room > 0;
            face.Width = face.Height = room;
        }
        void OnTools(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClockToolsViewModel.StopwatchRunning))
            {
                face.IsRunning = tools.StopwatchRunning;
            }
            if (e.PropertyName is nameof(ClockToolsViewModel.StopwatchText) or nameof(ClockToolsViewModel.StopwatchRunning) && face.IsVisible && !tools.StopwatchRunning)
            {
                face.InvalidateVisual(); // stopped: draw the time it stopped at, once
            }
        }
        void OnSettings(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClockSettingsViewModel.Style))
            {
                analogWanted = settings.Style.Stopwatch == StopwatchDisplay.Analog;
                ShowFace();
            }
        }
        tools.PropertyChanged += OnTools;
        settings.PropertyChanged += OnSettings;
        disposables.Add(new ActionDisposable(() => { tools.PropertyChanged -= OnTools; settings.PropertyChanged -= OnSettings; }));
        analogWanted = settings.Style.Stopwatch == StopwatchDisplay.Analog;
        face.IsRunning = tools.StopwatchRunning;

        var stack = Stack(face, text, actions, laps);
        rescale = s =>
        {
            room = s.DialSize;
            ShowFace();
            text.FontSize = s.Display;
            Fit(stack, s);
            Fit(start, s, true);
            Fit(lap, s, false);
            Fit(reset, s, false);
            Fit(actions, s);
            lapList.FontSize = s.Caption;
            laps.MaxHeight = s.Caption * 5;
            // The lap list needs room; the time and buttons come first, then the dial, then the laps.
            laps.IsVisible = !s.Compact && (!analogWanted || s.DialSize >= 160);
        };
        return Centered("ClockStopwatchPanel", stack);
    }

    // The compact alarm: the next alarm to ring, large, a switch for it, and one action that opens the full list and editor (which
    // does not fit a slot, so it opens as a flyout beside it). While an alarm rings it shows the alarm with Dismiss and Snooze.
    private static Control Alarm(AlarmsViewModel alarms, List<IDisposable> disposables, out Action<ClockTypeScale> rescale)
    {
        var time = Display("ClockAlarmNextTime");
        time.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.NextTimeText)) { Source = alarms });
        var caption = new TextBlock { Name = "ClockAlarmNextCaption", Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        caption.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.NextCaption)) { Source = alarms });
        var toggle = new ToggleSwitch { Name = "ClockAlarmToggle", HorizontalAlignment = HorizontalAlignment.Center, OnContent = null, OffContent = null };
        toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(AlarmsViewModel.NextEnabled)) { Source = alarms, Mode = BindingMode.TwoWay });
        toggle.Bind(Visual.IsVisibleProperty, new Binding(nameof(AlarmsViewModel.HasAlarms)) { Source = alarms });

        var manage = Secondary("ClockAlarmManage", "Manage", () => alarms.ManageCommand.Execute(null));
        var editor = new Flyout { Content = AlarmEditor(alarms), Placement = PlacementMode.Bottom };
        manage.Tag = editor; // the flyout it opens
        void OnManaging(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AlarmsViewModel.IsManaging))
            {
                return;
            }
            if (alarms.IsManaging)
            {
                editor.ShowAt(manage);
            }
            else
            {
                editor.Hide();
            }
        }
        alarms.PropertyChanged += OnManaging;
        disposables.Add(new ActionDisposable(() => alarms.PropertyChanged -= OnManaging));
        // Closing the flyout by clicking away puts the view model back to not managing.
        editor.Closed += (_, _) =>
        {
            if (alarms.IsManaging)
            {
                alarms.ManageCommand.Execute(null);
            }
        };

        var manageRow = Wrap(manage);
        var compact = Stack(time, caption, toggle, manageRow);
        compact.Bind(Visual.IsVisibleProperty, new Binding(nameof(AlarmsViewModel.Ringing)) { Source = alarms, Converter = ObjectConverters.IsNull });

        var ringingTitle = new TextBlock { Name = "ClockAlarmRingingTitle", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        ringingTitle.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.RingingTitle)) { Source = alarms });
        var ringingTime = new TextBlock { Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center };
        ringingTime.Bind(TextBlock.TextProperty, new Binding(nameof(AlarmsViewModel.RingingTime)) { Source = alarms });
        var dismiss = Primary("ClockAlarmDismiss");
        dismiss.Content = "Dismiss";
        dismiss.Command = alarms.DismissRingingCommand;
        var snooze = Primary("ClockAlarmSnooze");
        snooze.Content = $"Snooze {AlarmsViewModel.SnoozeMinutes} min";
        snooze.Command = alarms.SnoozeCommand;
        var ringingButtons = Wrap(dismiss, snooze);
        var ringing = Stack(ringingTitle, ringingTime, ringingButtons);
        ringing.Name = "ClockAlarmRinging";
        ringing.Bind(Visual.IsVisibleProperty, new Binding(nameof(AlarmsViewModel.Ringing)) { Source = alarms, Converter = ObjectConverters.IsNotNull });

        var root = new Grid { Name = "ClockAlarmPanel" };
        root.Children.Add(compact);
        root.Children.Add(ringing);
        rescale = s =>
        {
            time.FontSize = s.Display;
            caption.FontSize = s.Caption;
            toggle.RenderTransform = new ScaleTransform(s.Factor, s.Factor);
            Fit(compact, s);
            Fit(manage, s, false);
            ringingTitle.FontSize = Math.Round(s.Display * 0.7);
            ringingTime.FontSize = s.Title;
            Fit(ringing, s);
            Fit(dismiss, s, true);
            Fit(snooze, s, true);
            Fit(ringingButtons, s);
            Fit(manageRow, s);
        };
        return root;
    }

    // The full list and editor, opened from the compact display: a form to add an alarm (time as HH:MM, a label, the days) over the
    // list, each row with an on/off box and a delete button. It sits in a flyout, so it has room.
    private static Control AlarmEditor(AlarmsViewModel alarms)
    {
        var scale = ClockTypeScale.For(ClockTypeScale.Reference, ClockTypeScale.Reference);
        var time = new TextBox { Name = "ClockAlarmTime", MinWidth = scale.Body * 6, Watermark = "HH:MM", FontSize = scale.Body };
        time.Bind(TextBox.TextProperty, new Binding(nameof(AlarmsViewModel.NewTime)) { Source = alarms, Mode = BindingMode.TwoWay });
        var label = new TextBox { Name = "ClockAlarmLabel", Watermark = "Label", MinWidth = scale.Body * 8, FontSize = scale.Body, HorizontalAlignment = HorizontalAlignment.Stretch };
        label.Bind(TextBox.TextProperty, new Binding(nameof(AlarmsViewModel.NewLabel)) { Source = alarms, Mode = BindingMode.TwoWay });
        var add = new Button { Name = "ClockAlarmAdd", Content = "Add", Command = alarms.AddCommand, FontSize = scale.Body };

        var days = new WrapPanel { ItemSpacing = scale.Gap / 2, LineSpacing = scale.Gap / 2 };
        foreach (var day in alarms.Days)
        {
            var toggle = new ToggleButton { Name = "ClockAlarmDay_" + day.Day, Content = day.Title, FontSize = scale.Caption, Padding = new Thickness(scale.Gap, scale.ButtonPadY) };
            toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(DayToggle.IsOn)) { Source = day, Mode = BindingMode.TwoWay });
            days.Children.Add(toggle);
        }

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = scale.Gap };
        top.Children.Add(time);
        top.Children.Add(label);
        top.Children.Add(add);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(add, 2);
        var form = new StackPanel { Spacing = scale.Gap };
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
                text.Children.Add(new TextBlock { Text = item.TimeText, FontSize = scale.Title, FontWeight = FontWeight.SemiBold });
                text.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(item.Label) ? item.DaysText : item.Label + " · " + item.DaysText, FontSize = scale.Caption, Opacity = 0.7 });
                var delete = new Button { Content = "x", FontSize = scale.Caption, VerticalAlignment = VerticalAlignment.Center };
                delete.Click += (_, _) => item?.DeleteCommand.Execute(null);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, scale.Gap / 2) };
                row.Children.Add(on);
                row.Children.Add(text);
                row.Children.Add(delete);
                Grid.SetColumn(text, 1);
                Grid.SetColumn(delete, 2);
                return row;
            }),
        };
        var panel = new Grid { Name = "ClockAlarmEditor", RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = scale.Pad, Width = scale.Body * 24, MaxHeight = scale.Body * 24 };
        panel.Children.Add(form);
        var scroll = new ScrollViewer { Content = list };
        panel.Children.Add(scroll);
        Grid.SetRow(scroll, 1);
        return panel;
    }

    private static TextBlock Display(string name) => new() { Name = name, HorizontalAlignment = HorizontalAlignment.Center };

    private static Button Primary(string name) => new() { Name = name, HorizontalContentAlignment = HorizontalAlignment.Center };

    private static Button Secondary(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => click();
        return button;
    }

    // A row of buttons that wraps onto more lines when the slot is narrow.
    private static WrapPanel Wrap(params Control[] items)
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.AddRange(items);
        return row;
    }

    private static StackPanel Stack(params Control[] items)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        stack.Children.AddRange(items);
        return stack;
    }

    private static Grid Centered(string name, Control content)
    {
        var grid = new Grid { Name = name };
        grid.Children.Add(content);
        return grid;
    }

    private static void Fit(StackPanel stack, ClockTypeScale s)
    {
        stack.Spacing = s.Gap;
        stack.Margin = new Thickness(s.Pad);
    }

    private static void Fit(WrapPanel wrap, ClockTypeScale s)
    {
        wrap.ItemSpacing = s.Gap;
        wrap.LineSpacing = s.Gap;
    }

    private static void Fit(Button button, ClockTypeScale s, bool primary)
    {
        button.FontSize = primary ? s.Body : s.Caption;
        button.Padding = new Thickness(s.ButtonPadX, s.ButtonPadY);
        button.MinWidth = primary ? s.ButtonMinWidth : 0;
    }
}
