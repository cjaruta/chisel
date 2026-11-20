using System;
using System.Globalization;

namespace Chisel.Core.Calculator;

// The phone-style calculator: every operator is carried out as soon as the next one is pressed (2 + 3 * 4 = 20), equals repeats
// the last operation, and percent means "of the first number" after + and -. Arithmetic is decimal. Numbers are kept as culture
// free text while typed; the engine localizes them for display.
internal sealed class BasicPad
{
    private const int MaxDigits = 15;

    private decimal? _accumulator;
    private CalcKey? _pending;
    private (CalcKey Op, decimal Operand)? _last;
    private string _entry = "0";
    private decimal _value;
    private bool _typing;
    private bool _operandReady;

    public bool IsError { get; private set; }

    // The shown value as culture-free text ('.' separator, leading '-').
    public string Neutral => IsError ? "Error" : _typing ? _entry : CalculatorFormat.Format(_value, CultureInfo.InvariantCulture);

    public string Pending => _pending is { } op && _accumulator is { } acc && !IsError
        ? $"{CalculatorFormat.Format(acc, CultureInfo.InvariantCulture)} {Symbol(op)}" : "";

    // The shown number, or null while in error.
    public Num? Value => IsError ? null : Num.Of(Current());

    public void Reset()
    {
        _accumulator = null;
        _pending = null;
        _last = null;
        _entry = "0";
        _value = 0;
        _typing = false;
        _operandReady = false;
        IsError = false;
    }

    // Shows a number as if it had been typed: the next digit replaces it.
    public void Show(Num value)
    {
        Reset();
        try
        {
            _value = value.Exact ?? checked((decimal)value.Approx);
        }
        catch (OverflowException)
        {
            IsError = true;
        }
    }

    public void Press(CalcKey key)
    {
        if (IsError)
        {
            Reset();
            if (key is not (<= CalcKey.Decimal))
            {
                return;
            }
        }
        try
        {
            Handle(key);
        }
        catch (Exception e) when (e is OverflowException or CalculatorException)
        {
            Reset();
            IsError = true;
        }
    }

    private void Handle(CalcKey key)
    {
        switch (key)
        {
            case <= CalcKey.Digit9:
                Digit(key - CalcKey.Digit0);
                break;
            case CalcKey.Decimal:
                Decimal();
                break;
            case CalcKey.Add or CalcKey.Subtract or CalcKey.Multiply or CalcKey.Divide:
                Operator(key);
                break;
            case CalcKey.Equals:
                Equals();
                break;
            case CalcKey.Clear:
                Reset();
                break;
            case CalcKey.Negate:
                Negate();
                break;
            case CalcKey.Percent:
                Percent();
                break;
            case CalcKey.Backspace:
                Backspace();
                break;
        }
    }

    private decimal Current() => _typing ? decimal.Parse(_entry, NumberStyles.Float, CultureInfo.InvariantCulture) : _value;

    private void StartEntry(string text)
    {
        if (_pending is null)
        {
            _last = null;
            _accumulator = null;
        }
        _entry = text;
        _typing = true;
        _operandReady = true;
    }

    private void Digit(int d)
    {
        if (!_typing)
        {
            StartEntry(d.ToString(CultureInfo.InvariantCulture));
            return;
        }
        if (_entry is "0")
        {
            _entry = d.ToString(CultureInfo.InvariantCulture);
        }
        else if (_entry is "-0")
        {
            _entry = "-" + d.ToString(CultureInfo.InvariantCulture);
        }
        else if (DigitCount() < MaxDigits)
        {
            _entry += d.ToString(CultureInfo.InvariantCulture);
        }
    }

    private int DigitCount()
    {
        var count = 0;
        foreach (var c in _entry)
        {
            count += char.IsDigit(c) ? 1 : 0;
        }
        return count;
    }

    private void Decimal()
    {
        if (!_typing)
        {
            StartEntry("0.");
        }
        else if (!_entry.Contains('.'))
        {
            _entry += ".";
        }
    }

    private void Operator(CalcKey op)
    {
        if (_pending is not null && !_operandReady)
        {
            _pending = op; // 5 + * 3: the second operator replaces the first
            return;
        }
        var shown = Current();
        _value = _pending is { } pending && _accumulator is { } acc ? Apply(acc, pending, shown) : shown;
        _accumulator = _value;
        _pending = op;
        _typing = false;
        _operandReady = false;
        _last = null;
    }

    private void Equals()
    {
        var shown = Current();
        if (_pending is { } pending && _accumulator is { } acc)
        {
            _value = Apply(acc, pending, shown);
            _last = (pending, shown);
        }
        else if (_last is { } last)
        {
            _value = Apply(shown, last.Op, last.Operand);
        }
        else
        {
            _value = shown;
        }
        _accumulator = null;
        _pending = null;
        _typing = false;
        _operandReady = false;
    }

    private void Negate()
    {
        if (_typing)
        {
            _entry = _entry.StartsWith('-') ? _entry[1..] : _entry is "0" ? _entry : "-" + _entry;
            return;
        }
        _value = -_value;
        _operandReady = _pending is not null;
    }

    private void Percent()
    {
        var shown = Current();
        _value = _pending is CalcKey.Add or CalcKey.Subtract && _accumulator is { } acc ? acc * shown / 100 : shown / 100;
        _typing = false;
        _operandReady = true;
    }

    private void Backspace()
    {
        if (!_typing)
        {
            return;
        }
        _entry = _entry[..^1];
        if (_entry is "" or "-")
        {
            _entry = "0";
        }
    }

    private static decimal Apply(decimal a, CalcKey op, decimal b) => op switch
    {
        CalcKey.Add => a + b,
        CalcKey.Subtract => a - b,
        CalcKey.Multiply => a * b,
        CalcKey.Divide => b == 0 ? throw new CalculatorException("Divide by zero") : a / b,
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string Symbol(CalcKey op) => op switch
    {
        CalcKey.Add => "+",
        CalcKey.Subtract => "−",
        CalcKey.Multiply => "×",
        _ => "÷",
    };
}
