using System;
using Chisel.Core.Clock;

namespace Chisel.Core.Calculator;

// The calculator's options, saved as one record (replace-all). Advanced: show the advanced panel when the widget is wide enough.
// Angle: whether trig works in degrees or radians when the calculator opens.
public sealed record CalculatorSettings(bool Advanced = false, AngleMode Angle = AngleMode.Degrees)
{
    public static CalculatorSettings Default { get; } = new();
}

public interface ICalculatorSettingsRepository
{
    CalculatorSettings Get();
    void Save(CalculatorSettings settings);
}

// How the calculator is laid out in the room it has: whether the advanced panel fits, and the sizes of the gaps, the keys and the
// text, all worked out from the widget's actual size. Gaps, padding and the display's text come from the shared type scale (the same
// sizes as the clock beside it); key text follows the key it sits on, so it never outgrows it.
// The keys are one grid: the basic keypad (4 columns) with, when it fits, the advanced panel (5 columns) to its left, and the
// display above both. There are 5 rows of keys either way.
public sealed record CalculatorLayout(bool ShowAdvanced, int Columns, double Pad, double Gap, double KeyWidth, double KeyHeight,
    double DisplayHeight, double ResultFont, double SecondaryFont, double KeyFont, double SmallKeyFont, double MinHeight)
{
    public const int BasicColumns = 4;
    public const int AdvancedColumns = 5;
    public const int Rows = 5;

    // The narrowest a key may get before the advanced panel is dropped for the basic keypad.
    public const double MinKeyWidth = 44;

    // The shortest a key may get. A widget shorter than MinHeight scrolls instead of squishing the keys.
    public const double MinKeyHeight = 30;

    // The type scale grows with the room, so the height the keys need depends on the height they get. A widget too short for its own
    // minimum is laid out at that minimum (the card scrolls the rest): the result is the same for any height at or below it.
    public static CalculatorLayout For(double width, double height, bool advanced)
    {
        var layout = Compute(width, height, advanced);
        for (var i = 0; i < 8 && layout.MinHeight > height + 0.01; i++)
        {
            height = layout.MinHeight;
            layout = Compute(width, height, advanced);
        }
        return layout;
    }

    private static CalculatorLayout Compute(double width, double height, bool advanced)
    {
        var scale = ClockTypeScale.For(width, height);
        var pad = scale.Pad;
        var gap = scale.Gap;
        var all = BasicColumns + AdvancedColumns;
        var fits = (width - 2 * pad - (all - 1) * gap) / all >= MinKeyWidth;
        var show = advanced && fits;
        var columns = show ? all : BasicColumns;

        var keyWidth = (width - 2 * pad - (columns - 1) * gap) / columns;
        var secondary = scale.Caption;
        var result = scale.Display;
        var displayHeight = pad + secondary * 1.4 + result * 1.25;
        var keyHeight = Math.Max(MinKeyHeight, (height - 2 * pad - displayHeight - Rows * gap) / Rows);
        var minHeight = 2 * pad + displayHeight + Rows * (MinKeyHeight + gap);

        var smaller = Math.Min(keyWidth, keyHeight);
        var keyFont = Math.Min(Math.Max(smaller * 0.36, 10), scale.Title * 1.5);
        var smallFont = Math.Max(9, Math.Min(Math.Min(scale.Body, keyHeight * 0.34), (keyWidth - 6) / 3.2));
        return new CalculatorLayout(show, columns, pad, gap, keyWidth, keyHeight, displayHeight, result, secondary, keyFont, smallFont, minHeight);
    }
}
