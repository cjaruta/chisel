using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chisel.Core.Calculator;

// The advanced keypad: keys build an expression (shown as typed, like 2+3×4 or sin(30), with its parentheses closed for you), and
// equals evaluates it with ExpressionEvaluator. The expression is a list of tokens: a number ("12.5", negative "-5"), an operator,
// a postfix ("!", "%", "^2", "^(-1)"), "(" or a function opener ("sin("), or a constant ("pi", "e").
internal sealed class ExpressionPad
{
    private const int MaxDigits = 15;

    private readonly List<string> _tokens = [];
    private List<string> _source = [];
    private bool _evaluated;

    public bool IsError { get; private set; }

    public void Reset()
    {
        _tokens.Clear();
        _source = [];
        _evaluated = false;
        IsError = false;
    }

    // Shows a number as if it were a result: the next digit replaces it, an operator carries on from it.
    public void Show(Num value)
    {
        Reset();
        if (!value.IsZero)
        {
            _tokens.Add(CalculatorFormat.Neutral(value));
            _evaluated = true;
        }
    }

    // The big line.
    public string Display(CultureInfo culture)
    {
        if (IsError)
        {
            return "Error";
        }
        return _tokens.Count == 0 ? "0" : string.Concat(_tokens.Select(t => Shown(t, culture)));
    }

    // The small line: what the last result came from, or the value so far.
    public string Secondary(AngleMode angle, CultureInfo culture)
    {
        if (IsError)
        {
            return "";
        }
        if (_evaluated)
        {
            return string.Concat(_source.Select(t => Shown(t, culture))) + " =";
        }
        var trimmed = Trimmed();
        if (trimmed.Count == 0 || (trimmed.Count == 1 && IsNumber(trimmed[0])))
        {
            return "";
        }
        return Value(angle) is { } value ? "= " + CalculatorFormat.Format(value, culture) : "";
    }

    // The value of the expression so far; null if it is empty or does not work out.
    public Num? Value(AngleMode angle)
    {
        if (IsError || Trimmed().Count == 0)
        {
            return null;
        }
        try
        {
            return ExpressionEvaluator.Value(Canonical(Trimmed()), angle);
        }
        catch (CalculatorException)
        {
            return null;
        }
    }

    public void Recall(Num value)
    {
        if (IsError || _evaluated)
        {
            Reset();
        }
        if (!value.IsZero || _tokens.Count == 0)
        {
            _tokens.Add(CalculatorFormat.Neutral(value));
        }
    }

    public void Press(CalcKey key, AngleMode angle)
    {
        if (IsError)
        {
            Reset();
            if (key > CalcKey.Decimal)
            {
                return;
            }
        }
        try
        {
            Handle(key, angle);
        }
        catch (CalculatorException)
        {
            Reset();
            IsError = true;
        }
    }

    private void Handle(CalcKey key, AngleMode angle)
    {
        switch (key)
        {
            case <= CalcKey.Digit9:
                Digit(key - CalcKey.Digit0);
                break;
            case CalcKey.Decimal:
                Decimal();
                break;
            case CalcKey.Add:
                Operator("+");
                break;
            case CalcKey.Subtract:
                Operator("-");
                break;
            case CalcKey.Multiply:
                Operator("*");
                break;
            case CalcKey.Divide:
                Operator("/");
                break;
            case CalcKey.Power:
                Operator("^");
                break;
            case CalcKey.Equals:
                Evaluate(angle);
                break;
            case CalcKey.Clear:
                Reset();
                break;
            case CalcKey.Negate:
                Negate(angle);
                break;
            case CalcKey.Percent:
                Postfix("%");
                break;
            case CalcKey.Square:
                Postfix("^2");
                break;
            case CalcKey.Reciprocal:
                Postfix("^(-1)");
                break;
            case CalcKey.Factorial:
                Postfix("!");
                break;
            case CalcKey.Backspace:
                Backspace();
                break;
            case CalcKey.OpenParen:
                StartNew();
                _tokens.Add("(");
                break;
            case CalcKey.CloseParen:
                if (Depth() > 0 && _tokens.Count > 0 && IsValueEnd(_tokens[^1]))
                {
                    _tokens.Add(")");
                }
                break;
            case CalcKey.Pi:
                StartNew();
                _tokens.Add("pi");
                break;
            case CalcKey.E:
                StartNew();
                _tokens.Add("e");
                break;
            case CalcKey.Sin or CalcKey.Cos or CalcKey.Tan or CalcKey.Asin or CalcKey.Acos or CalcKey.Atan or CalcKey.Ln or CalcKey.Log or CalcKey.Sqrt:
                Function(key switch
                {
                    CalcKey.Sin => "sin(",
                    CalcKey.Cos => "cos(",
                    CalcKey.Tan => "tan(",
                    CalcKey.Asin => "asin(",
                    CalcKey.Acos => "acos(",
                    CalcKey.Atan => "atan(",
                    CalcKey.Ln => "ln(",
                    CalcKey.Log => "log(",
                    _ => "sqrt(",
                });
                break;
        }
    }

    // A new calculation starts when something is typed after a result.
    private void StartNew()
    {
        if (_evaluated)
        {
            Reset();
        }
    }

    private void Digit(int d)
    {
        StartNew();
        var text = d.ToString(CultureInfo.InvariantCulture);
        if (_tokens.Count > 0 && IsNumber(_tokens[^1]))
        {
            var last = _tokens[^1];
            if (last is "0")
            {
                _tokens[^1] = text;
            }
            else if (last is "-0")
            {
                _tokens[^1] = "-" + text;
            }
            else if (last.Count(char.IsDigit) < MaxDigits)
            {
                _tokens[^1] = last + text;
            }
            return;
        }
        _tokens.Add(text);
    }

    private void Decimal()
    {
        StartNew();
        if (_tokens.Count > 0 && IsNumber(_tokens[^1]))
        {
            if (!_tokens[^1].Contains('.'))
            {
                _tokens[^1] += ".";
            }
            return;
        }
        _tokens.Add("0.");
    }

    private void Operator(string op)
    {
        _evaluated = false;
        if (_tokens.Count == 0)
        {
            if (op == "-")
            {
                _tokens.Add("-");
            }
            else
            {
                _tokens.AddRange(["0", op]);
            }
            return;
        }
        var last = _tokens[^1];
        if (IsOpener(last))
        {
            if (op == "-")
            {
                _tokens.Add("-");
            }
        }
        else if (IsBinary(last))
        {
            if (op == "-" && last is "*" or "/" or "^")
            {
                _tokens.Add("-"); // 2 × −3
            }
            else
            {
                _tokens[^1] = op;
            }
        }
        else
        {
            _tokens.Add(op);
        }
    }

    private void Postfix(string token)
    {
        _evaluated = false;
        if (_tokens.Count > 0 && IsValueEnd(_tokens[^1]))
        {
            _tokens.Add(token);
        }
    }

    private void Function(string opener)
    {
        if (_evaluated)
        {
            _evaluated = false;
            _tokens.Insert(0, opener); // sin of the result
            return;
        }
        _tokens.Add(opener);
    }

    private void Negate(AngleMode angle)
    {
        if (_evaluated)
        {
            if (Value(angle) is { } value)
            {
                _tokens.Clear();
                _tokens.Add(CalculatorFormat.Neutral(value.Negate()));
            }
            return;
        }
        if (_tokens.Count > 0 && IsNumber(_tokens[^1]))
        {
            var last = _tokens[^1];
            _tokens[^1] = last.StartsWith('-') ? last[1..] : "-" + last;
        }
        else if (_tokens.Count == 0 || IsBinary(_tokens[^1]) || IsOpener(_tokens[^1]))
        {
            _tokens.Add("-0");
        }
    }

    private void Backspace()
    {
        if (_evaluated || _tokens.Count == 0)
        {
            return;
        }
        var last = _tokens[^1];
        var shorter = IsNumber(last) ? last[..^1] : "";
        if (shorter is "" or "-")
        {
            _tokens.RemoveAt(_tokens.Count - 1);
        }
        else
        {
            _tokens[^1] = shorter;
        }
    }

    private void Evaluate(AngleMode angle)
    {
        if (_evaluated || Trimmed().Count == 0)
        {
            return;
        }
        var value = ExpressionEvaluator.Value(Canonical(Trimmed()), angle);
        _source = [.. _tokens];
        _tokens.Clear();
        _tokens.Add(CalculatorFormat.Neutral(value));
        _evaluated = true;
    }

    // The expression without operators left dangling at its end (2+3+ is 2+3).
    private List<string> Trimmed()
    {
        var count = _tokens.Count;
        while (count > 0 && IsBinary(_tokens[count - 1]))
        {
            count--;
        }
        return _tokens.GetRange(0, count);
    }

    private static string Canonical(List<string> tokens)
    {
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            sb.Append(IsNumber(token) && token[0] == '-' ? "(" + token + ")" : token);
        }
        return sb.ToString();
    }

    private int Depth() => _tokens.Count(t => t == "(" || (t.Length > 1 && t.EndsWith('('))) - _tokens.Count(t => t == ")");

    private static bool IsNumber(string t) => char.IsDigit(t[0]) || t[0] == '.' || (t[0] == '-' && t.Length > 1);

    private static bool IsBinary(string t) => t is "+" or "-" or "*" or "/" or "^";

    private static bool IsOpener(string t) => t.EndsWith('(');

    private static bool IsValueEnd(string t) => IsNumber(t) || t is ")" or "!" or "%" or "^2" or "^(-1)" or "pi" or "e";

    private static string Shown(string token, CultureInfo culture) => token switch
    {
        "*" => "×",
        "/" => "÷",
        "-" => "−",
        "pi" => "π",
        "sqrt(" => "√(",
        "asin(" => "sin⁻¹(",
        "acos(" => "cos⁻¹(",
        "atan(" => "tan⁻¹(",
        "^2" => "²",
        "^(-1)" => "⁻¹",
        _ => IsNumber(token) ? CalculatorFormat.Localize(token, culture) : token,
    };
}
