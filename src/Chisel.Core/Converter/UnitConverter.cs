namespace Chisel.Core.Converter;

// A converted number, or why there is none.
public readonly record struct ConversionResult(decimal? Value, string? Error)
{
    public static ConversionResult Ok(decimal value) => new(value, null);
    public static ConversionResult Fail(string error) => new(null, error);
}

// Converts with decimal arithmetic: (value + from.Offset) × from.Num × to.Den / (from.Den × to.Num) - to.Offset. The one division
// comes last, so exact factors give exact answers (1 in = 2.54 cm, 100 °C = 212 °F).
public static class UnitConverter
{
    // The largest amount accepted, typed or converted. Keeps every product well inside decimal's range (about 7.9E+28).
    public const decimal MaxMagnitude = 1e15m;

    public const string TooLarge = "Number too large";
    public const string Negative = "Can't be negative";
    public const string BelowAbsoluteZero = "Below absolute zero";

    public static ConversionResult Convert(decimal value, Unit from, Unit to) => Convert(value, from, to, CategoryOf(from));

    public static ConversionResult Convert(decimal value, Unit from, Unit to, UnitCategory? category)
    {
        if (Math.Abs(value) > MaxMagnitude)
        {
            return ConversionResult.Fail(TooLarge);
        }
        if (category is { AllowsNegative: false } && value < 0)
        {
            return ConversionResult.Fail(Negative);
        }
        try
        {
            if (category?.MinBase is { } min && (value + from.Offset) * from.Numerator / from.Denominator < min)
            {
                return ConversionResult.Fail(BelowAbsoluteZero);
            }
            if (from == to)
            {
                return ConversionResult.Ok(value);
            }
            var result = (value + from.Offset) * from.Numerator * to.Denominator / (from.Denominator * to.Numerator) - to.Offset;
            return ConversionResult.Ok(result);
        }
        catch (OverflowException)
        {
            return ConversionResult.Fail(TooLarge);
        }
    }

    private static UnitCategory? CategoryOf(Unit unit) => UnitCatalog.All.FirstOrDefault(c => c.Units.Contains(unit));
}
