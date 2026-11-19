using System;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Media;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class MediaViewModelTests
{
    private static (MediaViewModel Vm, FakeMediaService Service, FakeTimeProvider Time) Make(MediaSessionInfo? info = null)
    {
        var service = new FakeMediaService { Current = info };
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var vm = new MediaViewModel(service, time) { IsActive = true };
        return (vm, service, time);
    }

    [Fact]
    public void NoSession_ShowsNothingPlaying_AndDisablesEverything()
    {
        var (vm, _, _) = Make();

        Assert.False(vm.HasSession);
        Assert.False(vm.CanPlayPause);
        Assert.False(vm.CanPrevious);
        Assert.False(vm.CanNext);
        Assert.False(vm.PlayPauseCommand.CanExecute(null));
    }

    [Fact]
    public void ASession_ShowsItsDetails()
    {
        var (vm, _, _) = Make(FakeMediaService.Sample());

        Assert.True(vm.HasSession);
        Assert.Equal("Sample Song", vm.Title);
        Assert.Equal("Test Artist", vm.Artist);
        Assert.Equal("Sample Album", vm.Album);
        Assert.Equal("Spotify", vm.Source);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void MissingMetadata_FallsBackToQuietText()
    {
        var (vm, _, _) = Make(new MediaSessionInfo { AlbumArtist = "Band", Controls = MediaControls.Play });

        Assert.Equal("Unknown title", vm.Title);
        Assert.Equal("Band", vm.Artist);
        Assert.Equal("", vm.Album);
        Assert.Null(vm.ArtBytes);
        Assert.False(vm.HasTimeline);
    }

    [Fact]
    public void TrackChange_UpdatesTheSessionAndRaisesChanges()
    {
        var (vm, service, _) = Make(FakeMediaService.Sample());
        var changed = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.Current = FakeMediaService.Sample() with { Title = "Next Song", AlbumArt = [1, 2, 3] };

        Assert.Equal("Next Song", vm.Title);
        Assert.Equal(new byte[] { 1, 2, 3 }, vm.ArtBytes);
        Assert.Contains(nameof(MediaViewModel.Title), changed);
        Assert.Contains(nameof(MediaViewModel.ArtBytes), changed);
    }

    [Fact]
    public void SessionEnding_ReturnsToNothingPlaying()
    {
        var (vm, service, _) = Make(FakeMediaService.Sample());

        service.Current = null;

        Assert.False(vm.HasSession);
    }

    [Fact]
    public void ButtonsFollowTheControlsTheSessionReports()
    {
        var (vm, _, _) = Make(FakeMediaService.Sample() with { Controls = MediaControls.Pause | MediaControls.Next });

        Assert.True(vm.CanPlayPause);
        Assert.True(vm.CanNext);
        Assert.False(vm.CanPrevious);
        Assert.False(vm.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void PlayPause_FollowsTheState()
    {
        var (vm, service, _) = Make(FakeMediaService.Sample(MediaPlaybackState.Paused) with { Controls = MediaControls.Play });
        Assert.False(vm.IsPlaying);
        Assert.True(vm.CanPlayPause);

        service.Current = FakeMediaService.Sample(MediaPlaybackState.Playing) with { Controls = MediaControls.Play };

        Assert.True(vm.IsPlaying);
        Assert.False(vm.CanPlayPause);   // playing, and the session does not offer pause
    }

    [Fact]
    public async Task Commands_CallTheService()
    {
        var (vm, service, _) = Make(FakeMediaService.Sample());

        await vm.PlayPauseCommand.ExecuteAsync(null);   // playing, so this pauses
        await vm.NextCommand.ExecuteAsync(null);
        await vm.PreviousCommand.ExecuteAsync(null);
        service.Current = FakeMediaService.Sample(MediaPlaybackState.Paused);
        await vm.PlayPauseCommand.ExecuteAsync(null);   // paused, so this plays

        Assert.Equal(new[] { "Pause", "Next", "Previous", "Play" }, service.Calls);
    }

    [Fact]
    public async Task ARefusedCommand_DoesNotThrow()
    {
        var (vm, service, _) = Make(FakeMediaService.Sample());
        service.Throws = true;

        await vm.NextCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Next" }, service.Calls);
    }

    [Fact]
    public void Progress_AdvancesWhilePlaying_AndStopsAtTheEnd()
    {
        var (vm, _, time) = Make(FakeMediaService.Sample());   // 1:23 of 3:35
        Assert.Equal("1:23", vm.ElapsedText);
        Assert.Equal("3:35", vm.DurationText);

        time.Advance(TimeSpan.FromSeconds(10));
        vm.Tick();
        Assert.Equal("1:33", vm.ElapsedText);
        Assert.Equal(93d / 215, vm.ProgressFraction, 3);

        time.Advance(TimeSpan.FromMinutes(10));
        vm.Tick();
        Assert.Equal("3:35", vm.ElapsedText);
        Assert.Equal(1, vm.ProgressFraction);
    }

    [Fact]
    public void Progress_HoldsStillWhilePaused()
    {
        var (vm, _, time) = Make(FakeMediaService.Sample(MediaPlaybackState.Paused));

        time.Advance(TimeSpan.FromSeconds(30));
        vm.Tick();

        Assert.Equal("1:23", vm.ElapsedText);
    }

    [Fact]
    public void WhenInactive_ItIgnoresTheServiceUntilActivated()
    {
        var service = new FakeMediaService { Current = FakeMediaService.Sample() };
        var vm = new MediaViewModel(service, TimeProvider.System);

        Assert.False(vm.HasSession);
        vm.IsActive = true;
        Assert.True(vm.HasSession);
        vm.IsActive = false;
        Assert.False(vm.HasSession);   // nothing is held while the widget is off the dashboard
        service.Current = FakeMediaService.Sample() with { Title = "Later" };
        Assert.False(vm.HasSession);
        vm.IsActive = true;
        Assert.Equal("Later", vm.Title);
    }
}
