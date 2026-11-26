namespace Chisel.Core.Converter;

// How much the converter widget shows for its size.
public enum ConverterDetail
{
    Compact,  // the value with its unit, and the result with its unit: two rows
    Standard, // and a category picker above, a swap button by the value and a copy button by the result
    Full,     // category tabs in place of the picker, and a list of the value in other common units
}

// Every size in the widget, worked out from the room it has. All the thresholds are here, to tune in one place. Sizes scale with
// the width (within limits), and shrink further when the height or width would not hold the rows, so nothing is clipped.
public sealed record ConverterLayout(
    ConverterDetail Detail, double Pad, double Scale, double Gap, double FieldHeight, double FieldFont, double UnitWidth,
    double ButtonWidth, double ValueWidth, double TabHeight, double TabFont, int CommonRows, double CommonRowHeight, double CommonFont)
{
    // The inner room (inside the padding) each level needs.
    public const double StandardMinWidth = 240;
    public const double StandardMinHeight = 140;
    public const double FullMinWidth = 340;
    public const double FullMinHeight = 250;

    // Scale 1 at this inner width, kept between the limits (then lowered if the rows would not fit).
    public const double RefWidth = 340;
    public const double MinScale = 0.8;
    public const double MaxScale = 1.3;

    public const double BaseField = 34;
    public const double BaseFieldFont = 15;
    public const double BaseGap = 8;
    public const double BaseUnitWidth = 92;
    public const double BaseButtonWidth = 34;
    public const double BaseTabHeight = 28;
    public const double BaseTabFont = 12;
    public const double BaseCommonRow = 24;
    public const double BaseCommonFont = 13;
    public const double MinValueWidth = 60; // the value box never gets narrower than this (scaled)

    public const int TabColumns = 4;        // 8 categories as 2 rows of 4
    public const int TabRows = 2;
    public const int MinCommonRows = 2;
    public const int MaxCommonRows = 6;

    public bool ShowCategoryPicker => Detail == ConverterDetail.Standard;
    public bool ShowTabs => Detail == ConverterDetail.Full;
    public bool ShowButtons => Detail >= ConverterDetail.Standard;

    // The height of everything shown, top to bottom.
    public double ContentHeight => Heights(Detail, Scale, CommonRows);

    // The width of a value or result row.
    public double RowWidth => ValueWidth + Gap + UnitWidth + (ShowButtons ? Gap + ButtonWidth : 0);

    public static ConverterLayout For(double width, double height)
    {
        width = Math.Max(width, 0);
        height = Math.Max(height, 0);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.03), 2, 10);
        var w = Math.Max(0, width - 2 * pad);
        var h = Math.Max(0, height - 2 * pad);

        var detail = w >= FullMinWidth && h >= FullMinHeight ? ConverterDetail.Full
            : w >= StandardMinWidth && h >= StandardMinHeight ? ConverterDetail.Standard
            : ConverterDetail.Compact;
        var buttons = detail >= ConverterDetail.Standard;
        var rowWidth = MinValueWidth + BaseGap + BaseUnitWidth + (buttons ? BaseGap + BaseButtonWidth : 0);
        var minRows = detail == ConverterDetail.Full ? MinCommonRows : 0;
        var s = Math.Clamp(w / RefWidth, MinScale, MaxScale);
        s = Math.Max(0, Math.Min(s, Math.Min(w / rowWidth, h / Heights(detail, 1, minRows))));

        var rows = 0;
        if (detail == ConverterDetail.Full && s > 0)
        {
            var spare = h - Heights(detail, s, 0);
            rows = Math.Clamp((int)Math.Floor(spare / (BaseCommonRow * s) + 1e-9), 0, MaxCommonRows);
        }
        var gap = BaseGap * s;
        var unit = BaseUnitWidth * s;
        var button = buttons ? BaseButtonWidth * s : 0;
        var value = Math.Max(0, w - unit - gap - (buttons ? gap + button : 0));
        return new ConverterLayout(detail, pad, s, gap, BaseField * s, BaseFieldFont * s, unit, button, value,
            BaseTabHeight * s, BaseTabFont * s, rows, BaseCommonRow * s, BaseCommonFont * s);
    }

    // Two field rows; the picker row (Standard) or two rows of tabs (Full) above; the common list (Full) below.
    private static double Heights(ConverterDetail detail, double s, int commonRows)
    {
        var height = 2 * BaseField + BaseGap;
        if (detail == ConverterDetail.Standard)
        {
            height += BaseField + BaseGap;
        }
        else if (detail == ConverterDetail.Full)
        {
            height += TabRows * BaseTabHeight + BaseGap + BaseGap + commonRows * BaseCommonRow;
        }
        return height * s;
    }
}

// What the converter remembers: the category and the two units it was last set to.
public sealed record ConverterSettings(string Category, string From, string To)
{
    public static ConverterSettings Default { get; } =
        new(UnitCatalog.Length.Id, UnitCatalog.Length.DefaultFrom, UnitCatalog.Length.DefaultTo);

    // These settings with unknown ids put back to defaults (a removed unit, or a hand-edited database).
    public ConverterSettings Valid()
    {
        if (UnitCatalog.Get(Category) is not { } category)
        {
            return Default;
        }
        return new ConverterSettings(category.Id,
            category.Unit(From)?.Id ?? category.DefaultFrom,
            category.Unit(To)?.Id ?? category.DefaultTo);
    }
}

public interface IConverterSettingsRepository
{
    ConverterSettings Get();
    void Save(ConverterSettings settings);
}
