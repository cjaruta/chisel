namespace Chisel.Core.Countdown;

// How much the countdown shows. Single: only the nearest event, as a large number with a label. List: adds the other events
// in rows under it. Detailed: adds a progress bar to the nearest event and to every row.
public enum CountdownSize { Single, List, Detailed }

// The countdown's layout for the room it has, worked out from the widget's size (all thresholds here). The nearest event (the
// hero) sits at the top; rows follow until the height runs out, and the rest are counted in a "+N more" line, so nothing is
// clipped or overlaps. Fonts are fractions of the widget's size, held between readable limits, and shrink further only when
// the widget is too small for those limits (never clipping).
public sealed record CountdownLayout(
    CountdownSize Size, double Pad, double Gap,
    double HeroNumberFont, double HeroLabelFont, double HeroHeight,
    double RowNameFont, double RowSmallFont, double RowHeight, double ProgressHeight,
    int VisibleRows, bool ShowMore, double MoreHeight)
{
    public const double ListMinWidth = 200;
    public const double ListMinHeight = 170;
    public const double DetailedMinWidth = 260;
    public const double DetailedMinHeight = 250;

    // The hero's number, as a fraction of the room: most of a single-event widget, a band at the top of a list.
    public const double SingleNumberHeight = 0.5;
    public const double SingleNumberWidth = 0.28;
    public const double ListNumberHeight = 0.2;
    public const double ListNumberWidth = 0.17;
    public const double MinNumberFont = 18;
    public const double MaxNumberFont = 110;
    public const double LabelFraction = 0.26;
    public const double MinLabelFont = 10;
    public const double MaxLabelFont = 20;
    public const double MinRowFont = 11;
    public const double MaxRowFont = 16;
    public const double LineHeight = 1.35;     // a line of text, as a multiple of its font (TextBlock lines run about 1.33)
    public const double HeroPadding = 8;       // the hero button's own padding, top and bottom together

    public bool ShowRows => Size != CountdownSize.Single;
    public bool ShowProgress => ProgressHeight > 0;

    public static CountdownSize SizeFor(double width, double height) =>
        width >= DetailedMinWidth && height >= DetailedMinHeight ? CountdownSize.Detailed
        : width >= ListMinWidth && height >= ListMinHeight ? CountdownSize.List
        : CountdownSize.Single;

    // otherEvents: how many events there are besides the hero. size: the level to use when the caller decided it from another
    // height (the widget decides with its "+ Add" row counted, so hiding that row in Single does not flip the size back).
    public static CountdownLayout For(double width, double height, int otherEvents, CountdownSize? size = null)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        var level = size ?? SizeFor(width, height);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.04), 4, 14);
        var innerWidth = Math.Max(1, width - 2 * pad);
        var innerHeight = Math.Max(1, height - 2 * pad);

        var rowFont = Math.Clamp(Math.Round(Math.Min(innerWidth * 0.045, innerHeight * 0.06)), MinRowFont, MaxRowFont);
        var smallFont = Math.Round(rowFont * 0.8);
        var progress = level == CountdownSize.Detailed ? Math.Max(3, Math.Round(rowFont * 0.3)) : 0;
        var gap = Math.Round(rowFont * 0.6);

        // The hero: a number, then the label and the date under it, and in Detailed its progress bar.
        var single = level == CountdownSize.Single;
        var cap = Math.Clamp(Math.Min(innerHeight * (single ? SingleNumberHeight : ListNumberHeight), innerWidth * (single ? SingleNumberWidth : ListNumberWidth)),
            MinNumberFont, MaxNumberFont);
        var heroRoom = single ? innerHeight : innerHeight * 0.45;
        var heroExtra = progress > 0 ? progress + gap : 0;
        var number = Largest(cap, n => HeroHeightFor(n) + heroExtra <= heroRoom);
        var label = LabelFor(number);
        var heroHeight = HeroHeightFor(number) + heroExtra;

        var rowHeight = Math.Ceiling(rowFont * LineHeight + smallFont * LineHeight + rowFont * 0.5 + (progress > 0 ? progress + rowFont * 0.35 : 0));
        var moreHeight = Math.Ceiling(smallFont * 1.6);
        var rows = 0;
        var more = false;
        if (!single && otherEvents > 0)
        {
            var room = innerHeight - heroHeight - gap;
            if (otherEvents * rowHeight <= room)
            {
                rows = otherEvents;
            }
            else
            {
                rows = Math.Clamp((int)Math.Floor((room - moreHeight) / rowHeight), 0, otherEvents);
                more = room >= moreHeight;
            }
        }
        return new CountdownLayout(level, pad, gap, number, label, heroHeight, rowFont, smallFont, rowHeight, progress, rows, more, moreHeight);
    }

    private static double LabelFor(double number) => Math.Clamp(number * LabelFraction, Math.Min(MinLabelFont, number * 0.6), MaxLabelFont);

    private static double HeroHeightFor(double number) => Math.Ceiling(number * LineHeight + 2 * LabelFor(number) * LineHeight + HeroPadding);

    // The largest size up to `cap` that `fits`, to a tenth of a point (or the smallest tried when nothing fits).
    private static double Largest(double cap, Func<double, bool> fits)
    {
        double low = 1, high = cap;
        if (fits(high))
        {
            return high;
        }
        for (var i = 0; i < 30; i++)
        {
            var mid = (low + high) / 2;
            if (fits(mid))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        return Math.Floor(low * 10) / 10;
    }
}
