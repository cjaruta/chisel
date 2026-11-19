using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Media;
using Chisel.Core.Settings;
using Chisel.Core.Theme;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class MediaBackgroundViewModelTests
{
    // Art is a one-byte array whose value picks a solid color. Samples can be held back with a gate.
    private sealed class FakeSampler : IArtSampler
    {
        public int Calls;
        public bool Fail;
        public Dictionary<byte, ManualResetEventSlim> Gates { get; } = [];
        public Dictionary<byte, CancellationToken> Tokens { get; } = [];

        public ArtPixels? Sample(byte[] art, CancellationToken cancel)
        {
            Interlocked.Increment(ref Calls);
            lock (Tokens)
            {
                Tokens[art[0]] = cancel;
            }
            if (Gates.TryGetValue(art[0], out var gate))
            {
                gate.Wait(TimeSpan.FromSeconds(5));
            }
            return Fail ? null : Solid(art[0]);
        }
    }

    private static ArtPixels Solid(byte id)
    {
        var color = Colors[id];
        var data = new byte[32 * 32 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            (data[i], data[i + 1], data[i + 2], data[i + 3]) = (color.R, color.G, color.B, 255);
        }
        return new ArtPixels(data, 32, 32);
    }

    private static readonly Rgb[] Colors = [new(200, 40, 40), new(40, 80, 220), new(40, 180, 90)];

    private static IReadOnlyList<Rgb> Expected(byte id) =>
        AlbumGradient.Adjust(AlbumPalette.Extract(Solid(id).Rgba, 32, 32), ThemeModes.Default, ThemeModes.Accent(ThemeModes.Default, ThemeModes.ResolveAccent(null)).Fill);

    private static IReadOnlyList<Rgb> ExpectedFallback() =>
        AlbumGradient.Fallback(ThemeModes.Default, ThemeModes.Accent(ThemeModes.Default, ThemeModes.ResolveAccent(null)).Fill);

    private static MediaSessionInfo Track(byte? art, MediaPlaybackState state = MediaPlaybackState.Playing) =>
        FakeMediaService.Sample(state) with { AlbumArt = art is { } id ? [id] : null };

    private static (MediaBackgroundViewModel Vm, FakeMediaService Service, FakeSampler Sampler, MediaSettingsViewModel Settings) Make(MediaSessionInfo? first = null)
    {
        var service = new FakeMediaService { Current = first };
        var media = new MediaViewModel(service, new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero))) { IsActive = true };
        var settings = new MediaSettingsViewModel(null);
        var sampler = new FakeSampler();
        return (new MediaBackgroundViewModel(media, settings, null, sampler), service, sampler, settings);
    }

    [Fact]
    public async Task ATrackWithArt_GetsItsPalette()
    {
        var (vm, service, _, _) = Make();
        service.Current = Track(1);
        await vm.Settled;
        Assert.Equal(Expected(1), vm.Colors);
    }

    [Fact]
    public async Task FastSkipping_OnlyTheLatestTrackIsApplied()
    {
        var (vm, service, sampler, _) = Make();
        sampler.Gates[0] = new ManualResetEventSlim(false);
        service.Current = Track(0);
        var first = vm.Settled;
        // Wait until the first decode is really under way (and held at its gate) before the next track arrives.
        Assert.True(SpinWait.SpinUntil(() => { lock (sampler.Tokens) { return sampler.Tokens.ContainsKey(0); } }, 2000));
        service.Current = Track(1);
        await vm.Settled;
        Assert.True(sampler.Tokens[0].IsCancellationRequested);
        var version = vm.Version;

        sampler.Gates[0].Set();
        await first;

        Assert.Equal(Expected(1), vm.Colors);
        Assert.Equal(version, vm.Version);
    }

    [Fact]
    public async Task TheSameCoverAgain_IsServedFromTheCache()
    {
        var (vm, service, sampler, _) = Make();
        service.Current = Track(1);
        await vm.Settled;
        service.Current = Track(2);
        await vm.Settled;
        Assert.Equal(2, sampler.Calls);

        service.Current = Track(1);
        await vm.Settled;

        Assert.Equal(2, sampler.Calls);
        Assert.Equal(Expected(1), vm.Colors);
    }

    [Fact]
    public async Task NoArt_NoSession_AndFailedDecodes_UseTheThemeFallback()
    {
        var (vm, service, sampler, _) = Make();
        Assert.Equal(ExpectedFallback(), vm.Colors); // no session yet

        service.Current = Track(null);
        await vm.Settled;
        Assert.Equal(ExpectedFallback(), vm.Colors);

        service.Current = Track(1);
        await vm.Settled;
        Assert.NotEqual(ExpectedFallback(), vm.Colors);

        sampler.Fail = true;
        service.Current = Track(2);
        await vm.Settled;
        Assert.Equal(ExpectedFallback(), vm.Colors);

        service.Current = null;
        await vm.Settled;
        Assert.Equal(ExpectedFallback(), vm.Colors);
    }

    [Fact]
    public async Task WithTheSettingOff_NothingIsShownOrDecoded()
    {
        var (vm, service, sampler, settings) = Make();
        settings.AlbumGradient = false;
        service.Current = Track(1);
        await vm.Settled;

        Assert.False(vm.IsOn);
        Assert.False(vm.ShouldAnimate);
        Assert.Equal(0, sampler.Calls);

        settings.AlbumGradient = true;
        await vm.Settled;
        Assert.True(vm.IsOn);
        Assert.Equal(Expected(1), vm.Colors);
    }

    [Fact]
    public void Motion_RunsOnlyWhilePlaying_AndWhenAnimateIsOn()
    {
        var (vm, service, _, settings) = Make(Track(1));
        Assert.True(vm.ShouldAnimate);

        service.Current = Track(1, MediaPlaybackState.Paused);
        Assert.False(vm.ShouldAnimate);

        service.Current = Track(1);
        Assert.True(vm.ShouldAnimate);

        settings.AnimateGradient = false;
        Assert.False(vm.ShouldAnimate);

        settings.AnimateGradient = true;
        service.Current = null;
        Assert.False(vm.ShouldAnimate);
    }

    [Fact]
    public async Task ChangingTheTheme_RecolorsTheSameCoverWithoutDecodingAgain()
    {
        var (vm, service, sampler, _) = Make();
        service.Current = Track(1);
        await vm.Settled;
        var dark = vm.Colors;

        vm.Recolor(ThemeModes.ById("light"), ThemeModes.Accent(ThemeModes.ById("light"), ThemeModes.ResolveAccent(null)).Fill);

        Assert.Equal(1, sampler.Calls);
        Assert.NotEqual(dark, vm.Colors);
        Assert.True(vm.Colors.All(c => AlbumPalette.Lightness(c) >= 0.7));
    }
}
