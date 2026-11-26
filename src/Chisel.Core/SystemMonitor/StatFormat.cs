using System.Globalization;

namespace Chisel.Core.SystemMonitor;

// Text for the system monitor's values: sizes in GB (or TB) of 1024^3 bytes like Task Manager, network rates in bits per
// second with decimal prefixes like network gear, and the spoken forms for screen readers. One decimal under 100, none above.
// These make strings, so the widget calls them only when the value it shows has changed.
public static class StatFormat
{
    private const double GB = 1024d * 1024 * 1024;
    private const double TB = GB * 1024;

    public static int WholePercent(double value) =>
        double.IsNaN(value) ? 0 : (int)Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);

    public static string Bytes(ulong bytes)
    {
        var (value, unit, _) = Scale(bytes, bytes);
        return $"{Number(value)} {unit}";
    }

    // "9.9/16 GB": both in the total's unit.
    public static string UsedOfTotal(ulong used, ulong total)
    {
        var (value, unit, divisor) = Scale(total, total);
        return $"{Number(used / divisor)}/{Number(value)} {unit}";
    }

    public static string BitsPerSecond(double bytesPerSecond)
    {
        var (value, prefix, _) = Rate(bytesPerSecond);
        return $"{Number(value)} {prefix}bps";
    }

    // "12.4M": the rate for the middle of a ring, where there is no room for the unit word.
    public static string ShortRate(double bytesPerSecond)
    {
        var (value, prefix, _) = Rate(bytesPerSecond);
        return Number(value) + prefix;
    }

    public static string SpokenPercent(string name, int percent) => $"{name} {percent} percent";

    public static string SpokenUsage(string name, int percent, ulong used, ulong total)
    {
        var (value, _, divisor) = Scale(total, total);
        var unit = divisor == TB ? "terabytes" : "gigabytes";
        return $"{name} {percent} percent, {Number(used / divisor)} of {Number(value)} {unit} used";
    }

    public static string SpokenNetwork(double downBytesPerSecond, double upBytesPerSecond) =>
        $"Network down {SpokenRate(downBytesPerSecond)}, up {SpokenRate(upBytesPerSecond)}";

    public static string SpokenBattery(int percent, bool charging) =>
        charging ? $"Battery {percent} percent, charging" : $"Battery {percent} percent";

    private static string SpokenRate(double bytesPerSecond)
    {
        var (value, _, word) = Rate(bytesPerSecond);
        return $"{Number(value)} {word} per second";
    }

    private static (double Value, string Unit, double Divisor) Scale(ulong bytes, ulong unitFrom) =>
        unitFrom >= TB ? (bytes / TB, "TB", TB) : (bytes / GB, "GB", GB);

    private static (double Value, string Prefix, string Word) Rate(double bytesPerSecond)
    {
        var bits = Math.Max(0, double.IsNaN(bytesPerSecond) ? 0 : bytesPerSecond) * 8;
        return bits >= 1e9 ? (bits / 1e9, "G", "gigabits")
            : bits >= 1e6 ? (bits / 1e6, "M", "megabits")
            : (bits / 1e3, "K", "kilobits");
    }

    private static string Number(double value) =>
        value.ToString(value < 100 ? "0.#" : "0", CultureInfo.CurrentCulture);
}
