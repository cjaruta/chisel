namespace Chisel.Core.Clock;

// What the timer shows in the room its slot has. As the slot shrinks the timer gives things up in a fixed order, always keeping the
// time: first the preset buttons thin out (all four, then +1m and +5m, then none), then Reset, then Start; the time itself shrinks
// last. Each step is only taken when the one before it would not fit, so nothing is ever cut off or squeezed over another part.
public sealed record TimerFit(double Display, int Presets, bool ShowReset, bool ShowStart, double Height)
{
    public const int AllPresets = 4;
    public const int FewPresets = 2;
}

public static class ClockTimerLayout
{
    // The widest the time can be (h:mm:ss) in digit widths, and how wide a digit is next to its font size.
    private const double TimeChars = 7;
    private const double CharWidth = 0.58;
    private const double LineHeight = 1.3;
    private const double ButtonLine = 1.2;   // a button's line is a little tighter than the big time's
    private const double ButtonBorder = 3;
    private const double ButtonChar = 0.65;  // a letter in a button, a little wide on purpose: a row that wraps must not be missed

    // The time is never shrunk below this unless there is no other way, and never below the absolute floor.
    public const double MinDisplay = 18;
    public const double FloorDisplay = 10;

    // The label lengths of the four presets: +1m, +5m, +10m and Clear.
    private static readonly int[] PresetChars = [3, 3, 4, 5];

    // The one width every preset chip has (the widest label, Clear), so the row is even.
    public static double ChipWidth(ClockTypeScale s) => PresetChars.Max() * s.Caption * ButtonChar + 2 * s.ButtonPadX + 2;

    public static TimerFit For(ClockTypeScale s)
    {
        var width = Math.Max(0, s.CellWidth - 2 * s.Pad);
        var height = Math.Max(0, s.CellHeight - 2 * s.Pad);
        var buttonH = s.Body * ButtonLine + 2 * s.ButtonPadY + ButtonBorder;
        var presetH = s.Caption * ButtonLine + 2 * s.ButtonPadY + ButtonBorder;

        // How many lines a row of buttons takes when it wraps in this width.
        int Lines(IEnumerable<double> widths)
        {
            var lines = 1;
            var used = 0.0;
            foreach (var w in widths)
            {
                if (used > 0 && used + s.Gap + w > width)
                {
                    lines++;
                    used = 0;
                }
                used += (used > 0 ? s.Gap : 0) + w;
            }
            return lines;
        }
        double PresetWidth(int index) => ChipWidth(s);
        var startW = Math.Max(s.ButtonMinWidth, 5 * s.Body * ButtonChar + 2 * s.ButtonPadX + 2);
        var resetW = startW; // Reset is as big as Start

        // The levels, most to least: the buttons each takes (height used besides the time, the gaps counted).
        var levels = new List<(int Presets, bool Reset, bool Start, double Other)>();
        double Other(int presets, bool reset, bool start)
        {
            var total = 0.0;
            if (presets > 0)
            {
                total += Lines(Enumerable.Range(0, presets).Select(PresetWidth)) * presetH + s.Gap;
            }
            if (start || reset)
            {
                var widths = new List<double>();
                if (start)
                {
                    widths.Add(startW);
                }
                if (reset)
                {
                    widths.Add(resetW);
                }
                total += Lines(widths) * buttonH + s.Gap;
            }
            return total;
        }
        levels.Add((TimerFit.AllPresets, true, true, Other(TimerFit.AllPresets, true, true)));
        levels.Add((TimerFit.FewPresets, true, true, Other(TimerFit.FewPresets, true, true)));
        levels.Add((0, true, true, Other(0, true, true)));
        levels.Add((0, false, true, Other(0, false, true)));
        levels.Add((0, false, false, 0));

        var wanted = Math.Max(MinDisplay, s.Display * 0.75);
        var widthFit = width / (TimeChars * CharWidth);
        for (var i = 0; i < levels.Count; i++)
        {
            var (presets, reset, start, other) = levels[i];
            var room = height - other;
            var font = Math.Min(s.Display, Math.Min(room / LineHeight, widthFit));
            var last = i == levels.Count - 1;
            if (font >= wanted || last)
            {
                font = Math.Max(FloorDisplay, font);
                return new TimerFit(font, presets, reset, start, font * LineHeight + other);
            }
        }
        throw new InvalidOperationException();
    }
}
