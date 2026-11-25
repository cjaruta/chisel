namespace Chisel.Core.Converter;

// One unit. Every unit is defined against its category's base unit by an exact rational factor and an offset:
//   base = (value + Offset) * Numerator / Denominator
// Factors are exact decimals from the definitions (1 in = 0.0254 m, 1 lb = 0.45359237 kg, 1 US gal = 231 in³ = 3.785411784 L,
// and so on), and ratios that are not finite decimals (km/h = 1000/3600 m/s) are kept as a fraction, so nothing is rounded
// until the single division at the end of a conversion. Offset is only used by temperature (°C and °F).
public sealed record Unit(string Id, string Symbol, string Name, decimal Numerator, decimal Denominator = 1, decimal Offset = 0);

// One kind of quantity. AllowsNegative: whether a negative amount makes sense (a length or a speed can be negative, a mass or a
// file size cannot). MinBase: the least base value allowed (absolute zero for temperature). Common lists the units, most used
// first, that the widget's list of other conversions shows.
public sealed record UnitCategory(
    string Id, string Title, string ShortTitle, IReadOnlyList<Unit> Units, string DefaultFrom, string DefaultTo,
    IReadOnlyList<string> Common, bool AllowsNegative = true, decimal? MinBase = null)
{
    public Unit? Unit(string? id) => Units.FirstOrDefault(u => u.Id == id);
}

// Every category and unit. Currency is left out on purpose: it needs live rates from the network.
public static class UnitCatalog
{
    // Base: metre.
    public static readonly UnitCategory Length = new("length", "Length", "Length",
    [
        new("mm", "mm", "Millimetres", 0.001m),
        new("cm", "cm", "Centimetres", 0.01m),
        new("m", "m", "Metres", 1m),
        new("km", "km", "Kilometres", 1000m),
        new("in", "in", "Inches", 0.0254m),          // international inch, exact
        new("ft", "ft", "Feet", 0.3048m),            // 12 in
        new("yd", "yd", "Yards", 0.9144m),           // 3 ft
        new("mi", "mi", "Miles", 1609.344m),         // 1760 yd
        new("nmi", "nmi", "Nautical miles", 1852m),  // exact by definition
    ], "m", "ft", ["m", "cm", "km", "in", "ft", "mi"]);

    // Base: gram.
    public static readonly UnitCategory Mass = new("mass", "Weight / mass", "Mass",
    [
        new("mg", "mg", "Milligrams", 0.001m),
        new("g", "g", "Grams", 1m),
        new("kg", "kg", "Kilograms", 1000m),
        new("t", "t", "Tonnes (metric)", 1000000m),
        new("oz", "oz", "Ounces", 28.349523125m),    // 1/16 lb
        new("lb", "lb", "Pounds", 453.59237m),       // international pound, exact
        new("stone", "st", "Stone", 6350.29318m),    // 14 lb
    ], "kg", "lb", ["kg", "g", "lb", "oz", "stone"], AllowsNegative: false);

    // Base: kelvin. °C = K - 273.15; °F = (K × 9/5) - 459.67.
    public static readonly UnitCategory Temperature = new("temperature", "Temperature", "Temp",
    [
        new("C", "°C", "Celsius", 1m, 1m, 273.15m),
        new("F", "°F", "Fahrenheit", 5m, 9m, 459.67m),
        new("K", "K", "Kelvin", 1m),
    ], "C", "F", ["C", "F", "K"], MinBase: 0m);

    // Base: litre. US customary units: 1 US gal = 3.785411784 L, and the rest by exact ratios
    // (qt = gal/4, pt = gal/8, cup = gal/16, fl oz = gal/128, tbsp = gal/256, tsp = gal/768).
    public static readonly UnitCategory Volume = new("volume", "Volume", "Volume",
    [
        new("mL", "mL", "Millilitres", 0.001m),
        new("L", "L", "Litres", 1m),
        new("m3", "m³", "Cubic metres", 1000m),
        new("tsp", "tsp", "Teaspoons (US)", 0.00492892159375m),
        new("tbsp", "tbsp", "Tablespoons (US)", 0.01478676478125m),
        new("floz", "fl oz", "Fluid ounces (US)", 0.0295735295625m),
        new("cup", "cup", "Cups (US)", 0.2365882365m),
        new("pt", "pt", "Pints (US)", 0.473176473m),
        new("qt", "qt", "Quarts (US)", 0.946352946m),
        new("gal", "gal", "Gallons (US)", 3.785411784m),
    ], "L", "gal", ["mL", "L", "cup", "floz", "gal"], AllowsNegative: false);

    // Base: square metre. Squares of the exact lengths; 1 acre = 43560 ft².
    public static readonly UnitCategory Area = new("area", "Area", "Area",
    [
        new("mm2", "mm²", "Square millimetres", 0.000001m),
        new("cm2", "cm²", "Square centimetres", 0.0001m),
        new("m2", "m²", "Square metres", 1m),
        new("ha", "ha", "Hectares", 10000m),
        new("km2", "km²", "Square kilometres", 1000000m),
        new("in2", "in²", "Square inches", 0.00064516m),
        new("ft2", "ft²", "Square feet", 0.09290304m),
        new("yd2", "yd²", "Square yards", 0.83612736m),
        new("acre", "ac", "Acres", 4046.8564224m),
        new("mi2", "mi²", "Square miles", 2589988.110336m),
    ], "m2", "ft2", ["m2", "ft2", "ha", "acre", "km2"], AllowsNegative: false);

    // Base: metre per second. Per-hour units are distance / 3600, kept as a fraction.
    public static readonly UnitCategory Speed = new("speed", "Speed", "Speed",
    [
        new("ms", "m/s", "Metres per second", 1m),
        new("kmh", "km/h", "Kilometres per hour", 1000m, 3600m),
        new("mph", "mph", "Miles per hour", 1609.344m, 3600m),
        new("kn", "kn", "Knots", 1852m, 3600m),
        new("fts", "ft/s", "Feet per second", 0.3048m),
    ], "kmh", "mph", ["kmh", "mph", "ms", "kn"]);

    // Base: byte. Decimal (SI) units are powers of 1000 (KB, MB, GB, TB); binary (IEC) units are powers of 1024 (KiB, MiB, GiB, TiB).
    public static readonly UnitCategory Data = new("data", "Data size", "Data",
    [
        new("bit", "bit", "Bits", 1m, 8m),
        new("B", "B", "Bytes", 1m),
        new("KB", "KB", "Kilobytes (1000 B)", 1000m),
        new("MB", "MB", "Megabytes (1000² B)", 1000000m),
        new("GB", "GB", "Gigabytes (1000³ B)", 1000000000m),
        new("TB", "TB", "Terabytes (1000⁴ B)", 1000000000000m),
        new("KiB", "KiB", "Kibibytes (1024 B)", 1024m),
        new("MiB", "MiB", "Mebibytes (1024² B)", 1048576m),
        new("GiB", "GiB", "Gibibytes (1024³ B)", 1073741824m),
        new("TiB", "TiB", "Tebibytes (1024⁴ B)", 1099511627776m),
    ], "MB", "MiB", ["B", "KB", "MB", "GB", "MiB", "GiB"], AllowsNegative: false);

    // Base: second. A year is the Julian year (365.25 days) and a month a twelfth of it (30.4375 days).
    public static readonly UnitCategory Time = new("time", "Time", "Time",
    [
        new("ms", "ms", "Milliseconds", 0.001m),
        new("s", "s", "Seconds", 1m),
        new("min", "min", "Minutes", 60m),
        new("h", "h", "Hours", 3600m),
        new("d", "d", "Days", 86400m),
        new("wk", "wk", "Weeks", 604800m),
        new("mo", "mo", "Months (30.4375 d)", 2629800m),
        new("yr", "yr", "Years (365.25 d)", 31557600m),
    ], "h", "min", ["s", "min", "h", "d", "wk"]);

    public static IReadOnlyList<UnitCategory> All { get; } = [Length, Mass, Temperature, Volume, Area, Speed, Data, Time];

    public static UnitCategory? Get(string? id) => All.FirstOrDefault(c => c.Id == id);
}
