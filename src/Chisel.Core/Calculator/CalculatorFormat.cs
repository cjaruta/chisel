using System;
using System.Globalization;

namespace Chisel.Core.Calculator;

// How the calculator writes a number: at most 12 significant digits (so 0.1 + 0.2 reads 0.3 and 1/3 reads 0.333333333333),
// trailing zeros trimmed, and scientific notation (1.5E+20) from 1E15 up and below 1E-9. The culture picks the separator and sign.
public static class CalculatorFormat
{
    public const int SignificantDigits = 12;
    private const double SciHigh = 1e15;
    private const double SciLow = 1e-9;

    public static string Format(decimal value, CultureInfo culture)
    {
        if (value == 0)
        {
            return "0";
        }
        var abs = Math.Abs(value);
        if (abs >= (decimal)SciHigh || abs < (decimal)SciLow)
        {
            return Scientific((double)value, culture);
        }
        var magnitude = (int)Math.Floor(Math.Log10((double)abs));
        var decimals = Math.Clamp(SignificantDigits - 1 - magnitude, 0, 28);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return Localize(rounded.ToString("0.############################", CultureInfo.InvariantCulture), culture);
    }

    public static string Format(double value, CultureInfo culture)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new CalculatorException("Not a number");
        }
        var abs = Math.Abs(value);
        if (abs == 0)
        {
            return "0";
        }
        return abs >= SciHigh || abs < SciLow ? Scientific(value, culture) : Format((decimal)value, culture);
    }

    internal static string Format(Num value, CultureInfo culture) =>
        value.Exact is { } exact ? Format(exact, culture) : Format(value.Approx, culture);

    // The number as typed keys would build it: digits and '.', with a leading '-'. Culture free, so it can be parsed back.
    internal static string Neutral(Num value)
    {
        var text = Format(value, CultureInfo.InvariantCulture);
        return text;
    }

    private static string Scientific(double value, CultureInfo culture)
    {
        var text = value.ToString("E" + (SignificantDigits - 1), CultureInfo.InvariantCulture);
        var e = text.IndexOf('E');
        var mantissa = text[..e];
        if (mantissa.Contains('.'))
        {
            mantissa = mantissa.TrimEnd('0').TrimEnd('.');
        }
        var exponent = int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture);
        return Localize($"{mantissa}E{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent)}", culture);
    }

    // Swaps the invariant separator and sign for the culture's own.
    internal static string Localize(string invariant, CultureInfo culture)
    {
        if (ReferenceEquals(culture, CultureInfo.InvariantCulture) || culture.Name.Length == 0)
        {
            return invariant;
        }
        var info = culture.NumberFormat;
        return invariant.Replace(".", info.NumberDecimalSeparator).Replace("-", info.NegativeSign);
    }
}
