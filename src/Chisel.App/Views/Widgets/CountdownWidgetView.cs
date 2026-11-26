using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Chisel.App.Theming;
using Chisel.App.ViewModels;
using Chisel.Core.Countdown;

namespace Chisel.App.Views.Widgets;

// The countdown widget: the nearest event large at the top (the hero), the other events in rows under it, and a "+N more" line
// for those that do not fit. What is shown and every size come from CountdownLayout. "+ Add" (and clicking an event) opens an
// inline form in place of the list. The widget tells the view model it is watching only while it is on screen, so the minute
// updates for hours and minutes stop when it is hidden, minimized or removed.
public sealed class CountdownWidgetView : WidgetView
{
    private static readonly CountdownColor[] Swatches = Enum.GetValues<CountdownColor>();

    private readonly Grid _main = new() { Name = "CountdownMain", RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
    private readonly Button _hero;
    private readonly TextBlock _heroMarker = new() { Name = "CountdownHeroEmoji", VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _heroDot = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _heroLead = new() { Name = "CountdownHeroLead", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _heroLabel = new() { Name = "CountdownHeroLabel", TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _heroDate = new() { Name = "CountdownHeroDate", TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ProgressStrip _heroProgress = new() { Name = "CountdownHeroProgress" };
    private readonly StackPanel _rows = new() { Name = "CountdownRows" };
    private readonly TextBlock _more = new() { Name = "CountdownMore", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _empty = new()
    {
        Name = "CountdownEmpty", Text = "No events yet.\nPress + Add to start counting down.", TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsVisible = false,
    };

    private readonly ScrollViewer _form = new() { Name = "CountdownForm", IsVisible = false };
    private readonly TextBox _name = new() { Name = "CountdownName", PlaceholderText = "Event name", MaxLength = CountdownValidation.MaxNameLength };
    private readonly CalendarDatePicker _date = new() { Name = "CountdownDate", HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Date" };
    private readonly TextBox _time = new() { Name = "CountdownTime", PlaceholderText = "Time (optional), e.g. 14:30" };
    private readonly TextBox _emoji = new() { Name = "CountdownEmoji", PlaceholderText = "Emoji (optional)" };
    // Wraps on a narrow card so every color stays on screen.
    private readonly WrapPanel _swatches = new() { Name = "CountdownColors", ItemSpacing = 4, LineSpacing = 4 };
    private readonly CheckBox _yearly = new() { Name = "CountdownYearly", Content = "Repeats every year" };
    private readonly TextBlock _error = new() { Name = "CountdownError", TextWrapping = TextWrapping.Wrap };
    private readonly Button _delete;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private CountdownColor _formColor;
    private readonly Button _add;
    private double _addRow = 44; // the "+ Add" row's height with its margin, measured once it is shown

    private CountdownViewModel? _countdown;
    private IDisposable? _windowState;
    private bool _minimized;
    private bool _watching;

    public CountdownLayout Layout { get; private set; } = CountdownLayout.For(0, 0, 0);

    public CountdownWidgetView()
    {
        ClipToBounds = true;
        Margin = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Countdown");

        _add = new Button { Name = "CountdownAdd", Content = "+ Add" };
        _add.Classes.Add("accent");
        _add.Click += (_, _) => _countdown?.OpenAdd();
        AutomationProperties.SetName(_add, "Add an event");
        BodyActions = _add;

        // The hero: emoji or color dot and the big number, then the label and the date, then (Detailed) its progress.
        _heroLead.Classes.Add("cdLead");
        _heroDate.Classes.Add("cdDim");
        _heroLabel.Classes.Add("cdDim");
        var leadLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8 };
        leadLine.Children.Add(_heroMarker);
        leadLine.Children.Add(_heroDot);
        leadLine.Children.Add(_heroLead);
        var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heroStack.Children.Add(leadLine);
        heroStack.Children.Add(_heroLabel);
        heroStack.Children.Add(_heroDate);
        heroStack.Children.Add(_heroProgress);
        _hero = new Button { Name = "CountdownHero", Content = heroStack };
        _hero.Classes.Add("cdRow");
        _hero.Click += (_, _) => Edit(_countdown?.Nearest);

        Grid.SetRow(_rows, 1);
        Grid.SetRow(_more, 2);
        _more.Classes.Add("cdDim");
        _empty.Classes.Add("cdDim");
        _main.Children.Add(_hero);
        _main.Children.Add(_rows);
        _main.Children.Add(_more);

        _delete = FormButton("CountdownDelete", "Delete", () => _countdown?.DeleteEditing());
        _moveUp = FormButton("CountdownMoveUp", "Move up", () => Move(-1));
        _moveDown = FormButton("CountdownMoveDown", "Move down", () => Move(1));
        _form.Content = BuildForm();

        var root = new Panel();
        root.Children.Add(_main);
        root.Children.Add(_empty);
        root.Children.Add(_form);
        Content = root;
    }

    private Control BuildForm()
    {
        foreach (var color in Swatches)
        {
            var swatch = new Button { Tag = color, Content = new Ellipse { Width = 14, Height = 14 } };
            swatch.Classes.Add("cdSwatch");
            AutomationProperties.SetName(swatch, color == CountdownColor.None ? "No color" : color.ToString());
            swatch.Click += (_, _) => ChooseColor(color);
            _swatches.Children.Add(swatch);
        }
        _error.Classes.Add("cdError");
        var save = FormButton("CountdownSave", "Save", SaveForm);
        save.Classes.Add("accent");
        var cancel = FormButton("CountdownCancel", "Cancel", () => _countdown?.Cancel());
        var buttons = new WrapPanel();
        foreach (var b in new[] { save, cancel, _delete, _moveUp, _moveDown })
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            buttons.Children.Add(b);
        }
        var emojiAndColor = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        _emoji.Width = 130;
        emojiAndColor.Children.Add(_emoji);
        emojiAndColor.Children.Add(_swatches);
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var c in new Control[] { _name, _date, _time, emojiAndColor, _yearly, _error, buttons })
        {
            stack.Children.Add(c);
        }
        AutomationProperties.SetName(_name, "Event name");
        AutomationProperties.SetName(_date, "Date");
        AutomationProperties.SetName(_time, "Time, optional");
        AutomationProperties.SetName(_emoji, "Emoji, optional");
        return stack;
    }

    private static Button FormButton(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text };
        button.Click += (_, _) => click();
        return button;
    }

    // Lifecycle --------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _countdown = (DataContext as MainViewModel)?.Countdown;
        if (_countdown is null)
        {
            return;
        }
        _countdown.PropertyChanged += OnChanged;
        if (TopLevel.GetTopLevel(this) is Window window)
        {
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
        if (_countdown is not null)
        {
            _countdown.PropertyChanged -= OnChanged;
            _countdown = null;
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

    private sealed class StateObserver(CountdownWidgetView view) : IObserver<WindowState>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(WindowState value)
        {
            view._minimized = value == WindowState.Minimized;
            view.Follow();
        }
    }

    private void Follow() => Watch(_countdown is not null && IsEffectivelyVisible && !_minimized);

    private void Watch(bool on)
    {
        if (on == _watching || _countdown is null)
        {
            return;
        }
        _watching = on;
        if (on)
        {
            _countdown.Attach(this);
        }
        else
        {
            _countdown.Detach(this);
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CountdownViewModel.IsFormOpen) && _countdown is { IsFormOpen: true })
        {
            LoadForm();
        }
        if (e.PropertyName is nameof(CountdownViewModel.Items) or nameof(CountdownViewModel.IsFormOpen) or nameof(CountdownViewModel.FormError))
        {
            Update();
        }
    }

    // Showing ----------------------------------------------------------------------------------------------------------------

    private bool Dark => ActualThemeVariant == ThemeVariant.Dark;

    private void Update()
    {
        if (_countdown is not { } countdown)
        {
            return;
        }
        var form = countdown.IsFormOpen;
        _form.IsVisible = form;
        _empty.IsVisible = !form && countdown.IsEmpty;
        _main.IsVisible = !form && !countdown.IsEmpty;
        _error.Text = countdown.FormError ?? "";
        _error.IsVisible = countdown.FormError is not null;
        _delete.IsVisible = countdown.IsEditing;
        _moveUp.IsVisible = _moveDown.IsVisible = countdown.IsEditing && countdown.IsCustomOrder;

        // The size is decided as if the "+ Add" row were shown; in Single it is hidden to give the one event the room.
        if (_add.IsVisible && _add.Bounds.Height > 0)
        {
            _addRow = _add.Bounds.Height + _add.Margin.Top + _add.Margin.Bottom;
        }
        var size = CountdownLayout.SizeFor(Bounds.Width, Bounds.Height - (_add.IsVisible ? 0 : _addRow));
        _add.IsVisible = form || countdown.IsEmpty || size != CountdownSize.Single;
        var layout = Layout = CountdownLayout.For(Bounds.Width, Bounds.Height, countdown.Others.Count, size);
        ((Panel)Content!).Margin = new Thickness(layout.Pad);
        if (countdown.Nearest is not { } nearest)
        {
            return;
        }

        // The hero.
        var room = Math.Max(1, Bounds.Width - 2 * layout.Pad - 12);
        _hero.Height = layout.HeroHeight;
        _hero.Classes.Set("today", nearest.IsToday);
        AutomationProperties.SetName(_hero, nearest.AutomationName);
        Marker(_heroMarker, _heroDot, nearest.Entry, layout.HeroNumberFont * 0.6);
        _heroLead.Text = nearest.Status.Lead;
        _heroLead.FontSize = layout.HeroNumberFont;
        FitWidth(_heroLead, room - (_heroMarker.IsVisible || _heroDot.IsVisible ? layout.HeroNumberFont * 0.6 + 8 : 0));
        var rest = nearest.Status.Rest;
        _heroLabel.Text = rest.Length > 0 ? $"{rest} · {nearest.Entry.Name}" : nearest.Entry.Name;
        _heroLabel.FontSize = layout.HeroLabelFont;
        _heroLabel.MaxWidth = room;
        _heroDate.Text = nearest.DateText;
        _heroDate.FontSize = Math.Max(1, layout.HeroLabelFont * 0.85);
        _heroDate.MaxWidth = room;
        _heroProgress.IsVisible = layout.ShowProgress;
        _heroProgress.Height = layout.ProgressHeight;
        _heroProgress.Width = Math.Min(room, 320);
        _heroProgress.Margin = new Thickness(0, layout.Gap / 2, 0, 0);
        _heroProgress.Fraction = nearest.Progress;
        _heroProgress.IsToday = nearest.IsToday;

        // The rows that fit, then how many did not.
        _rows.Children.Clear();
        _rows.Margin = new Thickness(0, layout.VisibleRows > 0 ? layout.Gap : 0, 0, 0);
        for (var i = 0; i < layout.VisibleRows; i++)
        {
            _rows.Children.Add(Row(countdown.Others[i], layout));
        }
        var hidden = countdown.Others.Count - layout.VisibleRows;
        _more.IsVisible = layout.ShowMore && hidden > 0;
        _more.Text = $"+{hidden} more";
        _more.FontSize = layout.RowSmallFont;
        _more.Height = layout.MoreHeight;
        AutomationProperties.SetName(_more, $"{hidden} more events");
    }

    private Control Row(CountdownItem item, CountdownLayout layout)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
        var marker = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = layout.RowNameFont, Margin = new Thickness(0, 0, 8, 0) };
        var dot = new Ellipse { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Marker(marker, dot, item.Entry, layout.RowNameFont * 0.7);
        var markers = new Panel();
        markers.Children.Add(marker);
        markers.Children.Add(dot);
        Grid.SetRowSpan(markers, 2);
        var name = new TextBlock { Text = item.Entry.Name, FontSize = layout.RowNameFont, TextTrimming = TextTrimming.CharacterEllipsis };
        name.Classes.Add("cdName");
        Grid.SetColumn(name, 1);
        var when = new TextBlock { Text = item.Status.Text, FontSize = layout.RowNameFont, Margin = new Thickness(8, 0, 0, 0) };
        when.Classes.Add("cdWhen");
        Grid.SetColumn(when, 2);
        var date = new TextBlock { Text = item.DateText, FontSize = layout.RowSmallFont, TextTrimming = TextTrimming.CharacterEllipsis };
        date.Classes.Add("cdDim");
        Grid.SetColumn(date, 1);
        Grid.SetColumnSpan(date, 2);
        Grid.SetRow(date, 1);
        grid.Children.Add(markers);
        grid.Children.Add(name);
        grid.Children.Add(when);
        grid.Children.Add(date);
        if (layout.ShowProgress)
        {
            var progress = new ProgressStrip
            {
                Fraction = item.Progress, IsToday = item.IsToday, Height = layout.ProgressHeight,
                Margin = new Thickness(0, layout.RowNameFont * 0.35, 0, 0),
            };
            Grid.SetRow(progress, 2);
            Grid.SetColumnSpan(progress, 3);
            grid.Children.Add(progress);
        }
        var row = new Button { Name = "CountdownRow", Content = grid, Height = layout.RowHeight };
        row.Classes.Add("cdRow");
        row.Classes.Set("today", item.IsToday);
        row.Classes.Set("past", item.IsPast);
        AutomationProperties.SetName(row, item.AutomationName);
        row.Click += (_, _) => Edit(item);
        return row;
    }

    // An emoji if the event has one, else its color dot, else nothing.
    private void Marker(TextBlock emoji, Ellipse dot, CountdownEntry entry, double size)
    {
        emoji.Text = entry.Emoji ?? "";
        emoji.IsVisible = entry.Emoji is not null;
        if (ReferenceEquals(emoji, _heroMarker))
        {
            emoji.FontSize = size;
        }
        dot.IsVisible = entry.Emoji is null && entry.Color != CountdownColor.None;
        dot.Width = dot.Height = Math.Max(6, size * 0.5);
        dot.Fill = new SolidColorBrush(ThemeColors.CountdownTag(entry.Color, Dark));
    }

    // Shrinks a text's font until it fits the width (a word like "Tomorrow" is wider than a number).
    private static void FitWidth(TextBlock text, double width)
    {
        text.Measure(Size.Infinity);
        if (text.DesiredSize.Width > width && text.DesiredSize.Width > 0)
        {
            text.FontSize = Math.Max(1, Math.Floor(text.FontSize * width / text.DesiredSize.Width));
        }
    }

    // Form -------------------------------------------------------------------------------------------------------------------

    private void Edit(CountdownItem? item)
    {
        if (item is not null && FindAncestorFrame() is not { IsEditing: true })
        {
            _countdown?.OpenEdit(item.Entry.Id);
        }
    }

    private WidgetFrame? FindAncestorFrame() => Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<WidgetFrame>(this);

    private void LoadForm()
    {
        var c = _countdown!;
        _name.Text = c.FormName;
        _date.SelectedDate = c.FormDate;
        _time.Text = c.FormTime;
        _emoji.Text = c.FormEmoji;
        _yearly.IsChecked = c.FormYearly;
        ChooseColor(c.FormColor);
        _form.Offset = default;
    }

    private void ChooseColor(CountdownColor color)
    {
        _formColor = color;
        foreach (var child in _swatches.Children)
        {
            if (child is Button { Tag: CountdownColor c, Content: Ellipse ellipse } swatch)
            {
                swatch.Classes.Set("selected", c == color);
                ellipse.Fill = new SolidColorBrush(ThemeColors.CountdownTag(c, Dark));
                ellipse.Stroke = c == CountdownColor.None ? ThemeService.Brush(this, "DimTextBrush") : null;
                ellipse.StrokeThickness = 1.2;
            }
        }
    }

    private void SaveForm()
    {
        if (_countdown is not { } c)
        {
            return;
        }
        c.FormName = _name.Text ?? "";
        c.FormDate = _date.SelectedDate;
        c.FormTime = _time.Text ?? "";
        c.FormEmoji = _emoji.Text ?? "";
        c.FormColor = _formColor;
        c.FormYearly = _yearly.IsChecked == true;
        c.Save();
        Update();
    }

    private void Move(int delta)
    {
        if (_countdown is { EditingId: { } id } c)
        {
            c.Move(id, delta);
        }
    }
}

// A thin rounded progress bar drawn in one pass: the track, then the filled part in the accent.
public sealed class ProgressStrip : Control
{
    private double _fraction;

    public double Fraction
    {
        get => _fraction;
        set
        {
            _fraction = value;
            AutomationProperties.SetHelpText(this, $"{Math.Round(Math.Clamp(value, 0, 1) * 100)} percent of the wait has passed");
            InvalidateVisual();
        }
    }

    public bool IsToday { get; set; }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = bounds.Height / 2;
        context.DrawRectangle(ThemeService.Brush(this, "GridLineBrush"), null, bounds, radius, radius);
        var fill = Math.Clamp(Fraction, 0, 1) * bounds.Width;
        if (fill > 0)
        {
            context.DrawRectangle(ThemeService.Brush(this, IsToday ? "AccentTextBrush" : "AccentBrush"), null,
                new Rect(0, 0, Math.Max(fill, bounds.Height), bounds.Height), radius, radius);
        }
    }
}
