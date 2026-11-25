using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Chisel.Core.SystemMonitor;

namespace Chisel.App.Views.Widgets;

// The theme colors every gauge of one widget draws with, looked up once per frame and rebuilt only when the theme or accent
// changes (Version tells the gauges to rebuild their pens and text). High is the accent shifted part way to a warm tone, for
// a gauge above SystemMonitorLayout's high-load mark; it is a nudge, not an alarm.
public sealed class GaugePaint
{
    public int Version { get; private set; }
    public IBrush Track { get; private set; } = Brushes.Transparent;
    public IBrush Accent { get; private set; } = Brushes.Transparent;
    public IBrush High { get; private set; } = Brushes.Transparent;
    public IBrush Text { get; private set; } = Brushes.Transparent;
    public IBrush Dim { get; private set; } = Brushes.Transparent;
    public IBrush Spark { get; private set; } = Brushes.Transparent;
    private ThemeVariant? _variant;

    public void Refresh(Control control)
    {
        var track = Theming.ThemeService.Brush(control, "GridLineBrush");
        var accent = Theming.ThemeService.Brush(control, "AccentBrush");
        var text = Theming.ThemeService.Brush(control, "TextBrush");
        var dim = Theming.ThemeService.Brush(control, "DimTextBrush");
        var variant = control.ActualThemeVariant;
        if (ReferenceEquals(track, Track) && ReferenceEquals(accent, Accent) && ReferenceEquals(text, Text) && ReferenceEquals(dim, Dim) && variant == _variant)
        {
            return;
        }
        Track = track;
        Accent = accent;
        Text = text;
        Dim = dim;
        _variant = variant;
        if (accent is ISolidColorBrush solid)
        {
            High = new SolidColorBrush(Theming.ThemeColors.HighLoad(solid.Color, variant != ThemeVariant.Light)).ToImmutable();
            Spark = new SolidColorBrush(solid.Color, 0.75).ToImmutable();
        }
        else
        {
            High = Spark = accent;
        }
        Version++;
    }
}

// One stat drawn as a ring: a faint full track, an arc for the value (clockwise from the top), the value in the middle, a label
// under it and, at the largest size, a sparkline of the last minute. Sizes come from SystemMonitorLayout. Show() works out what
// would be on screen and redraws only if that changed (Changes counts it); text and arcs are rebuilt only then, and arcs are
// cached per whole percent, so a steady sample and every redraw allocate nothing.
public sealed class StatGauge : Control
{
    // At or above this a CPU, RAM or disk gauge takes the High color.
    public const double HighLoad = 85;

    // The network ring is scaled to the busiest rate of the last minute, but never below 1 Mbps (in bytes per second).
    private const double MinNetworkScale = 125_000;
    private const double HistorySeconds = 60;
    private const ulong GB = 1024UL * 1024 * 1024;

    private static readonly Typeface ValueFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface LabelFace = new(FontFamily.Default);

    private readonly GaugePaint _paint;
    private readonly Geometry?[] _arcs = new Geometry?[101];
    private SystemMonitorLayout? _layout;
    private SystemMonitorSampler? _sampler;
    private long _key = long.MinValue;
    private int _arcPercent;
    private bool _high;
    private string _valueText = "";
    private string _value2Text = "";
    private string _labelText = "";
    private TextShape? _value;
    private TextShape? _value2;
    private TextShape? _label;
    private int _textVersion = -1;
    private int _penVersion = -1;
    private Pen? _trackPen;
    private Pen? _arcPen;
    private Pen? _highPen;
    private Pen? _sparkPen;
    private Pen? _baselinePen;
    private Geometry? _downMark;
    private Geometry? _upMark;

    public StatGauge(SystemStat stat, GaugePaint paint)
    {
        Stat = stat;
        _paint = paint;
        AutomationProperties.SetName(this, Name(stat));
    }

    public SystemStat Stat { get; }

    // How many times what the gauge shows has changed (and it asked to be redrawn).
    public int Changes { get; private set; }

    private static string Name(SystemStat stat) => stat switch
    {
        SystemStat.Cpu => "CPU",
        SystemStat.Ram => "RAM",
        SystemStat.Disk => "Disk",
        SystemStat.Network => "Network",
        _ => "Battery",
    };

    // A new size: drop everything built for the old one.
    public void Configure(SystemMonitorLayout layout)
    {
        _layout = layout;
        Array.Clear(_arcs);
        _key = long.MinValue;
        _textVersion = -1;
        _penVersion = -1;
        _downMark = _upMark = null;
        InvalidateVisual();
    }

    public void Show(SystemMonitorSampler sampler)
    {
        _sampler = sampler;
        if (_layout is null)
        {
            return;
        }
        var s = sampler.Current;
        long key;
        double fraction;
        switch (Stat)
        {
            case SystemStat.Cpu:
                key = StatFormat.WholePercent(s.CpuPercent ?? 0);
                fraction = key / 100.0;
                break;
            case SystemStat.Ram:
                key = UsageKey(s.RamPercent, s.RamUsed, s.RamTotal);
                fraction = StatFormat.WholePercent(s.RamPercent ?? 0) / 100.0;
                break;
            case SystemStat.Disk:
                key = UsageKey(s.DiskPercent, s.DiskUsed, s.DiskTotal);
                fraction = StatFormat.WholePercent(s.DiskPercent ?? 0) / 100.0;
                break;
            case SystemStat.Network:
                var down = s.DownBytesPerSecond ?? 0;
                key = RateKey(down) * 1_000_000 + RateKey(s.UpBytesPerSecond ?? 0);
                fraction = down / Math.Max(MinNetworkScale, sampler.History(SystemStat.Network).MaxOfLast(Window(sampler)));
                break;
            default:
                key = (s.BatteryPercent ?? 0) * 2 + (s.Charging ? 1 : 0);
                fraction = (s.BatteryPercent ?? 0) / 100.0;
                break;
        }
        var arc = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100);
        var changed = key != _key || arc != _arcPercent || _layout.ShowSparklines;
        if (key != _key)
        {
            _key = key;
            Describe(s);
        }
        _arcPercent = arc;
        if (changed)
        {
            Changes++;
            InvalidateVisual();
        }
    }

    // The number of samples that make about a minute at the sampler's rate.
    private static int Window(SystemMonitorSampler sampler) =>
        Math.Clamp((int)Math.Ceiling(HistorySeconds / Math.Max(0.001, sampler.Interval.TotalSeconds)), 2, SystemMonitorSampler.HistoryCapacity);

    // Changes exactly when the percentage or the "used/total" text would.
    private static long UsageKey(double? percent, ulong used, ulong total) =>
        StatFormat.WholePercent(percent ?? 0) * 100_000_000L + (long)Math.Round(used / (double)GB * 10) * 10_000 + (long)Math.Round(total / (double)GB);

    // Changes when the short rate text would: the unit, then the value to the shown precision.
    private static long RateKey(double bytesPerSecond)
    {
        var bits = Math.Max(0, bytesPerSecond) * 8;
        var (unit, value) = bits >= 1e9 ? (2, bits / 1e9) : bits >= 1e6 ? (1, bits / 1e6) : (0, bits / 1e3);
        return unit * 100_000 + (long)Math.Round(value < 100 ? value * 10 : Math.Round(value) * 10);
    }

    // The texts for a new value (only called when it changed).
    private void Describe(SystemSnapshot s)
    {
        switch (Stat)
        {
            case SystemStat.Cpu:
                var cpu = StatFormat.WholePercent(s.CpuPercent ?? 0);
                Set($"{cpu}%", "", "CPU", StatFormat.SpokenPercent("CPU", cpu));
                break;
            case SystemStat.Ram:
                var ram = StatFormat.WholePercent(s.RamPercent ?? 0);
                Set($"{ram}%", "", "RAM " + StatFormat.UsedOfTotal(s.RamUsed, s.RamTotal), StatFormat.SpokenUsage("RAM", ram, s.RamUsed, s.RamTotal));
                break;
            case SystemStat.Disk:
                var disk = StatFormat.WholePercent(s.DiskPercent ?? 0);
                Set($"{disk}%", "", "Disk " + StatFormat.UsedOfTotal(s.DiskUsed, s.DiskTotal), StatFormat.SpokenUsage("Disk", disk, s.DiskUsed, s.DiskTotal));
                break;
            case SystemStat.Network:
                var down = s.DownBytesPerSecond ?? 0;
                var up = s.UpBytesPerSecond ?? 0;
                Set(StatFormat.ShortRate(down), StatFormat.ShortRate(up), "Network", StatFormat.SpokenNetwork(down, up));
                break;
            default:
                var battery = s.BatteryPercent ?? 0;
                Set($"{battery}%", "", s.Charging ? "Charging" : "Battery", StatFormat.SpokenBattery(battery, s.Charging));
                break;
        }
        _high = Stat is SystemStat.Cpu or SystemStat.Ram or SystemStat.Disk && (Stat switch
        {
            SystemStat.Cpu => s.CpuPercent,
            SystemStat.Ram => s.RamPercent,
            _ => s.DiskPercent,
        } ?? 0) >= HighLoad;
    }

    private void Set(string value, string value2, string label, string spoken)
    {
        _valueText = value;
        _value2Text = value2;
        _labelText = label;
        _textVersion = -1;
        AutomationProperties.SetName(this, spoken);
    }

    // Drawing ----------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context) => Draw(context);

    // Draws the gauge at the origin of `context` (Render, or a test's bitmap).
    public void Draw(DrawingContext context)
    {
        if (_layout is not { Ring: > 2 } layout)
        {
            return;
        }
        _paint.Refresh(this);
        if (_penVersion != _paint.Version)
        {
            BuildPens(layout);
        }
        if (_textVersion != _paint.Version)
        {
            BuildText(layout);
        }

        var width = Bounds.Width > 0 ? Bounds.Width : layout.CellWidth;
        var height = Bounds.Height > 0 ? Bounds.Height : layout.CellHeight;
        var top = Math.Max(0, (height - layout.GaugeHeight) / 2);
        var center = new Point(width / 2, top + layout.Ring / 2);
        var radius = (layout.Ring - layout.Stroke) / 2;

        context.DrawEllipse(null, _trackPen, center, radius, radius);
        if (_arcPercent > 0)
        {
            context.DrawGeometry(null, _high ? _highPen : _arcPen, Arc(_arcPercent, center, radius));
        }

        if (_value2 is null)
        {
            _value!.Draw(context, _paint.Text, center.X - _value.Width / 2, center.Y - _value.Height / 2);
        }
        else
        {
            // Network: download over upload, each after a small triangle pointing its way.
            var mark = _value!.Height * 0.32;
            var gap = mark * 0.6;
            var y1 = center.Y - _value.Height;
            var x1 = center.X - (_value.Width + mark + gap) / 2;
            var x2 = center.X - (_value2.Width + mark + gap) / 2;
            using (context.PushTransform(Matrix.CreateTranslation(x1, y1 + _value.Height / 2)))
            {
                context.DrawGeometry(_paint.Text, null, _downMark!);
            }
            _value.Draw(context, _paint.Text, x1 + mark + gap, y1);
            using (context.PushTransform(Matrix.CreateTranslation(x2, center.Y + _value2.Height / 2)))
            {
                context.DrawGeometry(_paint.Dim, null, _upMark!);
            }
            _value2.Draw(context, _paint.Dim, x2 + mark + gap, center.Y);
        }

        var below = top + layout.Ring;
        if (layout.ShowLabels && _label is not null)
        {
            _label.Draw(context, _paint.Dim, Math.Max(0, (width - _label.Width) / 2), below + (layout.LabelHeight - _label.Height) / 2);
        }
        if (layout.ShowSparklines && _sampler is { } sampler)
        {
            DrawSparkline(context, sampler, layout, new Rect((width - layout.SparkWidth) / 2, below + layout.LabelHeight + layout.SparkGap, layout.SparkWidth, layout.SparkHeight));
        }
    }

    private void DrawSparkline(DrawingContext context, SystemMonitorSampler sampler, SystemMonitorLayout layout, Rect area)
    {
        var history = sampler.History(Stat);
        var window = Window(sampler);
        var count = Math.Min(history.Count, window);
        context.DrawLine(_baselinePen!, area.BottomLeft, area.BottomRight);
        if (count < 2)
        {
            return;
        }
        var first = history.Count - count;
        var max = Stat == SystemStat.Network ? Math.Max(MinNetworkScale, history.MaxOfLast(window)) : 100;
        var step = area.Width / (window - 1);
        var x = area.Right - (count - 1) * step;
        var previous = new Point(x, area.Bottom - Math.Clamp(history[first] / max, 0, 1) * area.Height);
        for (var i = 1; i < count; i++)
        {
            x += step;
            var next = new Point(x, area.Bottom - Math.Clamp(history[first + i] / max, 0, 1) * area.Height);
            context.DrawLine(_sparkPen!, previous, next);
            previous = next;
        }
    }

    // The value arc for a whole percent, built once per size.
    private Geometry Arc(int percent, Point center, double radius)
    {
        if (_arcs[percent] is { } cached)
        {
            return cached;
        }
        Geometry geometry;
        if (percent >= 100)
        {
            geometry = new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
        }
        else
        {
            var angle = percent / 100.0 * 2 * Math.PI;
            var start = new Point(center.X, center.Y - radius);
            var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
            var stream = new StreamGeometry();
            using (var g = stream.Open())
            {
                g.BeginFigure(start, false);
                g.ArcTo(end, new Size(radius, radius), 0, percent > 50, SweepDirection.Clockwise);
                g.EndFigure(false);
            }
            geometry = stream;
        }
        _arcs[percent] = geometry;
        return geometry;
    }

    private void BuildPens(SystemMonitorLayout layout)
    {
        _trackPen = new Pen(_paint.Track, layout.Stroke);
        _arcPen = new Pen(_paint.Accent, layout.Stroke, lineCap: PenLineCap.Round);
        _highPen = new Pen(_paint.High, layout.Stroke, lineCap: PenLineCap.Round);
        _sparkPen = new Pen(_paint.Spark, Math.Max(1, layout.Stroke * 0.3), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _baselinePen = new Pen(_paint.Track, 1);
        _penVersion = _paint.Version;
    }

    private void BuildText(SystemMonitorLayout layout)
    {
        var two = _value2Text.Length > 0;
        var size = two ? layout.ValueFont * 0.72 : layout.ValueFont;
        _value = TextShape.Of(new FormattedText(_valueText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, ValueFace, size, _paint.Text));
        _value2 = two ? TextShape.Of(new FormattedText(_value2Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, ValueFace, size * 0.85, _paint.Dim)) : null;
        _label = layout.ShowLabels
            ? TextShape.Of(new FormattedText(_labelText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, layout.LabelFont, _paint.Dim)
            {
                MaxTextWidth = Math.Max(1, layout.CellWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis,
            })
            : null;
        if (two && _downMark is null)
        {
            var m = _value.Height * 0.32;
            _downMark = Triangle(m, pointsDown: true);
            _upMark = Triangle(m * 0.85, pointsDown: false);
        }
        _textVersion = _paint.Version;
    }

    // Text turned into a shape once, when it changes: drawing a cached shape allocates nothing, where drawing text lays it out
    // again on every frame.
    private sealed class TextShape(Geometry? geometry, double width, double height)
    {
        public double Width { get; } = width;
        public double Height { get; } = height;

        public static TextShape Of(FormattedText text) => new(text.BuildGeometry(new Point(0, 0)), text.WidthIncludingTrailingWhitespace, text.Height);

        public void Draw(DrawingContext context, IBrush brush, double x, double y)
        {
            if (geometry is null)
            {
                return;
            }
            using (context.PushTransform(Matrix.CreateTranslation(x, y)))
            {
                context.DrawGeometry(brush, null, geometry);
            }
        }
    }

    // A small solid triangle of width `size`, centered vertically on y = 0.
    private static Geometry Triangle(double size, bool pointsDown)
    {
        var half = size / 2;
        var stream = new StreamGeometry();
        using (var g = stream.Open())
        {
            g.BeginFigure(pointsDown ? new Point(0, -half * 0.7) : new Point(0, half * 0.7), true);
            g.LineTo(pointsDown ? new Point(size, -half * 0.7) : new Point(size, half * 0.7));
            g.LineTo(pointsDown ? new Point(half, half * 0.7) : new Point(half, -half * 0.7));
            g.EndFigure(true);
        }
        return stream;
    }
}
