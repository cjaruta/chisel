namespace Chisel.Core.Habits;

// How much the habit widget shows for its size.
public enum HabitDetail
{
    Summary, // only the progress ring with "3/5" inside
    List,    // a summary line, then each habit with its checkbox
    Dots,    // and under each name its streak, and the last 7 days as dots
    Grid,    // the last 5 weeks as a small grid in place of the dots
}

// Every size in the widget, worked out from the room it has. All the thresholds are here, to tune in one place. Sizes scale with
// the width (within limits), and each level is only chosen when its rows fit, so nothing is clipped. Rows that do not all fit
// scroll.
public sealed record HabitLayout(
    HabitDetail Detail, double Pad, double Scale, double Gap, double RingSize, double CountFont, double LabelFont,
    double SummaryHeight, double SummaryFont, double RowHeight, double CheckSize, double NameFont, double SmallFont,
    double DotSize, double DotGap, int HistoryDays, double HistoryWidth, double HistoryHeight)
{
    // The inner room (inside the padding) each level needs.
    public const double ListMinWidth = 170;
    public const double ListMinHeight = 110;
    public const double DotsMinWidth = 300;
    public const double DotsMinHeight = 180;
    public const double GridMinWidth = 360;
    public const double GridMinHeight = 300;

    // Scale 1 at this inner width, kept between the limits.
    public const double RefWidth = 340;
    public const double MinScale = 0.85;
    public const double MaxScale = 1.3;

    public const double BaseNameFont = 14;
    public const double BaseSmallFont = 11;
    public const double BaseCheck = 20;
    public const double BaseGap = 8;
    public const double BaseDot = 9;
    public const double BaseDotGap = 3;
    public const double BaseSummaryHeight = 30;
    public const double BaseSummaryFont = 13;
    public const double LineHeight = 1.3;
    public const double MinNameWidth = 60;

    public const int WeekDays = 7;
    public const int GridWeeks = 5;
    public const double MaxRingSize = 180;    // the Summary ring stops growing here
    public const double RingLabelFrom = 60;   // from this ring size "done" is written under the count
    public const double CountFontPart = 0.24; // the count's font as a part of the ring (keeps "30/30" inside it)

    public bool ShowList => Detail >= HabitDetail.List;
    public bool ShowStreaks => Detail >= HabitDetail.Dots;
    public bool ShowRingLabel => Detail == HabitDetail.Summary && RingSize >= RingLabelFrom;

    public static HabitLayout For(double width, double height)
    {
        width = Math.Max(width, 0);
        height = Math.Max(height, 0);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.03), 2, 10);
        var w = Math.Max(0, width - 2 * pad);
        var h = Math.Max(0, height - 2 * pad);

        var detail = w >= GridMinWidth && h >= GridMinHeight ? HabitDetail.Grid
            : w >= DotsMinWidth && h >= DotsMinHeight ? HabitDetail.Dots
            : w >= ListMinWidth && h >= ListMinHeight ? HabitDetail.List
            : HabitDetail.Summary;

        var s = Math.Clamp(w / RefWidth, MinScale, MaxScale);
        var name = BaseNameFont * s;
        var small = BaseSmallFont * s;
        var check = BaseCheck * s;
        var dot = BaseDot * s;
        var dotGap = BaseDotGap * s;
        var days = detail switch { HabitDetail.Grid => WeekDays * GridWeeks, HabitDetail.Dots => WeekDays, _ => 0 };
        var historyWidth = days > 0 ? WeekDays * dot + (WeekDays - 1) * dotGap : 0;
        var weeks = days / WeekDays;
        var historyHeight = weeks > 0 ? weeks * dot + (weeks - 1) * dotGap : 0;
        var text = detail >= HabitDetail.Dots ? (name + small) * LineHeight : Math.Max(check, name * LineHeight);
        var row = Math.Max(text, historyHeight) + 10 * s;

        if (detail == HabitDetail.Summary)
        {
            var ring = Math.Min(Math.Min(w, h), MaxRingSize);
            return new HabitLayout(detail, pad, s, BaseGap * s, ring, ring * CountFontPart, Math.Clamp(ring * 0.1, 0, 14),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        var summary = BaseSummaryHeight * s;
        return new HabitLayout(detail, pad, s, BaseGap * s, summary * 0.7, 0, 0, summary, BaseSummaryFont * s, row, check,
            name, small, dot, dotGap, days, historyWidth, historyHeight);
    }
}
