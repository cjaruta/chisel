using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.Core.Calculator;

namespace Chisel.App.Views.Widgets;

// The calculator widget: a display over a 4-column keypad, with, when the setting is on and the widget is wide enough, the advanced
// panel to the left of the keypad in the same grid (no divider). Nothing has a fixed size: the gaps, key sizes and text all come from
// CalculatorLayout, worked out from the widget's actual size (see CalculatorViewModel.Resize), and the display text shrinks to fit.
// Keys are plain buttons that do not take focus; the widget itself does, and handles the keyboard (digits and operators, Enter,
// Backspace, Escape, Ctrl+C). In edit mode the frame makes the body inert so the card drags like the others.
public sealed class CalculatorWidgetView : WidgetView
{
    private const double OperatorScale = 1.2;

    private enum KeyKind { Number, Function, Operator, Equals, Advanced }

    private readonly record struct KeyDef(CalcKey Key, string Label, KeyKind Kind, int Row, int Column, int Span = 1);

    private static KeyDef[] BasicKeys(string decimalSeparator) =>
    [
        new(CalcKey.Clear, "AC", KeyKind.Function, 0, 0), new(CalcKey.Negate, "±", KeyKind.Function, 0, 1),
        new(CalcKey.Percent, "%", KeyKind.Function, 0, 2), new(CalcKey.Divide, "÷", KeyKind.Operator, 0, 3),
        new(CalcKey.Digit7, "7", KeyKind.Number, 1, 0), new(CalcKey.Digit8, "8", KeyKind.Number, 1, 1),
        new(CalcKey.Digit9, "9", KeyKind.Number, 1, 2), new(CalcKey.Multiply, "×", KeyKind.Operator, 1, 3),
        new(CalcKey.Digit4, "4", KeyKind.Number, 2, 0), new(CalcKey.Digit5, "5", KeyKind.Number, 2, 1),
        new(CalcKey.Digit6, "6", KeyKind.Number, 2, 2), new(CalcKey.Subtract, "−", KeyKind.Operator, 2, 3),
        new(CalcKey.Digit1, "1", KeyKind.Number, 3, 0), new(CalcKey.Digit2, "2", KeyKind.Number, 3, 1),
        new(CalcKey.Digit3, "3", KeyKind.Number, 3, 2), new(CalcKey.Add, "+", KeyKind.Operator, 3, 3),
        new(CalcKey.Backspace, "⌫", KeyKind.Function, 4, 0), new(CalcKey.Digit0, "0", KeyKind.Number, 4, 1),
        new(CalcKey.Decimal, decimalSeparator, KeyKind.Number, 4, 2), new(CalcKey.Equals, "=", KeyKind.Equals, 4, 3),
    ];

    // The advanced panel: 5 columns by 5 rows, the angle key spanning the end of the last row.
    private static readonly KeyDef[] AdvancedKeys =
    [
        new(CalcKey.MemoryClear, "MC", KeyKind.Advanced, 0, 0), new(CalcKey.MemoryRecall, "MR", KeyKind.Advanced, 0, 1),
        new(CalcKey.MemoryAdd, "M+", KeyKind.Advanced, 0, 2), new(CalcKey.MemorySubtract, "M−", KeyKind.Advanced, 0, 3),
        new(CalcKey.Pi, "π", KeyKind.Advanced, 0, 4),
        new(CalcKey.OpenParen, "(", KeyKind.Advanced, 1, 0), new(CalcKey.CloseParen, ")", KeyKind.Advanced, 1, 1),
        new(CalcKey.Factorial, "n!", KeyKind.Advanced, 1, 2), new(CalcKey.Reciprocal, "1/x", KeyKind.Advanced, 1, 3),
        new(CalcKey.E, "e", KeyKind.Advanced, 1, 4),
        new(CalcKey.Sin, "sin", KeyKind.Advanced, 2, 0), new(CalcKey.Cos, "cos", KeyKind.Advanced, 2, 1),
        new(CalcKey.Tan, "tan", KeyKind.Advanced, 2, 2), new(CalcKey.Ln, "ln", KeyKind.Advanced, 2, 3),
        new(CalcKey.Log, "log", KeyKind.Advanced, 2, 4),
        new(CalcKey.Asin, "sin⁻¹", KeyKind.Advanced, 3, 0), new(CalcKey.Acos, "cos⁻¹", KeyKind.Advanced, 3, 1),
        new(CalcKey.Atan, "tan⁻¹", KeyKind.Advanced, 3, 2), new(CalcKey.Sqrt, "√", KeyKind.Advanced, 3, 3),
        new(CalcKey.Square, "x²", KeyKind.Advanced, 3, 4),
        new(CalcKey.Power, "xʸ", KeyKind.Advanced, 4, 0), new(CalcKey.ToggleAngle, "", KeyKind.Advanced, 4, 1, 4),
    ];

    private readonly Grid _root = new() { Name = "CalculatorRoot" };
    private readonly Grid _keys = new() { Name = "CalculatorKeys" };
    private readonly Grid _advancedPanel = new() { Name = "CalculatorAdvancedPanel" };
    private readonly Panel _resultHost = new() { ClipToBounds = true };
    private readonly TextBlock _display = new() { Name = "CalculatorDisplay", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _secondary = new() { Name = "CalculatorSecondary", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _memory = new() { Name = "CalculatorMemory", Text = "M", IsVisible = false };
    private readonly TextBlock _angle = new() { Name = "CalculatorAngle" };
    private readonly List<(Button Button, TextBlock Text, KeyKind Kind)> _buttons = [];
    private TextBlock? _angleKeyText;
    private CalculatorViewModel? _calc;
    private bool _builtWithPanel;
    private string _shownText = "";

    public CalculatorWidgetView()
    {
        Focusable = true;
        ClipToBounds = true;
        // The card scrolls this view when it is too short (see MinHeight); it fills the card otherwise, with no margin of its own.
        Margin = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Stretch;

        _root.RowDefinitions = new RowDefinitions("Auto,*");
        var indicators = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        indicators.Children.Add(_memory);
        indicators.Children.Add(_angle);
        var secondaryRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        secondaryRow.Children.Add(indicators);
        Grid.SetColumn(_secondary, 1);
        secondaryRow.Children.Add(_secondary);
        _resultHost.Children.Add(_display);
        var displayArea = new Grid { Name = "CalculatorDisplayArea", RowDefinitions = new RowDefinitions("Auto,*"), IsHitTestVisible = false };
        displayArea.Children.Add(secondaryRow);
        Grid.SetRow(_resultHost, 1);
        displayArea.Children.Add(_resultHost);
        _root.Children.Add(displayArea);
        Grid.SetRow(_keys, 1);
        _root.Children.Add(_keys);
        Content = _root;

        AddHandler(PointerPressedEvent, (_, _) => Focus(), handledEventsToo: true);
        // The window binds Escape (close what floats over it) and handles it before the bubble phase, so it is taken here.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _calc = (DataContext as MainViewModel)?.Calculator;
        if (_calc is null)
        {
            return;
        }
        _calc.PropertyChanged += OnCalculatorChanged;
        ReportSize();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_calc is not null)
        {
            _calc.PropertyChanged -= OnCalculatorChanged;
            _calc = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ReportSize();
        FitDisplay();
    }

    private void ReportSize() => _calc?.Resize(Bounds.Width, Bounds.Height);

    private void OnCalculatorChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CalculatorViewModel.Layout):
                if (_calc!.Layout.ShowAdvanced != _builtWithPanel)
                {
                    Rebuild();
                }
                else
                {
                    ApplyLayout();
                }
                break;
            case nameof(CalculatorViewModel.Display) or nameof(CalculatorViewModel.IsError):
                FitDisplay();
                break;
            case nameof(CalculatorViewModel.Secondary):
                _secondary.Text = _calc!.Secondary;
                break;
            case nameof(CalculatorViewModel.HasMemory) or nameof(CalculatorViewModel.Angle):
                UpdateIndicators();
                break;
        }
    }

    // Lays the keys out again (the advanced panel appeared or went away).
    private void Rebuild()
    {
        if (_calc is null)
        {
            return;
        }
        var panel = _calc.Layout.ShowAdvanced;
        _builtWithPanel = panel;
        _buttons.Clear();
        _angleKeyText = null;
        _keys.Children.Clear();
        _advancedPanel.Children.Clear();
        _keys.ColumnDefinitions.Clear();
        _keys.RowDefinitions.Clear();
        for (var row = 0; row < CalculatorLayout.Rows; row++)
        {
            _keys.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        }
        for (var column = 0; column < (panel ? CalculatorLayout.AdvancedColumns : 0) + CalculatorLayout.BasicColumns; column++)
        {
            _keys.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        }

        var offset = panel ? CalculatorLayout.AdvancedColumns : 0;
        foreach (var def in BasicKeys(_calc.DecimalSeparator))
        {
            Add(def, offset);
        }
        if (panel)
        {
            foreach (var def in AdvancedKeys)
            {
                Add(def, 0);
            }
            _advancedPanel.IsHitTestVisible = false;
            _keys.Children.Insert(0, _advancedPanel);
            Grid.SetColumnSpan(_advancedPanel, CalculatorLayout.AdvancedColumns);
            Grid.SetRowSpan(_advancedPanel, CalculatorLayout.Rows);
        }
        ApplyLayout();
        UpdateIndicators();
        _secondary.Text = _calc.Secondary;
        FitDisplay();
    }

    private void Add(KeyDef def, int columnOffset)
    {
        var text = new TextBlock { Text = def.Label, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var button = new Button
        {
            Name = "Key_" + def.Key,
            Content = text,
            Command = _calc!.PressCommand,
            CommandParameter = def.Key,
            Focusable = false,
        };
        button.Classes.Add("calcKey");
        button.Classes.Add(def.Kind switch
        {
            KeyKind.Number => "num",
            KeyKind.Function => "fn",
            KeyKind.Operator => "op",
            KeyKind.Equals => "eq",
            _ => "adv",
        });
        Grid.SetRow(button, def.Row);
        Grid.SetColumn(button, def.Column + columnOffset);
        Grid.SetColumnSpan(button, def.Span);
        _keys.Children.Add(button);
        _buttons.Add((button, text, def.Kind));
        if (def.Key == CalcKey.ToggleAngle)
        {
            _angleKeyText = text;
        }
    }

    // Sizes everything from the layout the view model worked out for the widget's size.
    private void ApplyLayout()
    {
        if (_calc is null)
        {
            return;
        }
        var layout = _calc.Layout;
        MinHeight = layout.MinHeight;
        _root.Margin = new Thickness(layout.Pad);
        _root.RowDefinitions[0].Height = new GridLength(layout.DisplayHeight - layout.Pad);
        _keys.RowSpacing = layout.Gap;
        _keys.ColumnSpacing = layout.Gap;
        _keys.Margin = new Thickness(0, layout.Gap, 0, 0);
        _secondary.FontSize = layout.SecondaryFont;
        _memory.FontSize = layout.SecondaryFont;
        _angle.FontSize = layout.SecondaryFont;
        var radius = new CornerRadius(Math.Max(6, Math.Min(layout.KeyWidth, layout.KeyHeight) * 0.22));
        foreach (var (button, text, kind) in _buttons)
        {
            // Operator glyphs are drawn small in the font, so they get a larger size; it still fits the key (see CalculatorLayout.KeyFont).
            text.FontSize = kind switch
            {
                KeyKind.Advanced => layout.SmallKeyFont,
                KeyKind.Operator or KeyKind.Equals => layout.KeyFont * OperatorScale,
                _ => layout.KeyFont,
            };
            button.CornerRadius = radius;
        }
        UpdateIndicators();
        FitDisplay();
    }

    private void UpdateIndicators()
    {
        if (_calc is null)
        {
            return;
        }
        _memory.IsVisible = _calc.HasMemory;
        _angle.IsVisible = _builtWithPanel;
        _angle.Text = _calc.AngleLabel;
        if (_angleKeyText is not null)
        {
            _angleKeyText.Text = _calc.Angle == AngleMode.Degrees ? "Degrees" : "Radians";
        }
        _memory.Foreground = Brush("AccentTextBrush");
        _angle.Foreground = Brush("DimTextBrush");
        _secondary.Foreground = Brush("DimTextBrush");
    }

    // Shows the display text at the largest size that fits the room; a number too long even at the smallest size shows its end.
    private void FitDisplay()
    {
        if (_calc is null)
        {
            return;
        }
        var full = _calc.Layout.ResultFont;
        var available = _resultHost.Bounds.Width;
        var text = _calc.Display;
        var size = full;
        if (available > 0)
        {
            var width = Measure(text, full);
            if (width > available)
            {
                size = Math.Max(full * 0.45, full * available / width);
                while (text.Length > 1 && Measure(text, size) > available)
                {
                    text = text.Length > 2 && text[0] == '…' ? "…" + text[2..] : "…" + text[1..];
                }
            }
        }
        _display.FontSize = size;
        _display.Text = text;
        _display.Foreground = Brush(_calc.IsError ? "CheckpointBrush" : "TextBrush");
        _shownText = text;
    }

    private double Measure(string text, double size) =>
        new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(_display.FontFamily, _display.FontStyle, _display.FontWeight), size, null).Width;

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    private bool InEditMode => this.FindAncestorOfType<WidgetFrame>() is { IsEditing: true };

    // Escape clears the calculator while it has the keyboard, unless it is there to cancel something else.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _calc is not null && DataContext is MainViewModel main && !InEditMode
            && !main.Settings.IsOpen && !main.Schedule.BlockEditor.IsOpen && !main.Schedule.MarkerEditor.IsOpen)
        {
            _calc.PressCommand.Execute(CalculatorKeys.Escape);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_calc is null || e.Handled || InEditMode)
        {
            return;
        }
        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && _calc.Result is { Length: > 0 } result)
            {
                _ = clipboard.SetTextAsync(result);
            }
            e.Handled = true;
            return;
        }
        CalcKey? key = e.Key switch
        {
            Key.Enter => CalculatorKeys.Enter,
            Key.Back => CalculatorKeys.Back,
            Key.Delete => CalculatorKeys.Escape,
            _ => null,
        };
        if (key is { } k)
        {
            _calc.PressCommand.Execute(k);
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_calc is null || e.Handled || InEditMode || e.Text is null)
        {
            return;
        }
        foreach (var c in e.Text)
        {
            e.Handled |= _calc.PressChar(c);
        }
    }
}
