using System.Globalization;
using Chisel.Core.Calculator;

namespace Chisel.Core.Tests.Calculator;

public class CalculatorFormatTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0.3, "0.3")]
    [InlineData(0.0, "0")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(1234567.891, "1234567.891")]
    [InlineData(123456789012345.0, "123456789012345")]
    public void Decimals_ArePlainAndTrimmed(double value, string shown) => Assert.Equal(shown, CalculatorFormat.Format((decimal)value, Invariant));

    [Fact]
    public void Decimals_RoundToTwelveSignificantDigits()
    {
        Assert.Equal("0.333333333333", CalculatorFormat.Format(1m / 3, Invariant));
        Assert.Equal("0.666666666667", CalculatorFormat.Format(2m / 3, Invariant));
        Assert.Equal("0.000123456789012", CalculatorFormat.Format(0.000123456789012345m, Invariant));
    }

    [Theory]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(1.0 / 3, "0.333333333333")]
    [InlineData(0.49999999999999994, "0.5")]
    [InlineData(100.00000000000001, "100")]
    public void Doubles_DoNotShowFloatingPointNoise(double value, string shown) => Assert.Equal(shown, CalculatorFormat.Format(value, Invariant));

    [Theory]
    [InlineData(1.5e20, "1.5E+20")]
    [InlineData(6.02e23, "6.02E+23")]
    [InlineData(1e15, "1E+15")]
    [InlineData(1.2345678901234567e19, "1.23456789012E+19")]
    [InlineData(-4e30, "-4E+30")]
    [InlineData(1e-10, "1E-10")]
    [InlineData(2.5e-12, "2.5E-12")]
    public void VeryLargeOrSmallValues_UseScientificNotation(double value, string shown) => Assert.Equal(shown, CalculatorFormat.Format(value, Invariant));

    [Fact]
    public void BigDecimals_UseScientificNotationToo() => Assert.Equal("1E+24", CalculatorFormat.Format(1_000_000_000_000_000_000_000_000m, Invariant));

    [Fact]
    public void Plain_StopsJustBelowAQuadrillion() => Assert.Equal("999999999999999", CalculatorFormat.Format(999_999_999_999_999m, Invariant));

    [Fact]
    public void TheCultureChoosesTheSeparator_AndTheMinusSign()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("-1234,5", CalculatorFormat.Format(-1234.5m, german));
        Assert.Equal("1,5E+20", CalculatorFormat.Format(1.5e20, german));
    }

    [Fact]
    public void NotANumberAndInfinity_AreNotFormattable()
    {
        Assert.Throws<CalculatorException>(() => CalculatorFormat.Format(double.NaN, Invariant));
        Assert.Throws<CalculatorException>(() => CalculatorFormat.Format(double.PositiveInfinity, Invariant));
    }
}
