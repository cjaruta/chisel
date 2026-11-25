using System;
using System.Linq;

namespace Chisel.App.Startup;

// Command-line switches Chisel understands. "--minimized" is what the start-with-Windows entry passes.
public sealed record StartupArguments(bool Minimized)
{
    public const string MinimizedSwitch = "--minimized";

    public static StartupArguments Parse(string[]? args) =>
        new(args?.Any(a => string.Equals(a?.Trim(), MinimizedSwitch, StringComparison.OrdinalIgnoreCase)) == true);
}
