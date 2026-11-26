using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The daily verse widget: a quiet reading card. The verse comes first at a readable size, with the reference and "KJV"
// underneath and two small text actions (Copy, Read in context). The text is never shrunk to fit: when it does not fit
// the card it is cut with an ellipsis and a "Read more" action opens an expanded view, the full text in a scrolling area
// that fills the card ("Show less" goes back). A card too short for even one line scrolls everything instead. While the layout is being edited the card is only a drag handle.
public sealed class ScriptureWidgetView : WidgetView
{
    private const double MaxTextWidth = 680;
    private const double FooterRowWidth = 420;

    private MainViewModel? _main;
    private ScriptureViewModel? _scripture;
    private bool _expanded;

    public ScriptureWidgetView()
    {
        Padding = new Thickness(18, 10, 18, 8);
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel main)
        {
            _main = main;
            _scripture = main.Scripture;
            _scripture.PropertyChanged += OnScriptureChanged;
            _scripture.WriteClipboard = WriteClipboard;
            main.Layout.PropertyChanged += OnLayoutChanged;
            IsHitTestVisible = !main.Layout.IsEditing;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scripture is not null && _main is not null)
        {
            _scripture.PropertyChanged -= OnScriptureChanged;
            _main.Layout.PropertyChanged -= OnLayoutChanged;
            _scripture.WriteClipboard = null;
            _scripture = null;
            _main = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Refresh();
    }

    private void OnScriptureChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.IsEditing) && _main is not null)
        {
            IsHitTestVisible = !_main.Layout.IsEditing;
        }
    }

    private void WriteClipboard(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    // A hidden tab (another widget of its group is showing) draws nothing: it is rebuilt when it is shown again.
    private bool _stale;

    private void Refresh()
    {
        if (IsEffectivelyVisible)
        {
            _stale = false;
            Rebuild();
        }
        else
        {
            _stale = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_stale && change.Property.Name == nameof(IsEffectivelyVisible) && IsEffectivelyVisible)
        {
            Refresh();
        }
    }

    private IBrush Brush(string key) => Theming.ThemeService.Brush(this, key);

    private static double VerseSize(double width) => width >= 520 ? 22 : width >= 380 ? 19 : 16;

    private TextBlock VerseText(double size, double width) =>
        new()
        {
            Name = "ScriptureText", Text = _scripture!.Text, FontSize = size, LineHeight = Math.Round(size * 1.4),
            TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Min(width, MaxTextWidth), HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = Brush("TextBrush"), FontFamily = FontFamily,
        };

    private Button Action(string name, string label, Action click)
    {
        var button = new Button { Name = name, Content = label };
        button.Classes.Add("verseAction");
        button.Click += (_, _) => click();
        return button;
    }

    private void Rebuild()
    {
        if (_scripture is null)
        {
            return;
        }
        var width = Bounds.Width - Padding.Left - Padding.Right;
        var height = Bounds.Height - Padding.Top - Padding.Bottom;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var size = VerseSize(width);

        // Does the whole verse fit above the footer?
        var room = height - FooterHeight(width) - 6;
        var probe = VerseText(size, width);
        probe.Measure(new Size(width, double.PositiveInfinity));
        var fits = probe.DesiredSize.Height <= room + 0.5;
        if (fits)
        {
            _expanded = false;
        }

        // Too short for even one line of verse above the footer: everything scrolls, so the verse is read at full size.
        if (room < probe.LineHeight)
        {
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(VerseText(size, width));
            stack.Children.Add(BuildFooter(width, canExpand: false, expanded: false));
            Content = new ScrollViewer
            {
                Name = "ScriptureExpanded",
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = stack,
            };
            return;
        }

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Control body;
        if (_expanded)
        {
            body = new ScrollViewer
            {
                Name = "ScriptureExpanded",
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = VerseText(size, width),
            };
        }
        else
        {
            var text = VerseText(size, width);
            if (!fits)
            {
                var lines = Math.Max(1, (int)Math.Floor(room / text.LineHeight));
                text.MaxLines = lines;
                text.Text = ClipToLines(_scripture.Text, size, width, lines);
            }
            text.VerticalAlignment = fits ? VerticalAlignment.Center : VerticalAlignment.Top;
            body = text;
        }
        root.Children.Add(body);
        var foot = BuildFooter(width, canExpand: !fits, expanded: _expanded);
        foot.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(foot, 1);
        root.Children.Add(foot);
        Content = root;
    }

    // The longest start of the verse, cut at a word, that fits in the given lines once "…" is added. A wrapped TextBlock
    // does not draw its own ellipsis, so the cut is made here and is always visible.
    private string ClipToLines(string full, double size, double width, int lines)
    {
        var probe = VerseText(size, width);
        bool Fits(string candidate)
        {
            probe.Text = candidate;
            probe.Measure(new Size(width, double.PositiveInfinity));
            return probe.DesiredSize.Height <= lines * probe.LineHeight + 0.5;
        }
        string Cut(int length)
        {
            var at = full.LastIndexOf(' ', Math.Max(0, length - 1));
            return full[..(at > 0 ? at : length)].TrimEnd(',', ';', ':', ' ') + "…";
        }
        int low = 0, high = full.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (Fits(Cut(middle)))
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return Cut(low);
    }

    // The footer's height: one row in a wide card, two in a narrow one (reference, then actions). Fixed numbers, as a control
    // that is not on screen yet has no template to measure.
    private static double FooterHeight(double width) => width >= FooterRowWidth ? 21 : 38;

    // Reference and "KJV" on the left, the actions on the right; in a narrow card the actions go on a second line.
    private Control BuildFooter(double width, bool canExpand, bool expanded)
    {
        var scripture = _scripture!;
        var reference = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        reference.Children.Add(new TextBlock
        {
            Name = "ScriptureReference", Text = scripture.Reference, FontSize = 13, FontWeight = FontWeight.SemiBold,
            Foreground = Brush("DimTextBrush"), TextTrimming = TextTrimming.CharacterEllipsis,
        });
        reference.Children.Add(new TextBlock
        {
            Name = "ScriptureTranslation", Text = scripture.Translation, FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = Brush("DimTextBrush"), Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        if (expanded)
        {
            actions.Children.Add(Action("ScriptureLess", "Show less", () => { _expanded = false; Refresh(); }));
        }
        else if (canExpand)
        {
            actions.Children.Add(Action("ScriptureMore", "Read more", () => { _expanded = true; Refresh(); }));
        }
        actions.Children.Add(Action("ScriptureCopy", scripture.IsCopied ? "Copied" : "Copy", () => scripture.CopyCommand.Execute(null)));
        actions.Children.Add(Action("ScriptureContext", "Read in context", () => scripture.ReadInContextCommand.Execute(null)));

        if (width >= FooterRowWidth)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(reference);
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);
            return row;
        }
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(reference);
        stack.Children.Add(actions);
        return stack;
    }
}
