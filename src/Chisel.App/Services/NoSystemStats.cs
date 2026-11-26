using Chisel.Core.SystemMonitor;

namespace Chisel.App.Services;

// A stats source that can read nothing, so every gauge stays hidden. The default when none is given (tests that do not use it).
public sealed class NoSystemStats : ISystemStatsSource
{
    public void Read(ref RawSystemStats stats) { }

    public void Dispose() { }
}
