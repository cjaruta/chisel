using System;
using System.Globalization;

namespace Chisel.Core.Calculator;

// The calculator behind the widget: keys in, display text out. No UI types. Display is localized (the culture's decimal separator);
// keys always mean the same thing whatever the culture.
//
// Basic mode is the phone calculator (BasicPad, decimal arithmetic). Advanced mode builds an expression (ExpressionPad) that is
// evaluated with precedence, parentheses and functions. The memory and the angle setting belong to the calculator, not the mode.
public sealed class CalculatorEngine
{
    private readonly CultureInfo _culture;
    private readonly BasicPad _basic = new();
    private readonly ExpressionPad _expression = new();
    private Num? _memory;
    private bool _advanced;

    public CalculatorEngine(CultureInfo? culture = null)
    {
        _culture = culture ?? CultureInfo.CurrentCulture;
    }

    // Advanced mode takes expressions with precedence, parentheses and functions; off it is the phone-style calculator.
    // Switching keeps the number the calculator is showing (or the value of the expression so far); an error is cleared.
    public bool Advanced
    {
        get => _advanced;
        set
        {
            if (value == _advanced)
            {
                return;
            }
            var shown = CurrentValue() ?? Num.Of(0m);
            _advanced = value;
            if (value)
            {
                _expression.Show(shown);
            }
            else
            {
                _basic.Show(shown);
            }
        }
    }

    // Whether the trig functions work in degrees or radians.
    public AngleMode Angle { get; set; } = AngleMode.Degrees;

    public bool HasMemory => _memory is not null;

    public bool IsError => _advanced ? _expression.IsError : _basic.IsError;

    // The big line: the number being typed, the expression being built, or the last result.
    public string Display => _advanced ? _expression.Display(_culture) : CalculatorFormat.Localize(_basic.Neutral, _culture);

    // The small line above it: the operation waiting for its second number (basic), or the value so far / what was calculated (advanced).
    public string Secondary => _advanced ? _expression.Secondary(Angle, _culture) : CalculatorFormat.Localize(_basic.Pending, _culture);

    // The number to copy: the displayed number, or the value of the expression so far. Empty while in error.
    public string Result => CurrentValue() is { } value ? CalculatorFormat.Format(value, _culture) : "";

    public void Press(CalcKey key)
    {
        switch (key)
        {
            case CalcKey.ToggleAngle:
                Angle = Angle == AngleMode.Degrees ? AngleMode.Radians : AngleMode.Degrees;
                break;
            case CalcKey.MemoryClear:
                _memory = null;
                break;
            case CalcKey.MemoryRecall:
                Recall();
                break;
            case CalcKey.MemoryAdd or CalcKey.MemorySubtract:
                AddToMemory(key == CalcKey.MemoryAdd);
                break;
            case >= CalcKey.OpenParen when !_advanced:
                break; // the advanced keys do nothing on the basic keypad
            default:
                if (_advanced)
                {
                    _expression.Press(key, Angle);
                }
                else
                {
                    _basic.Press(key);
                }
                break;
        }
    }

    private Num? CurrentValue() => _advanced ? _expression.Value(Angle) : _basic.Value;

    private void Recall()
    {
        var memory = _memory ?? Num.Of(0m);
        if (_advanced)
        {
            _expression.Recall(memory);
        }
        else
        {
            _basic.Recall(memory);
        }
    }

    private void AddToMemory(bool add)
    {
        if (CurrentValue() is not { } value)
        {
            return;
        }
        _memory = _memory is { } memory
            ? (add ? Num.Add(memory, value) : Num.Subtract(memory, value))
            : (add ? value : value.Negate());
        // The number is "used up": the next digit starts a new one.
        if (_advanced)
        {
            _expression.Press(CalcKey.Equals, Angle);
        }
        else
        {
            _basic.Commit();
        }
    }
}
