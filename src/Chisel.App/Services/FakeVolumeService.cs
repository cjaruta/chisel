using System;
using System.Collections.Generic;
using Chisel.Core.Media;

namespace Chisel.App.Services;

// A volume service that only remembers what it was told. Like the real one it raises Changed after every write,
// and External* simulates the volume being changed somewhere else. Used by tests and capture scenes.
public sealed class FakeVolumeService : IVolumeService
{
    public bool IsAvailable { get; set; } = true;
    public double Level { get; private set; } = 0.5;
    public bool IsMuted { get; private set; }
    public List<double> Writes { get; } = [];

    public event EventHandler? Changed;

    public void SetLevel(double level)
    {
        if (!IsAvailable)
        {
            return;
        }
        Level = VolumeMath.Clamp(level);
        Writes.Add(Level);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetMuted(bool muted)
    {
        if (IsAvailable)
        {
            IsMuted = muted;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleMute() => SetMuted(!IsMuted);

    public void ExternalChange(double level, bool muted)
    {
        Level = level;
        IsMuted = muted;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
