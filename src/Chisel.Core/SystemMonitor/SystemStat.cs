namespace Chisel.Core.SystemMonitor;

// One reading the system monitor can show, in the order the widget lays them out.
public enum SystemStat { Cpu, Ram, Disk, Network, Battery }

// A set of stats, as the user chooses them in settings and as they are saved.
[Flags]
public enum SystemStats
{
    None = 0,
    Cpu = 1 << SystemStat.Cpu,
    Ram = 1 << SystemStat.Ram,
    Disk = 1 << SystemStat.Disk,
    Network = 1 << SystemStat.Network,
    Battery = 1 << SystemStat.Battery,
    All = Cpu | Ram | Disk | Network | Battery,
}

public static class SystemStatsExtensions
{
    public static bool Includes(this SystemStats set, SystemStat stat) => (set & (SystemStats)(1 << (int)stat)) != 0;

    public static SystemStats With(this SystemStats set, SystemStat stat, bool on) =>
        on ? set | (SystemStats)(1 << (int)stat) : set & ~(SystemStats)(1 << (int)stat);
}
