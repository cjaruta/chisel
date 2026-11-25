namespace Chisel.Core.Clipboard;

// How much the clipboard history widget shows for its size.
public enum ClipboardDetail
{
    Latest, // the latest item and how many there are
    List,   // a scrolling list of items, one line each with its length
    Full,   // and a search box above, and each item's time under its preview
}

// Every size in the widget, worked out from the room it has. All the thresholds are here, to tune in one place. Sizes scale with
// the width (within limits), and shrink further when the height or width would not hold the least content, so nothing is clipped.
public sealed record ClipboardLayout(
    ClipboardDetail Detail, double Pad, double Scale, double Gap, double RowHeight, double PreviewFont, double MetaFont,
    double SearchHeight, double ButtonHeight, double ButtonFont, double IntroFont, double IntroLineHeight, int IntroLines)
{
    // The inner room (inside the padding) each level needs.
    public const double ListMinWidth = 180;
    public const double ListMinHeight = 110;
    public const double FullMinWidth = 280;
    public const double FullMinHeight = 220;

    // Scale 1 at this inner width, kept between the limits (then lowered if the content would not fit).
    public const double RefWidth = 300;
    public const double MinScale = 0.85;
    public const double MaxScale = 1.25;
    public const double MinRowWidth = 120; // a row's least width (scaled)

    public const double BaseRow = 30;      // one line: preview and length
    public const double BaseTallRow = 42;  // two lines: preview, then time and length
    public const double BasePreviewFont = 13;
    public const double BaseMetaFont = 11;
    public const double BaseSearch = 30;
    public const double BaseGap = 6;
    public const double BaseButton = 30;
    public const double BaseButtonFont = 13;
    public const double BaseIntroFont = 12.5;
    public const double IntroLineFactor = 1.35;
    public const int MinListRows = 3;      // the least rows the list must show at its level
    public const int MaxIntroLines = 8;

    public bool ShowList => Detail >= ClipboardDetail.List;
    public bool ShowSearch => Detail == ClipboardDetail.Full;
    public bool ShowTimes => Detail == ClipboardDetail.Full;

    // The least height the recording view needs at this level.
    public double MinContentHeight => MinHeight(Detail, Scale);

    public static ClipboardLayout For(double width, double height)
    {
        width = Math.Max(width, 0);
        height = Math.Max(height, 0);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.03), 2, 10);
        var w = Math.Max(0, width - 2 * pad);
        var h = Math.Max(0, height - 2 * pad);

        var detail = w >= FullMinWidth && h >= FullMinHeight ? ClipboardDetail.Full
            : w >= ListMinWidth && h >= ListMinHeight ? ClipboardDetail.List
            : ClipboardDetail.Latest;
        var s = Math.Clamp(w / RefWidth, MinScale, MaxScale);
        s = Math.Max(0, Math.Min(s, Math.Min(w / MinRowWidth, h / MinHeight(detail, 1))));

        var button = BaseButton * s;
        var gap = BaseGap * s;
        var introLine = BaseIntroFont * s * IntroLineFactor;
        var lines = introLine > 0 ? Math.Clamp((int)Math.Floor((h - button - gap) / introLine + 1e-9), 0, MaxIntroLines) : 0;
        return new ClipboardLayout(detail, pad, s, gap, (detail == ClipboardDetail.List ? BaseRow : BaseTallRow) * s,
            BasePreviewFont * s, BaseMetaFont * s, BaseSearch * s, button, BaseButtonFont * s, BaseIntroFont * s, introLine, lines);
    }

    private static double MinHeight(ClipboardDetail detail, double s) => s * detail switch
    {
        ClipboardDetail.Full => BaseSearch + BaseGap + MinListRows * BaseTallRow,
        ClipboardDetail.List => MinListRows * BaseRow,
        _ => BaseTallRow,
    };
}
