using System;

namespace Chisel.Core.Calculator;

// A calculated number: exact (decimal) while only + - * / have touched it, approximate (double) once a function has.
internal readonly record struct Num(decimal? Exact, double Approx)
{
    public static Num Of(decimal value) => new(value, (double)value);

    public static Num Of(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new CalculatorException("Not a number");
        }
        return new Num(null, value);
    }

    public bool IsZero => Exact is { } e ? e == 0 : Approx == 0;

    public Num Negate() => Exact is { } e ? Of(-e) : Of(-Approx);

    public static Num Add(Num a, Num b) => Combine(a, b, (x, y) => x + y, (x, y) => x + y);

    public static Num Subtract(Num a, Num b) => Combine(a, b, (x, y) => x - y, (x, y) => x - y);

    public static Num Multiply(Num a, Num b) => Combine(a, b, (x, y) => x * y, (x, y) => x * y);

    public static Num Divide(Num a, Num b)
    {
        if (b.IsZero)
        {
            throw new CalculatorException("Divide by zero");
        }
        return Combine(a, b, (x, y) => x / y, (x, y) => x / y);
    }

    private static Num Combine(Num a, Num b, Func<decimal, decimal, decimal> exact, Func<double, double, double> approx)
    {
        if (a.Exact is { } x && b.Exact is { } y)
        {
            try
            {
                return Of(exact(x, y));
            }
            catch (OverflowException)
            {
                // Too big for decimal: carry on in double (it may still fit, shown in scientific notation).
            }
        }
        return Of(approx(a.Approx, b.Approx));
    }
}
