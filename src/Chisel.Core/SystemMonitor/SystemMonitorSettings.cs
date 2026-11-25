namespace Chisel.Core.SystemMonitor;

// The system monitor's options, saved as one record (replace-all): which stats it shows and how often it samples.
public sealed record SystemMonitorSettings(SystemStats Shown = SystemStats.All, int RefreshSeconds = 1)
{
    public static IReadOnlyList<int> RefreshChoices { get; } = [1, 2, 5];

    public static SystemMonitorSettings Default { get; } = new();

    // Unknown stat bits are dropped and a rate that is not offered falls back to one second.
    public SystemMonitorSettings Normalized() => new(Shown & SystemStats.All, RefreshChoices.Contains(RefreshSeconds) ? RefreshSeconds : 1);
}

public interface ISystemMonitorSettingsRepository
{
    SystemMonitorSettings Get();
    void Save(SystemMonitorSettings settings);
}
