using System.Globalization;

namespace Chisel.Core.Converter;

// Reading and writing the converter's numbers in the user's culture.
public static class ConverterNumber
{
    // Results show at most this many significant digits, trailing zeros trimmed.
    public const int SignificantDigits = 10;

    // Typed amounts closer to zero than this are refused (they would lose all their digits in a conversion).
    public const decimal MinMagnitude = 1e-12m;

    // Results from here up, and below SciLow, are written in scientific notation.
    public const decimal SciHigh = 1e15m;
    public const decimal SciLow = 1e-6m;

    public const string NotANumber = "Not a number";
    public const string TooSmall = "Number too small";

    private const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
    private const string Fraction = "############################";

    // The typed text as a number. Accepts the culture's decimal and group separators, a sign and scientific notation (1.5e3).
    // Empty text gives neither a value nor an error.
    public static ConversionResult Parse(string? text, CultureInfo culture)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0)
        {
            return new ConversionResult(null, null);
        }
        if (!decimal.TryParse(text, Styles, culture, out var value))
        {
            // Too big for a decimal (or "1e400"): the double reading tells a huge number from nonsense.
            return double.TryParse(text, Styles, culture, out var d) && !double.IsNaN(d) && (double.IsInfinity(d) || Math.Abs(d) > 1e28)
                ? ConversionResult.Fail(UnitConverter.TooLarge)
                : ConversionResult.Fail(NotANumber);
        }
        if (Math.Abs(value) > UnitConverter.MaxMagnitude)
        {
            return ConversionResult.Fail(UnitConverter.TooLarge);
        }
        // A decimal rounds 1e-30 to 0, so a zero is checked against the double reading.
        var tiny = value == 0
            ? double.TryParse(text, Styles, culture, out var dz) && dz != 0
            : Math.Abs(value) < MinMagnitude;
        return tiny ? ConversionResult.Fail(TooSmall) : ConversionResult.Ok(value);
    }

    // The number for display: rounded to SignificantDigits, with the culture's group and decimal separators.
    public static string Format(decimal value, CultureInfo culture) => Write(value, culture, "#,0.");

    // The number to copy: as Format, without group separators.
    public static string Plain(decimal value, CultureInfo culture) => Write(value, culture, "0.");

    private static string Write(decimal value, CultureInfo culture, string integerPart)
    {
        if (value == 0)
        {
            return "0";
        }
        var abs = Math.Abs(value);
        if (abs >= SciHigh || abs < SciLow)
        {
            return value.ToString("0." + new string('#', SignificantDigits - 1) + "E+0", culture);
        }
        return Round(value).ToString(integerPart + Fraction, culture);
    }

    private static decimal Round(decimal value)
    {
        var magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        var decimals = SignificantDigits - 1 - magnitude;
        if (decimals >= 0)
        {
            return Math.Round(value, Math.Min(decimals, 28), MidpointRounding.AwayFromZero);
        }
        var scale = Pow10(-decimals);
        return Math.Round(value / scale, MidpointRounding.AwayFromZero) * scale;
    }

    private static decimal Pow10(int n)
    {
        var p = 1m;
        for (var i = 0; i < n; i++)
        {
            p *= 10;
        }
        return p;
    }
}
