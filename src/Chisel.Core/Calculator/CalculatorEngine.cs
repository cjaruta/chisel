using System.Globalization;

namespace Chisel.Core.Calculator;

// The calculator behind the widget: keys in, display text out. No UI types. Display is localized (the culture's decimal separator);
// keys always mean the same thing whatever the culture.
public sealed class CalculatorEngine
{
    private readonly CultureInfo _culture;
    private readonly BasicPad _basic = new();

    public CalculatorEngine(CultureInfo? culture = null)
    {
        _culture = culture ?? CultureInfo.CurrentCulture;
    }

    // Advanced mode takes expressions with precedence, parentheses and functions; off it is the phone-style calculator.
    // Switching keeps the number on the display.
    public bool Advanced { get; set; }

    public bool IsError => _basic.IsError;

    // The big line: the number being typed, or the last result.
    public string Display => CalculatorFormat.Localize(_basic.Neutral, _culture);

    // The small line above it: the operation waiting for its second number.
    public string Secondary => CalculatorFormat.Localize(_basic.Pending, _culture);

    public void Press(CalcKey key) => _basic.Press(key);
}
