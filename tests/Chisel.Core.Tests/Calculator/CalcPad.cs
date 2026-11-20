using System.Globalization;
using Chisel.Core.Calculator;

namespace Chisel.Core.Tests.Calculator;

// Test helper: types keys from a short script. Digits and '.' are themselves; + - * / = are the operators;
// C clears, N is +/-, % is percent, B is backspace, ( and ) are parentheses.
internal static class CalcPad
{
    public static CalculatorEngine Run(string keys, CultureInfo? culture = null, bool advanced = false)
    {
        var engine = new CalculatorEngine(culture ?? CultureInfo.InvariantCulture) { Advanced = advanced };
        Press(engine, keys);
        return engine;
    }

    public static void Press(CalculatorEngine engine, string keys)
    {
        foreach (var c in keys)
        {
            engine.Press(c switch
            {
                >= '0' and <= '9' => CalcKey.Digit0 + (c - '0'),
                '.' => CalcKey.Decimal,
                '+' => CalcKey.Add,
                '-' => CalcKey.Subtract,
                '*' => CalcKey.Multiply,
                '/' => CalcKey.Divide,
                '=' => CalcKey.Equals,
                'C' => CalcKey.Clear,
                'N' => CalcKey.Negate,
                '%' => CalcKey.Percent,
                'B' => CalcKey.Backspace,
                '(' => CalcKey.OpenParen,
                ')' => CalcKey.CloseParen,
                _ => throw new ArgumentException($"No key for '{c}'"),
            });
        }
    }
}
