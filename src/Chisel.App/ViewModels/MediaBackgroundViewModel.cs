using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.App.Theming;
using Chisel.Core.Media;
using Chisel.Core.Theme;

namespace Chisel.App.ViewModels;

// The media widget's gradient: three colors from the current cover, ready to draw. When the cover changes it is decoded and
// reduced on a background thread; a newer track cancels the older work and only the latest result is applied. Palettes are
// cached by a hash of the cover bytes. With no art, a failed decode or no session the colors come from the theme's card and
// accent. Colors are always safe for the current theme mode (see AlbumGradient), so a theme change only re-adjusts them.
public sealed class MediaBackgroundViewModel : ViewModelBase
{
    private readonly MediaViewModel _media;
    private readonly MediaSettingsViewModel _settings;
    private readonly IArtSampler _sampler;
    private readonly PaletteCache _cache;
    private CancellationTokenSource _cancel = new();
    private int _generation;
    private Rgb[]? _raw;
    private ThemeDefinition _mode;
    private Rgb _accent;

    public MediaBackgroundViewModel(MediaViewModel media, MediaSettingsViewModel settings, ThemeService? theme, IArtSampler sampler, PaletteCache? cache = null)
    {
        _media = media;
        _settings = settings;
        _sampler = sampler;
        _cache = cache ?? new PaletteCache(6);
        _mode = theme?.Mode ?? ThemeModes.Default;
        _accent = theme?.Tokens.Fill ?? ThemeModes.Accent(_mode, ThemeModes.ResolveAccent(null)).Fill;
        Colors = AlbumGradient.Fallback(_mode, _accent);
        media.PropertyChanged += OnMediaChanged;
        settings.PropertyChanged += OnSettingsChanged;
        if (theme is not null)
        {
            theme.Changed += () => Recolor(theme.Mode, theme.Tokens.Fill);
        }
        Reload();
    }

    // The three stops, already adjusted for the theme mode.
    public IReadOnlyList<Rgb> Colors { get; private set; }

    // Goes up each time Colors is replaced (the view cross-fades on it).
    public int Version { get; private set; }

    public ThemeDefinition Mode => _mode;

    public bool IsOn => _settings.AlbumGradient;

    // Slow drift: only with the gradient on, "Animate background" on, and a playing session. The view adds visibility.
    public bool ShouldAnimate => IsOn && _settings.AnimateGradient && _media.HasSession && _media.IsPlaying;

    // Completes when the latest cover has been handled (for tests and captures).
    public Task Settled { get; private set; } = Task.CompletedTask;

    // Milliseconds from the track change to the palette being applied, for the last cover that was decoded.
    public double LastPaletteMilliseconds { get; private set; }

    public void Recolor(ThemeDefinition mode, Rgb accent)
    {
        _mode = mode;
        _accent = accent;
        OnPropertyChanged(nameof(Mode));
        Apply();
    }

    private void OnMediaChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MediaViewModel.ArtBytes):
            case nameof(MediaViewModel.HasSession):
                Reload();
                OnPropertyChanged(nameof(ShouldAnimate));
                break;
            case nameof(MediaViewModel.IsPlaying):
                OnPropertyChanged(nameof(ShouldAnimate));
                break;
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaSettingsViewModel.AlbumGradient))
        {
            OnPropertyChanged(nameof(IsOn));
            OnPropertyChanged(nameof(ShouldAnimate));
            Reload();
        }
        else if (e.PropertyName == nameof(MediaSettingsViewModel.AnimateGradient))
        {
            OnPropertyChanged(nameof(ShouldAnimate));
        }
    }

    private void Reload()
    {
        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        var generation = ++_generation;
        var art = IsOn && _media.HasSession ? _media.ArtBytes : null;
        if (art is null)
        {
            Settled = Task.CompletedTask;
            _raw = null;
            if (IsOn)
            {
                Apply();
            }
            return;
        }
        Settled = Process(art, generation, _cancel.Token, Stopwatch.GetTimestamp());
    }

    private async Task Process(byte[] art, int generation, CancellationToken cancel, long started)
    {
        Rgb[]? palette;
        try
        {
            palette = await Task.Run(() => Extract(art, cancel), cancel);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (generation != _generation)
        {
            return;
        }
        _raw = palette;
        LastPaletteMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Apply();
    }

    private Rgb[]? Extract(byte[] art, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var key = Convert.ToHexString(SHA256.HashData(art));
        if (_cache.Get(key) is { } hit)
        {
            return hit;
        }
        if (_sampler.Sample(art, cancel) is not { } pixels)
        {
            return null;
        }
        cancel.ThrowIfCancellationRequested();
        var palette = AlbumPalette.Extract(pixels.Rgba, pixels.Width, pixels.Height);
        if (palette.Count == 0)
        {
            return null;
        }
        var result = new Rgb[palette.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = palette[i];
        }
        _cache.Add(key, result);
        return result;
    }

    private void Apply()
    {
        var colors = _raw is null ? AlbumGradient.Fallback(_mode, _accent) : AlbumGradient.Adjust(_raw, _mode, _accent);
        if (colors.SequenceEqual(Colors))
        {
            return;
        }
        Colors = colors;
        Version++;
        OnPropertyChanged(nameof(Colors));
        OnPropertyChanged(nameof(Version));
    }
}
