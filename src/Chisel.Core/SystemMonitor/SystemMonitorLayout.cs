namespace Chisel.Core.SystemMonitor;

// How much the system monitor shows. Compact: only the CPU and RAM rings, when the full set would be too small to read.
// Standard: every chosen stat (with a label under each ring while it is readable). Full: adds a sparkline of the last minute.
public enum MonitorSize { Compact, Standard, Full }

// Where one gauge goes, in the widget's own coordinates.
public readonly record struct GaugeCell(double X, double Y, double Width, double Height);

// The system monitor's layout for the room it has, worked out when the widget's size or chosen stats change (never per sample).
// The gauges sit in a grid; the column count is whichever gives the biggest rings. Every font and stroke is a fraction of the
// ring, so squishing the widget in either direction shrinks the rings and their text together, and a gauge always fits its cell.
// Detail goes by ring size, not by the widget's width or height: sparklines, then labels, then the stats other than CPU and RAM
// are dropped only when keeping them would push the rings below the sizes below. Dropping detail frees room, so the ring is then
// held at the size it had when the detail went; the rings never grow as the widget shrinks. All thresholds live here.
public sealed class SystemMonitorLayout
{
    // The smallest ring that keeps its sparkline, its label, and every chosen stat (below that only CPU and RAM).
    public const double MinSparkRing = 64;
    public const double MinLabelRing = MinLabelFont / LabelFontFraction;
    public const double MinAllStatsRing = 52;
    public const double MaxRing = 150;

    // Proportions of the ring diameter.
    public const double StrokeFraction = 0.085;
    public const double MinStroke = 2;
    public const double MaxStroke = 10;
    public const double ValueFontFraction = 0.24;
    public const double MinValueFont = 6;
    public const double LabelFontFraction = 0.135;
    public const double MinLabelFont = 8;
    public const double MaxLabelFont = 14;
    public const double LabelLineHeight = 1.5;    // the label row, as a multiple of its font
    public const double SparkHeightFraction = 0.3;
    public const double SparkWidthFraction = 1.4;
    public const double SparkGapFraction = 0.06;  // between the label and the sparkline

    private SystemMonitorLayout(MonitorSize size, IReadOnlyList<SystemStat> stats, bool labels, bool sparks, double pad, int columns, int rows,
        double cellWidth, double cellHeight, double ring)
    {
        Size = size;
        Stats = stats;
        ShowLabels = labels;
        ShowSparklines = sparks;
        Pad = pad;
        Columns = columns;
        Rows = rows;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        Ring = ring;
        Stroke = Math.Clamp(ring * StrokeFraction, MinStroke, MaxStroke);
        ValueFont = Math.Max(MinValueFont, ring * ValueFontFraction);
        LabelFont = labels ? LabelFontFor(ring) : 0;
        LabelHeight = labels ? LabelFont * LabelLineHeight : 0;
        SparkGap = sparks ? ring * SparkGapFraction : 0;
        SparkHeight = sparks ? ring * SparkHeightFraction : 0;
        SparkWidth = sparks ? Math.Min(cellWidth, ring * SparkWidthFraction) : 0;
    }

    public MonitorSize Size { get; }
    public IReadOnlyList<SystemStat> Stats { get; }
    public bool ShowLabels { get; }
    public bool ShowSparklines { get; }
    public double Pad { get; }
    public int Columns { get; }
    public int Rows { get; }
    public double CellWidth { get; }
    public double CellHeight { get; }
    public double Ring { get; }
    public double Stroke { get; }
    public double ValueFont { get; }
    public double LabelFont { get; }
    public double LabelHeight { get; }
    public double SparkGap { get; }
    public double SparkHeight { get; }
    public double SparkWidth { get; }

    // The ring, its label and its sparkline, stacked.
    public double GaugeHeight => Ring + LabelHeight + SparkGap + SparkHeight;

    private double Gap => Pad;

    public GaugeCell Cell(int index)
    {
        var column = index % Columns;
        var row = index / Columns;
        return new GaugeCell(Pad + column * (CellWidth + Gap), Pad + row * (CellHeight + Gap), CellWidth, CellHeight);
    }

    // The stats shown at a size: the chosen ones (battery only when there is one), or in Compact just CPU and RAM. A compact
    // widget with neither chosen shows the first chosen stat so it is never blank by surprise.
    public static IReadOnlyList<SystemStat> Visible(MonitorSize size, SystemStats chosen, bool hasBattery)
    {
        var stats = new List<SystemStat>();
        foreach (var stat in Enum.GetValues<SystemStat>())
        {
            if (chosen.Includes(stat) && (stat != SystemStat.Battery || hasBattery))
            {
                stats.Add(stat);
            }
        }
        if (size == MonitorSize.Compact)
        {
            var core = stats.Where(s => s is SystemStat.Cpu or SystemStat.Ram).ToList();
            return core.Count > 0 ? core : stats.Take(1).ToList();
        }
        return stats;
    }

    public static SystemMonitorLayout For(double width, double height, SystemStats chosen, bool hasBattery)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        var pad = Math.Clamp(Math.Round(width * 0.025), 4, 12); // from the width only, so a shorter widget never gains room
        var size = MonitorSize.Full;
        var stats = Visible(size, chosen, hasBattery);
        var labels = true;
        var sparks = true;
        var cap = MaxRing;

        var fit = Fit(width, height, pad, stats.Count, labels, sparks, cap);
        if (fit.Ring < MinSparkRing)
        {
            size = MonitorSize.Standard;
            sparks = false;
            cap = MinSparkRing;
            fit = Fit(width, height, pad, stats.Count, labels, sparks, cap);
        }
        if (fit.Ring < MinLabelRing)
        {
            labels = false;
            cap = Math.Min(cap, Math.Floor(MinLabelRing));
            fit = Fit(width, height, pad, stats.Count, labels, sparks, cap);
        }
        if (fit.Ring < MinAllStatsRing)
        {
            var compact = Visible(MonitorSize.Compact, chosen, hasBattery);
            if (compact.Count < stats.Count)
            {
                size = MonitorSize.Compact;
                stats = compact;
                cap = MinAllStatsRing;
                fit = Fit(width, height, pad, stats.Count, labels, sparks, cap);
            }
        }
        return new SystemMonitorLayout(size, stats, labels, sparks, pad, fit.Columns, fit.Rows, fit.CellWidth, fit.CellHeight, fit.Ring);
    }

    private static double LabelFontFor(double ring) => Math.Min(ring * LabelFontFraction, MaxLabelFont);

    // The height a gauge with this ring needs.
    private static double HeightFor(double ring, bool labels, bool sparks) =>
        ring + (labels ? LabelFontFor(ring) * LabelLineHeight : 0) + (sparks ? ring * (SparkGapFraction + SparkHeightFraction) : 0);

    private static (int Columns, int Rows, double CellWidth, double CellHeight, double Ring) Fit(
        double width, double height, double pad, int count, bool labels, bool sparks, double cap)
    {
        var innerWidth = Math.Max(0, width - 2 * pad);
        var innerHeight = Math.Max(0, height - 2 * pad);
        if (count == 0)
        {
            return (1, 1, innerWidth, innerHeight, 0);
        }
        var best = (Columns: 1, Rows: count, CellWidth: 0d, CellHeight: 0d, Ring: -1d);
        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var cellWidth = Math.Max(0, (innerWidth - pad * (columns - 1)) / columns);
            var cellHeight = Math.Max(0, (innerHeight - pad * (rows - 1)) / rows);
            // The largest ring whose gauge fits the cell (the height it needs grows with the ring).
            double low = 0, high = Math.Min(Math.Min(cellWidth, cap), cellHeight);
            for (var i = 0; i < 40; i++)
            {
                var mid = (low + high) / 2;
                if (HeightFor(mid, labels, sparks) <= cellHeight)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }
            var ring = Math.Floor(low);
            if (ring > best.Ring)
            {
                best = (columns, rows, cellWidth, cellHeight, ring);
            }
        }
        return best;
    }
}
