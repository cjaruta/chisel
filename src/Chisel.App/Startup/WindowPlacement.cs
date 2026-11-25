namespace Chisel.App.Startup;

// The window's normal bounds (Width/Height in device-independent units, X/Y in screen pixels) and whether it was maximized.
// When maximized, the bounds are the ones it goes back to. Files saved before Maximized existed load as not maximized.
public sealed record WindowPlacement(double Width, double Height, double? X, double? Y, bool Maximized = false);
