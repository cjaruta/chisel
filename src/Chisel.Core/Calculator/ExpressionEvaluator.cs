using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chisel.Core.Calculator;

// Reads and evaluates an expression: + - * / ^ ! % ( ), unary minus, implicit multiplication (2(3+1), 2pi), the functions
// sin cos tan asin acos atan ln log sqrt and the constants pi and e. Usual precedence: ! and % bind tightest, then ^ (right to
// left), unary minus, * and /, then + and -. After + or -, "10%" means that percent of the left side (200 + 10% = 220).
// Missing closing parentheses and a trailing operator are forgiven; anything else that is not a number is a CalculatorException.
public sealed class ExpressionEvaluator
{
    private const int MaxFactorial = 170;
    private const int MaxExactPower = 100;

    private readonly List<Token> _tokens;
    private readonly AngleMode _angle;
    private int _pos;
    private bool _termIsPercent;

    private ExpressionEvaluator(List<Token> tokens, AngleMode angle)
    {
        _tokens = tokens;
        _angle = angle;
    }

    // The value of the expression written the way the calculator shows numbers.
    public static string Evaluate(string expression, AngleMode angle, CultureInfo culture) =>
        CalculatorFormat.Format(Value(expression, angle), culture);

    internal static Num Value(string expression, AngleMode angle)
    {
        var tokens = Lex(expression);
        while (tokens.Count > 0 && tokens[^1].Kind == Kind.Op && tokens[^1].Text is "+" or "-" or "*" or "/" or "^")
        {
            tokens.RemoveAt(tokens.Count - 1);
        }
        if (tokens.Count == 0)
        {
            throw new CalculatorException("Empty expression");
        }
        var parser = new ExpressionEvaluator(tokens, angle);
        var value = parser.ParseExpression();
        if (parser._pos < tokens.Count)
        {
            throw new CalculatorException("Unexpected " + tokens[parser._pos].Text);
        }
        return value;
    }

    private enum Kind { Number, Op, Name }

    private readonly record struct Token(Kind Kind, string Text, Num Number = default);

    private static List<Token> Lex(string text)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }
                if (i < text.Length && text[i] == 'E')
                {
                    var j = i + 1;
                    if (j < text.Length && text[j] is '+' or '-')
                    {
                        j++;
                    }
                    if (j < text.Length && char.IsDigit(text[j]))
                    {
                        while (j < text.Length && char.IsDigit(text[j]))
                        {
                            j++;
                        }
                        i = j;
                    }
                }
                tokens.Add(new Token(Kind.Number, text[start..i], ParseNumber(text[start..i])));
            }
            else if (char.IsLetter(c))
            {
                var sb = new StringBuilder();
                while (i < text.Length && char.IsLetter(text[i]))
                {
                    sb.Append(char.ToLowerInvariant(text[i++]));
                }
                tokens.Add(new Token(Kind.Name, sb.ToString()));
            }
            else
            {
                var op = c switch
                {
                    '×' => "*",
                    '÷' => "/",
                    '−' => "-",
                    'π' => "pi",
                    '√' => "sqrt",
                    '+' or '-' or '*' or '/' or '^' or '!' or '%' or '(' or ')' => c.ToString(),
                    _ => throw new CalculatorException("Unexpected " + c),
                };
                tokens.Add(new Token(op.Length > 1 ? Kind.Name : Kind.Op, op));
                i++;
            }
        }
        return tokens;
    }

    private static Num ParseNumber(string text)
    {
        if (text.Count(ch => ch == '.') > 1)
        {
            throw new CalculatorException("Bad number");
        }
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
        {
            return Num.Of(exact);
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var approx)
            ? Num.Of(approx) : throw new CalculatorException("Bad number");
    }

    private Token? Peek => _pos < _tokens.Count ? _tokens[_pos] : null;

    private bool PeekOp(string op) => Peek is { Kind: Kind.Op } t && t.Text == op;

    private Num ParseExpression()
    {
        var left = ParseTerm();
        while (Peek is { Kind: Kind.Op, Text: "+" or "-" } t)
        {
            _pos++;
            var right = ParseTerm();
            if (_termIsPercent)
            {
                right = Num.Multiply(left, right); // 200 + 10% adds a tenth of 200
            }
            left = t.Text == "+" ? Num.Add(left, right) : Num.Subtract(left, right);
        }
        return left;
    }

    private Num ParseTerm()
    {
        var value = ParseUnary();
        var compound = false;
        while (true)
        {
            if (PeekOp("*"))
            {
                _pos++;
                value = Num.Multiply(value, ParseUnary());
            }
            else if (PeekOp("/"))
            {
                _pos++;
                value = Num.Divide(value, ParseUnary());
            }
            else if (Peek is { Kind: Kind.Number or Kind.Name } || PeekOp("("))
            {
                value = Num.Multiply(value, ParseUnary());
            }
            else
            {
                break;
            }
            compound = true;
        }
        _termIsPercent = !compound && _pos > 0 && _tokens[_pos - 1] is { Kind: Kind.Op, Text: "%" };
        return value;
    }

    private Num ParseUnary()
    {
        if (PeekOp("-"))
        {
            _pos++;
            return ParseUnary().Negate();
        }
        if (PeekOp("+"))
        {
            _pos++;
            return ParseUnary();
        }
        return ParsePower();
    }

    private Num ParsePower()
    {
        var value = ParsePostfix();
        if (PeekOp("^"))
        {
            _pos++;
            return Power(value, ParseUnary());
        }
        return value;
    }

    private Num ParsePostfix()
    {
        var value = ParseAtom();
        while (true)
        {
            if (PeekOp("!"))
            {
                _pos++;
                value = Factorial(value);
            }
            else if (PeekOp("%"))
            {
                _pos++;
                value = Num.Divide(value, Num.Of(100m));
            }
            else
            {
                return value;
            }
        }
    }

    private Num ParseAtom()
    {
        if (Peek is not { } token)
        {
            throw new CalculatorException("Unexpected end");
        }
        _pos++;
        switch (token.Kind)
        {
            case Kind.Number:
                return token.Number;
            case Kind.Op when token.Text == "(":
                return CloseGroup(ParseExpression());
            case Kind.Name when token.Text == "pi":
                return Num.Of(Math.PI);
            case Kind.Name when token.Text == "e":
                return Num.Of(Math.E);
            case Kind.Name when IsFunction(token.Text):
                if (!PeekOp("("))
                {
                    throw new CalculatorException("Expected ( after " + token.Text);
                }
                _pos++;
                return Apply(token.Text, CloseGroup(ParseExpression()));
            default:
                throw new CalculatorException("Unexpected " + token.Text);
        }
    }

    // A missing ) at the very end is fine; anywhere else it is a mistake.
    private Num CloseGroup(Num value)
    {
        if (PeekOp(")"))
        {
            _pos++;
        }
        else if (_pos < _tokens.Count)
        {
            throw new CalculatorException("Expected )");
        }
        return value;
    }

    private static bool IsFunction(string name) => name is "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or "ln" or "log" or "sqrt";

    private Num Apply(string name, Num argument)
    {
        var x = argument.Approx;
        switch (name)
        {
            case "sin":
                return Snap(Math.Sin(ToRadians(x)));
            case "cos":
                return Snap(Math.Cos(ToRadians(x)));
            case "tan":
                var radians = ToRadians(x);
                return Math.Abs(Math.Cos(radians)) < 1e-15 ? throw new CalculatorException("Tangent is undefined here") : Snap(Math.Tan(radians));
            case "asin":
                return Math.Abs(x) > 1 ? throw new CalculatorException("Out of range") : Snap(FromRadians(Math.Asin(x)));
            case "acos":
                return Math.Abs(x) > 1 ? throw new CalculatorException("Out of range") : Snap(FromRadians(Math.Acos(x)));
            case "atan":
                return Snap(FromRadians(Math.Atan(x)));
            case "ln":
                return x <= 0 ? throw new CalculatorException("Out of range") : Num.Of(Math.Log(x));
            case "log":
                return x <= 0 ? throw new CalculatorException("Out of range") : Num.Of(Math.Log10(x));
            default:
                return x < 0 ? throw new CalculatorException("Out of range") : Num.Of(Math.Sqrt(x));
        }
    }

    private double ToRadians(double x) => _angle == AngleMode.Degrees ? x * Math.PI / 180 : x;

    private double FromRadians(double x) => _angle == AngleMode.Degrees ? x * 180 / Math.PI : x;

    // sin(180 degrees) is 1.2E-16 in floating point, but it is 0.
    private static Num Snap(double value) => Num.Of(Math.Abs(value) < 1e-14 ? 0 : value);

    private static Num Power(Num a, Num b)
    {
        if (a.Exact is not null && b.Exact is { } n && n == Math.Truncate(n) && Math.Abs(n) <= MaxExactPower)
        {
            var result = Num.Of(1m);
            for (var i = 0; i < Math.Abs(n); i++)
            {
                result = Num.Multiply(result, a);
            }
            return n < 0 ? Num.Divide(Num.Of(1m), result) : result;
        }
        return Num.Of(Math.Pow(a.Approx, b.Approx));
    }

    private static Num Factorial(Num value)
    {
        var x = value.Approx;
        if (x < 0 || x > MaxFactorial || x != Math.Floor(x))
        {
            throw new CalculatorException("Factorial needs a whole number from 0 to " + MaxFactorial);
        }
        var result = Num.Of(1m);
        for (var i = 2; i <= (int)x; i++)
        {
            result = Num.Multiply(result, Num.Of((decimal)i));
        }
        return result;
    }
}
