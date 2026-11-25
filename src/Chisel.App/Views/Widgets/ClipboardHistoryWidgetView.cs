using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.Core.Clipboard;

namespace Chisel.App.Views.Widgets;

// The clipboard history widget. Off, it explains what it records and offers Start recording; nothing listens until then. Recording,
// it shows the latest copy and a count (small), a list of copies (medium), or a search box and each copy's time as well (large).
// What is shown and every size come from ClipboardLayout. Click an item, or select it and press Enter, to copy it back; Delete
// removes the selected item; the arrows move through the list. Copy, pin and delete buttons show on the hovered or selected row.
// The toolbar above holds Start / Pause / Stop, Clear all and the options (keep history between sessions, limits).
// Automation names read only an item's preview (one line, cut short), its length and its time: never the whole text.
public sealed class ClipboardHistoryWidgetView : WidgetView
{
    private const string RecordIcon = "M12,6 A6,6 0 1 1 11.99,6 Z";
    private const string PauseIcon = "M9,6 V18 M15,6 V18";
    private const string ResumeIcon = "M8,5 L19,12 L8,19 Z";
    private const string StopIcon = "M7,7 H17 V17 H7 Z";
    private const string ClearIcon = "M5,7 H19 M10,7 V4 H14 V7 M7,7 L8,20 H16 L17,7";
    private const string OptionsIcon = "M4,7 H20 M4,17 H20 M9,4 V10 M15,14 V20";
    private const string CopyIcon = "M9,9 H20 V20 H9 Z M5,15 H4 V4 H15 V5";
    private const string PinIcon = "M9,4 H15 L14,10 L17,13 H7 L10,10 Z M12,13 V20";
    private const string DeleteIcon = "M6,6 L18,18 M18,6 L6,18";

    // The toolbar shows the state as a word from this width; narrower, as a dot.
    public const double StatusWordMinWidth = 260;

    public const string IntroText =
        "Keeps a list of text you copy so you can copy it again. Nothing is recorded until you press Start recording, and only text "
        + "is kept: no pictures or files. History stays in memory and is gone when Chisel closes, unless you turn on Keep history in "
        + "the options. Copies from password managers, and text that looks like a card number, key or password, are skipped, but "
        + "these checks can't catch everything.";

    public const string FilterNote =
        "Skips copies from password managers and from apps that mark a copy private, text longer than the limit, and text that looks "
        + "like a card number, key or password. These checks can't catch everything, so pause recording before copying anything "
        + "sensitive.";

    private readonly Grid _root = new() { Name = "ClipRoot" };

    private readonly StackPanel _intro = new() { Name = "ClipIntroPanel", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _introText = new() { Name = "ClipIntro", Text = IntroText, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.WordEllipsis };
    private readonly Button _start = new()
    {
        Name = "ClipStart", Content = "Start recording", HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center,
    };

    private readonly Button _latest = new() { Name = "ClipLatest" };
    private readonly TextBlock _latestText = new() { Name = "ClipLatestText", TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock _count = new() { Name = "ClipCount", TextTrimming = TextTrimming.CharacterEllipsis };

    private readonly DockPanel _listArea = new() { Name = "ClipListArea", LastChildFill = true };
    private readonly TextBox _search = new() { Name = "ClipSearch", Watermark = "Search" };
    private readonly ListBox _list = new() { Name = "ClipList", SelectionMode = SelectionMode.Single };
    private readonly TextBlock _empty = new() { Name = "ClipEmpty", Text = "Copy some text and it shows up here.", TextWrapping = TextWrapping.Wrap };

    private readonly ScrollViewer _options = new() { Name = "ClipOptionsPanel", IsVisible = false };
    private readonly CheckBox _keep = new() { Name = "ClipKeep", Content = "Keep history between sessions" };
    private readonly TextBlock _keepNote = new() { Name = "ClipKeepNote", TextWrapping = TextWrapping.Wrap };
    private readonly NumericUpDown _maxItems = new()
    {
        Name = "ClipMaxItems", Minimum = 1, Maximum = ClipboardHistorySettings.MaxItemsLimit, Increment = 5, FormatString = "0", Width = 120,
    };
    private readonly NumericUpDown _maxLength = new()
    {
        Name = "ClipMaxLength", Minimum = ClipboardHistorySettings.MinLength, Maximum = ClipboardHistorySettings.MaxLengthLimit,
        Increment = 1000, FormatString = "0", Width = 120,
    };
    private readonly TextBlock _filterNote = new() { Name = "ClipFilterNote", Text = FilterNote, TextWrapping = TextWrapping.Wrap };

    private readonly TextBlock _copied = new() { Name = "ClipCopied", Text = "Copied", IsVisible = false };

    private readonly StackPanel _toolbar = new() { Name = "ClipActions", Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly TextBlock _status = new() { Name = "ClipStatus", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    private readonly TextBlock _statusDot = new() { Name = "ClipStatusDot", Text = "●", IsVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button _record = ToolButton("ClipRecord", RecordIcon, "Start recording");
    private readonly Button _pause = ToolButton("ClipPause", PauseIcon, "Pause recording");
    private readonly Button _stop = ToolButton("ClipStop", StopIcon, "Stop recording");
    private readonly Button _clear = ToolButton("ClipClear", ClearIcon, "Clear all");
    private readonly Button _optionsButton = ToolButton("ClipOptions", OptionsIcon, "Clipboard history options");

    private readonly DispatcherTimer _ages = new() { Interval = TimeSpan.FromMinutes(1) };
    private ClipboardHistoryViewModel? _vm;
    private bool _syncing;

    public ClipboardLayout Layout { get; private set; } = ClipboardLayout.For(0, 0);

    public ClipboardHistoryWidgetView()
    {
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Clipboard history");

        // Toolbar, shown by the frame above the body.
        _status.Classes.Add("cdDim");
        _statusDot.Classes.Add("cdDim");
        _toolbar.Children.Add(_status);
        _toolbar.Children.Add(_statusDot);
        foreach (var button in new[] { _record, _pause, _stop, _clear, _optionsButton })
        {
            _toolbar.Children.Add(button);
        }
        _record.Click += (_, _) => _vm?.Start();
        _pause.Click += (_, _) => _vm?.TogglePause();
        _stop.Click += (_, _) => _vm?.Stop();
        _clear.Click += (_, _) => _vm?.ClearAll();
        _optionsButton.Click += (_, _) =>
        {
            _options.IsVisible = !_options.IsVisible;
            ShowState();
        };
        BodyActions = _toolbar;

        // Off: what it does, and the button to start.
        _start.Classes.Add("accent");
        AutomationProperties.SetName(_start, "Start recording");
        _start.Click += (_, _) => _vm?.Start();
        _intro.Children.Add(_introText);
        _intro.Children.Add(_start);

        // Small: the latest copy and the count. Clicking it copies the latest again.
        _latest.Classes.Add("clipLatest");
        _count.Classes.Add("cdDim");
        var latestPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        latestPanel.Children.Add(_latestText);
        latestPanel.Children.Add(_count);
        _latest.Content = latestPanel;
        _latest.Click += (_, _) => CopyLatest();

        // Medium and large: search (large only) and the list.
        _search.Classes.Add("clipField");
        AutomationProperties.SetName(_search, "Search clipboard history");
        _search.TextChanged += (_, _) =>
        {
            if (!_syncing && _vm is not null)
            {
                _vm.Query = _search.Text ?? "";
            }
        };
        DockPanel.SetDock(_search, Dock.Top);
        _list.Classes.Add("clipList");
        AutomationProperties.SetName(_list, "Clipboard items");
        _list.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is ClipboardEntry entry)
            {
                AutomationProperties.SetName(e.Container, Describe(entry));
            }
        };
        _list.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        _list.Tapped += OnListTapped;
        _empty.Classes.Add("cdDim");
        _empty.IsVisible = false;
        _listArea.Children.Add(_search);
        _listArea.Children.Add(_empty);
        _listArea.Children.Add(_list);

        // Options.
        _keepNote.Classes.Add("cdDim");
        _filterNote.Classes.Add("cdDim");
        AutomationProperties.SetName(_keep, "Keep history between sessions");
        AutomationProperties.SetName(_maxItems, "Most items to keep");
        AutomationProperties.SetName(_maxLength, "Skip text longer than this many characters");
        _keep.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing && _vm is not null)
            {
                _vm.KeepHistory = _keep.IsChecked == true;
                ShowOptions();
            }
        };
        _maxItems.ValueChanged += (_, e) =>
        {
            if (!_syncing && _vm is not null && e.NewValue is { } value)
            {
                _vm.MaxItems = (int)value;
            }
        };
        _maxLength.ValueChanged += (_, e) =>
        {
            if (!_syncing && _vm is not null && e.NewValue is { } value)
            {
                _vm.MaxLength = (int)value;
            }
        };
        var done = new Button { Name = "ClipOptionsDone", Content = "Done" };
        done.Click += (_, _) =>
        {
            _options.IsVisible = false;
            ShowState();
        };
        var options = new StackPanel { Spacing = 8 };
        options.Children.Add(_keep);
        options.Children.Add(_keepNote);
        options.Children.Add(Labeled("Most items to keep (pinned items don't count)", _maxItems));
        options.Children.Add(Labeled("Skip text longer than (characters)", _maxLength));
        options.Children.Add(_filterNote);
        options.Children.Add(done);
        _options.Content = options;

        _copied.Classes.Add("clipCopied");
        _copied.HorizontalAlignment = HorizontalAlignment.Right;
        _copied.VerticalAlignment = VerticalAlignment.Bottom;

        _root.Children.Add(_intro);
        _root.Children.Add(_latest);
        _root.Children.Add(_listArea);
        _root.Children.Add(_options);
        _root.Children.Add(_copied);
        Content = _root;

        _ages.Tick += (_, _) => ShowItems();
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(control);
        return panel;
    }

    private static Path Icon(string data, double size) => new()
    {
        Data = Geometry.Parse(data), Stretch = Stretch.Uniform, StrokeThickness = 1.6, StrokeLineCap = PenLineCap.Round,
        Width = size, Height = size,
        [!Shape.StrokeProperty] = new Binding(nameof(Button.Foreground)) { RelativeSource = new RelativeSource { AncestorType = typeof(Button) } },
    };

    private static Button ToolButton(string name, string icon, string automationName)
    {
        var button = new Button { Name = name, Content = Icon(icon, 12) };
        button.Classes.Add("clipTool");
        AutomationProperties.SetName(button, automationName);
        ToolTip.SetTip(button, automationName);
        return button;
    }

    // Lifecycle --------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _vm = (DataContext as MainViewModel)?.ClipboardHistory;
        if (_vm is null)
        {
            return;
        }
        _vm.PropertyChanged += OnChanged;
        _syncing = true;
        _search.Text = _vm.Query;
        _syncing = false;
        ApplyLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _ages.Stop();
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnChanged;
            _vm = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout();
        // The toolbar's status depends on the width but sits outside this view: change it after this layout pass so it is measured again.
        Dispatcher.UIThread.Post(ShowState);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ClipboardHistoryViewModel.Visible):
                ShowItems();
                break;
            case nameof(ClipboardHistoryViewModel.IsRecording) or nameof(ClipboardHistoryViewModel.IsPaused):
                ShowState();
                break;
            case nameof(ClipboardHistoryViewModel.CopiedId):
                _copied.IsVisible = _vm?.CopiedId is not null;
                break;
            case nameof(ClipboardHistoryViewModel.KeepHistory) or nameof(ClipboardHistoryViewModel.MaxItems) or nameof(ClipboardHistoryViewModel.MaxLength):
                ShowOptions();
                break;
        }
    }

    // Input ------------------------------------------------------------------------------------------------------------------

    // Taken on the way down: the window binds some keys before the list would see them.
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null || e.KeyModifiers != KeyModifiers.None || _list.ItemCount == 0
            || this.FindAncestorOfType<WidgetFrame>() is { IsEditing: true })
        {
            return;
        }
        if (e.Key is Key.Up or Key.Down)
        {
            var step = e.Key == Key.Down ? 1 : -1;
            _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + step, 0, _list.ItemCount - 1);
            FocusSelected();
            e.Handled = true;
            return;
        }
        if (_list.SelectedItem is not ClipboardEntry entry)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            _ = _vm.CopyAsync(entry.Id);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var index = _list.SelectedIndex;
            _vm.Delete(entry.Id);
            _list.SelectedIndex = Math.Min(index, _list.ItemCount - 1);
            e.Handled = true;
        }
    }

    // A click on a row (not on one of its buttons) copies it.
    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is not null && e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: ClipboardEntry entry })
        {
            _ = _vm.CopyAsync(entry.Id);
        }
    }

    // The list itself takes no focus: its rows do. Keep focus on the selected row so the keys keep working.
    private void FocusSelected()
    {
        if (_list.SelectedIndex >= 0)
        {
            _list.ScrollIntoView(_list.SelectedIndex);
            _list.ContainerFromIndex(_list.SelectedIndex)?.Focus();
        }
    }

    private void CopyLatest()
    {
        if (_vm?.Latest is { } latest)
        {
            _ = _vm.CopyAsync(latest.Id);
        }
    }

    // Showing ----------------------------------------------------------------------------------------------------------------

    // Which part shows: the options when open; the explanation when off with nothing recorded; otherwise the latest copy (small)
    // or the list. The toolbar shows the actions that apply now.
    private void ShowState()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        var options = _options.IsVisible;
        var hasItems = vm.Items.Count > 0;
        var intro = !options && !vm.IsRecording && !hasItems;
        _intro.IsVisible = intro;
        _latest.IsVisible = !options && !intro && !Layout.ShowList;
        _listArea.IsVisible = !options && !intro && Layout.ShowList;
        _search.IsVisible = Layout.ShowSearch;

        _record.IsVisible = !vm.IsRecording && (hasItems || options);
        _pause.IsVisible = _stop.IsVisible = vm.IsRecording;
        ((Path)_pause.Content!).Data = Geometry.Parse(vm.IsPaused ? ResumeIcon : PauseIcon);
        var pauseName = vm.IsPaused ? "Resume recording" : "Pause recording";
        AutomationProperties.SetName(_pause, pauseName);
        ToolTip.SetTip(_pause, pauseName);
        _clear.IsVisible = hasItems;
        // The state as a word, or as a dot where the toolbar has little room (its name still says it).
        var state = !vm.IsRecording ? "Off" : vm.IsPaused ? "Paused" : "Recording";
        var word = Bounds.Width == 0 || Bounds.Width >= StatusWordMinWidth;
        _status.Text = state;
        _status.IsVisible = word;
        _statusDot.IsVisible = !word;
        AutomationProperties.SetName(_statusDot, state);
        ToolTip.SetTip(_statusDot, state);
        _status.Classes.Set("clipRec", vm.IsRecording && !vm.IsPaused);
        _statusDot.Classes.Set("clipRec", vm.IsRecording && !vm.IsPaused);
        if (_options.IsVisible)
        {
            ShowOptions();
        }
        UpdateAgesTimer();
    }

    private void ShowItems()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        var selected = (_list.SelectedItem as ClipboardEntry)?.Id;
        var focused = _list.IsKeyboardFocusWithin;
        _list.ItemsSource = null; // the same list, changed in place: set it again so the rows are rebuilt
        _list.ItemsSource = vm.Visible;
        if (selected is { } id)
        {
            for (var i = 0; i < vm.Visible.Count; i++)
            {
                if (vm.Visible[i].Id == id)
                {
                    _list.SelectedIndex = i;
                    break;
                }
            }
        }
        if (focused)
        {
            FocusSelected();
        }
        _empty.IsVisible = vm.Visible.Count == 0;
        _empty.Text = vm.Items.Count == 0 ? "Copy some text and it shows up here." : "No items match the search.";

        if (vm.Latest is { } latest)
        {
            _latestText.Text = latest.Preview;
            _count.Text = $"{Plural(vm.Items.Count, "item")} · {Plural(latest.Text.Length, "char")}";
            AutomationProperties.SetName(_latest, "Copy the latest item: " + Describe(latest));
        }
        else
        {
            _latestText.Text = "Nothing copied yet";
            _count.Text = vm.IsRecording ? "Recording" : "Off";
            AutomationProperties.SetName(_latest, "No items yet");
        }
        ShowState();
    }

    private void ShowOptions()
    {
        if (_vm is not { } vm)
        {
            return;
        }
        _syncing = true;
        _keep.IsChecked = vm.KeepHistory;
        _maxItems.Value = vm.MaxItems;
        _maxLength.Value = vm.MaxLength;
        _syncing = false;
        _keepNote.Text = vm.KeepHistory
            ? $"Saved on this computer only, in {vm.StorageLocation}. Turn this off to delete the saved file."
            : "History is kept in memory only and is gone when Chisel closes.";
    }

    // The relative times ("2 min ago") are redrawn once a minute, only while they are shown.
    private void UpdateAgesTimer()
    {
        var run = _vm is not null && Layout.ShowTimes && _listArea.IsVisible && _vm.Items.Count > 0;
        if (run != _ages.IsEnabled)
        {
            _ages.IsEnabled = run;
        }
    }

    private string Describe(ClipboardEntry entry)
    {
        var age = _vm is null ? "" : ", " + RelativeTime.Format(_vm.Now - entry.CopiedAt);
        return $"{entry.Preview}, {entry.Text.Length} characters{(entry.Pinned ? ", pinned" : "")}{age}";
    }

    private static string Plural(int n, string word) => $"{n:N0} {word}{(n == 1 ? "" : "s")}";

    private Control BuildRow(ClipboardEntry entry)
    {
        var layout = Layout;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), Height = layout.RowHeight, Background = Brushes.Transparent };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = entry.Preview, FontSize = Font(layout.PreviewFont), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
        });
        if (layout.ShowTimes && _vm is not null)
        {
            var meta = new TextBlock
            {
                Text = $"{RelativeTime.Format(_vm.Now - entry.CopiedAt)} · {Plural(entry.Text.Length, "char")}",
                FontSize = Font(layout.MetaFont), TextTrimming = TextTrimming.CharacterEllipsis,
            };
            meta.Classes.Add("cdDim");
            text.Children.Add(meta);
        }
        row.Children.Add(text);

        var column = 1;
        if (!layout.ShowTimes)
        {
            var length = new TextBlock { Text = entry.Text.Length.ToString("N0"), FontSize = Font(layout.MetaFont), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            length.Classes.Add("cdDim");
            ToolTip.SetTip(length, Plural(entry.Text.Length, "character"));
            Grid.SetColumn(length, column);
            row.Children.Add(length);
        }
        column = 2;
        var size = Math.Max(16, layout.RowHeight * 0.6);
        foreach (var (icon, name, action) in new (string, string, Action)[]
        {
            (CopyIcon, "Copy", () => _ = _vm?.CopyAsync(entry.Id)),
            (PinIcon, entry.Pinned ? "Unpin" : "Pin", () => _vm?.TogglePin(entry.Id)),
            (DeleteIcon, "Delete", () => _vm?.Delete(entry.Id)),
        })
        {
            var button = new Button { Content = Icon(icon, size * 0.55), Width = size, Height = size, Margin = new Thickness(2, 0, 0, 0) };
            button.Classes.Add("clipAction");
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => action();
            Grid.SetColumn(button, column++);
            row.Children.Add(button);
        }
        if (entry.Pinned)
        {
            var pin = Icon(PinIcon, Font(layout.MetaFont));
            pin.Classes.Add("clipPinned");
            pin.Margin = new Thickness(6, 0, 0, 0);
            pin.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pin, 1);
            row.Children.Add(pin);
        }
        return row;
    }

    private ClipboardDetail? _templateDetail;
    private double _templateScale;

    private void ApplyLayout()
    {
        var layout = Layout = ClipboardLayout.For(Bounds.Width, Bounds.Height);
        _root.Margin = new Thickness(layout.Pad);

        _introText.FontSize = Font(layout.IntroFont);
        _introText.MaxLines = layout.IntroLines;
        _introText.IsVisible = layout.IntroLines > 0;
        _introText.Margin = new Thickness(0, 0, 0, layout.Gap);
        _start.Height = layout.ButtonHeight;
        _start.FontSize = Font(layout.ButtonFont);
        _start.Padding = new Thickness(12 * layout.Scale, 0);

        _latest.Height = layout.RowHeight;
        _latestText.FontSize = Font(layout.PreviewFont);
        _count.FontSize = Font(layout.MetaFont);

        _search.Height = layout.SearchHeight;
        _search.FontSize = Font(layout.PreviewFont);
        _search.Margin = new Thickness(0, 0, 0, layout.Gap);
        _empty.FontSize = Font(layout.PreviewFont);
        _copied.FontSize = Font(layout.MetaFont);

        // Rows are built for one size: rebuild them when the size or the detail changes.
        if (_templateDetail != layout.Detail || Math.Abs(_templateScale - layout.Scale) > 0.01)
        {
            _templateDetail = layout.Detail;
            _templateScale = layout.Scale;
            _list.ItemTemplate = new FuncDataTemplate<ClipboardEntry>((entry, _) => entry is null ? new Panel() : BuildRow(entry));
        }
        ShowItems();
    }

    private static double Font(double size) => Math.Max(1, size);
}
