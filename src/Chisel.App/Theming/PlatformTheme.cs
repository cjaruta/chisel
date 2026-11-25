using System;
using Avalonia;
using Avalonia.Platform;

namespace Chisel.App.Theming;

// The Windows light/dark setting, and a signal when it changes.
public interface IPlatformTheme
{
    bool IsLight { get; }
    event Action? Changed;
}

// Reads Avalonia's platform settings. Without them (headless, very old systems) it reports dark and never changes.
public sealed class AvaloniaPlatformTheme : IPlatformTheme
{
    private readonly IPlatformSettings? _settings;

    public AvaloniaPlatformTheme(IPlatformSettings? settings)
    {
        _settings = settings;
        if (_settings is not null)
        {
            _settings.ColorValuesChanged += (_, _) => Changed?.Invoke();
        }
    }

    public bool IsLight => _settings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Light;

    public event Action? Changed;
}
