namespace Chisel.Core.Calculator;

// Every key the calculator has. Digit0..Digit9 are consecutive so a digit key is Digit0 + n.
public enum CalcKey
{
    Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,
    Decimal, Add, Subtract, Multiply, Divide, Equals, Clear, Negate, Percent, Backspace,
    // Advanced keys.
    OpenParen, CloseParen, Sin, Cos, Tan, Asin, Acos, Atan, Ln, Log, Sqrt, Square, Power, Reciprocal, Pi, E, Factorial,
    MemoryClear, MemoryRecall, MemoryAdd, MemorySubtract, ToggleAngle,
}

public enum AngleMode
{
    Degrees,
    Radians,
}

// A calculation that has no answer (divide by zero, out of range, not a number). The calculator shows "Error".
public sealed class CalculatorException(string message) : Exception(message);
