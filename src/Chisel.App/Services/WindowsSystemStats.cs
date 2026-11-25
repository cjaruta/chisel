using System;
using System.IO;
using System.Runtime.InteropServices;
using Chisel.Core.SystemMonitor;

namespace Chisel.App.Services;

// Reads the system monitor's stats from plain Win32 calls that any user may make (no admin rights, no network, no WMI or
// performance counters): GetSystemTimes for CPU, GlobalMemoryStatusEx for RAM, GetDiskFreeSpaceEx for the system drive,
// GetIfTable2 for network bytes, GetSystemPowerStatus for the battery. None of them allocates managed memory. A call that fails
// leaves its stat unavailable for that reading; one that is missing altogether is not tried again.
public sealed class WindowsSystemStats : ISystemStatsSource
{
    private readonly string _systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    private bool _noNetwork;
    private bool _noBattery;

    public void Read(ref RawSystemStats stats)
    {
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            // Kernel time includes idle time.
            stats.HasCpu = true;
            stats.CpuIdle = (ulong)idle;
            stats.CpuTotal = (ulong)kernel + (ulong)user;
        }

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref memory))
        {
            stats.HasMemory = true;
            stats.MemoryTotal = memory.TotalPhys;
            stats.MemoryAvailable = memory.AvailPhys;
        }

        if (GetDiskFreeSpaceEx(_systemDrive, out _, out var total, out var free))
        {
            stats.HasDisk = true;
            stats.DiskTotal = total;
            stats.DiskFree = free;
        }

        if (!_noNetwork)
        {
            ReadNetwork(ref stats);
        }
        if (!_noBattery)
        {
            ReadBattery(ref stats);
        }
    }

    // MIB_IF_TABLE2 is a ULONG count, padded to 8, then MIB_IF_ROW2 rows of 1352 bytes. Only the fields used here are read, at
    // their offsets in the row. Counted: physical (hardware, not filter) interfaces that are up and are not loopback, so virtual
    // switches and filter drivers do not count the same bytes twice.
    private const int RowsOffset = 8;
    private const int RowSize = 1352;
    private const int TypeOffset = 1128;
    private const int FlagsOffset = 1152;
    private const int OperStatusOffset = 1156;
    private const int InOctetsOffset = 1208;
    private const int OutOctetsOffset = 1280;
    private const byte HardwareInterface = 1;
    private const byte FilterInterface = 2;
    private const int OperStatusUp = 1;
    private const int SoftwareLoopback = 24;

    private void ReadNetwork(ref RawSystemStats stats)
    {
        IntPtr table;
        try
        {
            if (GetIfTable2(out table) != 0)
            {
                return;
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _noNetwork = true;
            return;
        }
        try
        {
            var count = Marshal.ReadInt32(table);
            ulong received = 0, sent = 0;
            for (var i = 0; i < count; i++)
            {
                var row = table + RowsOffset + i * RowSize;
                var flags = Marshal.ReadByte(row, FlagsOffset);
                if ((flags & HardwareInterface) == 0 || (flags & FilterInterface) != 0
                    || Marshal.ReadInt32(row, OperStatusOffset) != OperStatusUp || Marshal.ReadInt32(row, TypeOffset) == SoftwareLoopback)
                {
                    continue;
                }
                received += (ulong)Marshal.ReadInt64(row, InOctetsOffset);
                sent += (ulong)Marshal.ReadInt64(row, OutOctetsOffset);
            }
            stats.HasNetwork = true;
            stats.NetworkReceived = received;
            stats.NetworkSent = sent;
        }
        finally
        {
            FreeMibTable(table);
        }
    }

    private void ReadBattery(ref RawSystemStats stats)
    {
        try
        {
            if (!GetSystemPowerStatus(out var power))
            {
                return;
            }
            // 128: no system battery; 255: unknown status. A desktop reports one of these and the gauge stays hidden.
            if (power.BatteryFlag is 128 or 255 || power.BatteryLifePercent > 100)
            {
                return;
            }
            stats.HasBattery = true;
            stats.BatteryPercent = power.BatteryLifePercent;
            stats.Charging = power.ACLineStatus == 1;
        }
        catch (EntryPointNotFoundException)
        {
            _noBattery = true;
        }
    }

    public void Dispose()
    {
        // Nothing is held between readings: the network table is freed after each one.
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetDiskFreeSpaceExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong freeToCaller, out ulong total, out ulong free);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetIfTable2(out IntPtr table);

    [DllImport("iphlpapi.dll")]
    private static extern void FreeMibTable(IntPtr memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
