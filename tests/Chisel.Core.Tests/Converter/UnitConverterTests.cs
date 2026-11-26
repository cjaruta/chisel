using System.Globalization;
using Chisel.Core.Converter;

namespace Chisel.Core.Tests.Converter;

public class UnitConverterTests
{
    // value, from, to, expected (exact where the factors are exact)
    public static TheoryData<string, decimal, string, string, decimal> Known => new()
    {
        { "length", 1m, "in", "cm", 2.54m },
        { "length", 1m, "mi", "km", 1.609344m },
        { "length", 3m, "ft", "yd", 1m },
        { "length", 1000m, "mm", "m", 1m },
        { "mass", 1m, "lb", "kg", 0.45359237m },
        { "mass", 1m, "stone", "lb", 14m },
        { "mass", 16m, "oz", "lb", 1m },
        { "mass", 1m, "g", "mg", 1000m },
        { "temperature", 100m, "C", "F", 212m },
        { "temperature", 0m, "K", "C", -273.15m },
        { "temperature", -40m, "F", "C", -40m },
        { "volume", 1m, "gal", "L", 3.785411784m },
        { "volume", 3m, "tsp", "tbsp", 1m },
        { "volume", 2m, "cup", "pt", 1m },
        { "volume", 8m, "floz", "cup", 1m },
        { "volume", 4m, "qt", "gal", 1m },
        { "area", 1m, "acre", "ft2", 43560m },
        { "area", 1m, "ha", "m2", 10000m },
        { "area", 1m, "km2", "ha", 100m },
        { "speed", 36m, "kmh", "ms", 10m },
        { "speed", 1m, "mph", "kmh", 1.609344m },
        { "speed", 1m, "kn", "kmh", 1.852m },
        { "data", 1m, "MB", "KB", 1000m },
        { "data", 1m, "MiB", "KiB", 1024m },
        { "data", 1m, "GiB", "B", 1073741824m },
        { "data", 1m, "TB", "GB", 1000m },
        { "data", 1m, "B", "bit", 8m },
        { "time", 1m, "h", "s", 3600m },
        { "time", 1m, "wk", "d", 7m },
        { "time", 1m, "yr", "d", 365.25m },
        { "time", 1m, "min", "ms", 60000m },
    };

    [Theory]
    [MemberData(nameof(Known))]
    public void Converts_KnownValues_BothWays(string category, decimal value, string from, string to, decimal expected)
    {
        var c = UnitCatalog.Get(category)!;
        Assert.Equal(expected, UnitConverter.Convert(value, c.Unit(from)!, c.Unit(to)!).Value!.Value, 20);
        Assert.Equal(value, UnitConverter.Convert(expected, c.Unit(to)!, c.Unit(from)!).Value!.Value, 20);
    }

    [Fact]
    public void RoundTrips_EveryUnitPair_WithinTolerance()
    {
        foreach (var category in UnitCatalog.All)
        {
            foreach (var a in category.Units)
            {
                foreach (var b in category.Units)
                {
                    var r = UnitConverter.Convert(1.23456m, a, b);
                    Assert.True(r.Value.HasValue, $"{category.Id}: {a.Id} -> {b.Id}: {r.Error}");
                    var there = r.Value!.Value;
                    var back = UnitConverter.Convert(there, b, a).Value!.Value;
                    Assert.True(Math.Abs(back - 1.23456m) < 1e-12m, $"{category.Id}: {a.Id} -> {b.Id} -> {a.Id} gave {back}");
                }
            }
        }
    }

    [Fact]
    public void BinaryAndDecimal_DataUnits_Differ()
    {
        var data = UnitCatalog.Get("data")!;
        Assert.Equal(1048576m, UnitConverter.Convert(1, data.Unit("MiB")!, data.Unit("B")!).Value);
        Assert.Equal(1000000m, UnitConverter.Convert(1, data.Unit("MB")!, data.Unit("B")!).Value);
        Assert.Equal(1.048576m, UnitConverter.Convert(1, data.Unit("MiB")!, data.Unit("MB")!).Value);
    }

    [Theory]
    [InlineData("K", -0.01)]
    [InlineData("C", -273.16)]
    [InlineData("F", -459.68)]
    public void Rejects_BelowAbsoluteZero(string unit, double value)
    {
        var t = UnitCatalog.Get("temperature")!;
        var result = UnitConverter.Convert((decimal)value, t.Unit(unit)!, t.Unit("K")!);
        Assert.Null(result.Value);
        Assert.Equal("Below absolute zero", result.Error);
    }

    [Fact]
    public void Allows_NegativeLength_ButNotNegativeMass()
    {
        var length = UnitCatalog.Get("length")!;
        Assert.Equal(-1m, UnitConverter.Convert(-100, length.Unit("cm")!, length.Unit("m")!).Value);
        var mass = UnitCatalog.Get("mass")!;
        Assert.Equal("Can't be negative", UnitConverter.Convert(-1, mass.Unit("kg")!, mass.Unit("g")!).Error);
    }

    [Fact]
    public void Rejects_TooLargeValues()
    {
        var length = UnitCatalog.Get("length")!;
        var result = UnitConverter.Convert(UnitConverter.MaxMagnitude * 10, length.Unit("m")!, length.Unit("mm")!);
        Assert.Equal("Number too large", result.Error);
    }

    [Fact]
    public void Catalog_HasEveryCategory_WithDefaultsInIt()
    {
        Assert.Equal(new[] { "length", "mass", "temperature", "volume", "area", "speed", "data", "time" }, UnitCatalog.All.Select(c => c.Id));
        foreach (var c in UnitCatalog.All)
        {
            Assert.NotNull(c.Unit(c.DefaultFrom));
            Assert.NotNull(c.Unit(c.DefaultTo));
            Assert.Equal(c.Units.Count, c.Units.Select(u => u.Id).Distinct().Count());
        }
    }
}

public class ConverterNumberTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData(" -3 ", -3)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1.5e3", 1500)]
    [InlineData("2E-3", 0.002)]
    public void Parses_EnUs(string text, double expected)
    {
        var parsed = ConverterNumber.Parse(text, Us);
        Assert.Null(parsed.Error);
        Assert.Equal((decimal)expected, parsed.Value);
    }

    [Fact]
    public void Parses_CommaDecimalCulture()
    {
        Assert.Equal(1234.5m, ConverterNumber.Parse("1.234,5", De).Value);
        Assert.Equal(0.25m, ConverterNumber.Parse("0,25", De).Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1..2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e400")]
    [InlineData("12abc")]
    public void Rejects_Invalid(string text)
    {
        var parsed = ConverterNumber.Parse(text, Us);
        Assert.Null(parsed.Value);
        Assert.NotNull(parsed.Error);
    }

    [Fact]
    public void Empty_IsNeitherValueNorError()
    {
        var parsed = ConverterNumber.Parse("  ", Us);
        Assert.Null(parsed.Value);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void Rejects_HugeAndTinyMagnitudes()
    {
        Assert.Equal("Number too large", ConverterNumber.Parse("1e20", Us).Error);
        Assert.Equal("Number too small", ConverterNumber.Parse("1e-20", Us).Error);
    }

    [Theory]
    [InlineData(1234567.891, "en-US", "1,234,567.891")]
    [InlineData(1234567.891, "de-DE", "1.234.567,891")]
    [InlineData(0.30480000, "en-US", "0.3048")]
    [InlineData(-2.5, "de-DE", "-2,5")]
    [InlineData(3.28083989501312, "en-US", "3.280839895")]
    [InlineData(0, "en-US", "0")]
    public void Formats_WithCulture_AndSignificantDigits(double value, string culture, string expected) =>
        Assert.Equal(expected, ConverterNumber.Format((decimal)value, CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Formats_LargeAndTinyValues_InScientificNotation()
    {
        Assert.Equal("1.5E+18", ConverterNumber.Format(1.5e18m, Us));
        Assert.Equal("2,5E-9", ConverterNumber.Format(0.0000000025m, De));
    }

    [Fact]
    public void Plain_HasNoGroupSeparators()
    {
        Assert.Equal("1234567,5", ConverterNumber.Plain(1234567.5m, De));
        Assert.Equal("1234567.5", ConverterNumber.Plain(1234567.5m, Us));
    }
}
