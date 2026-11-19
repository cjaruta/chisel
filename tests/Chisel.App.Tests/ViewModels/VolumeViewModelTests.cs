using System;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Media;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class VolumeViewModelTests
{
    private static (VolumeViewModel Vm, FakeVolumeService Service, FakeTimeProvider Time) Make(bool available = true)
    {
        var service = new FakeVolumeService { IsAvailable = available };
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
        return (new VolumeViewModel(service, time), service, time);
    }

    [Fact]
    public void StartsFromTheServiceAndFollowsChangesMadeElsewhere()
    {
        var (vm, service, _) = Make();
        Assert.Equal(0.5, vm.Level);
        Assert.Equal(VolumeIcon.Medium, vm.Icon);

        service.ExternalChange(0.9, false);

        Assert.Equal(0.9, vm.Level);
        Assert.Equal(VolumeIcon.High, vm.Icon);
        Assert.Equal("90%", vm.Tip);
    }

    [Fact]
    public void Mute_ThenUnmute_RestoresTheLevel()
    {
        var (vm, service, _) = Make();

        vm.ToggleMute();
        Assert.True(vm.IsMuted);
        Assert.Equal(VolumeIcon.Muted, vm.Icon);
        Assert.Equal(0.5, vm.Level);

        vm.ToggleMute();
        Assert.False(service.IsMuted);
        Assert.Equal(0.5, service.Level);
    }

    [Fact]
    public void UnmutingFromZero_GoesBackToTheLastAudibleLevel()
    {
        var (vm, service, time) = Make();
        vm.DragTo(0.7);
        vm.CompleteDrag();
        time.Advance(TimeSpan.FromSeconds(1));
        vm.DragTo(0);
        vm.CompleteDrag();
        Assert.Equal(0, service.Level);

        vm.ToggleMute();

        Assert.Equal(0.7, service.Level);
        Assert.False(service.IsMuted);
    }

    [Fact]
    public void Dragging_SetsTheLevelLive_AndUnmutes()
    {
        var (vm, service, _) = Make();
        service.SetMuted(true);

        vm.DragTo(0.25);

        Assert.Equal(0.25, service.Level);
        Assert.False(service.IsMuted);
        Assert.Equal(0.25, vm.Level);
    }

    [Fact]
    public void Dragging_WritesAtMostAboutThirtyTimesASecond_AndTheEndAlwaysLands()
    {
        var (vm, service, time) = Make();

        for (var i = 1; i <= 6; i++)
        {
            vm.DragTo(i / 20d);
            time.Advance(TimeSpan.FromMilliseconds(5));
        }
        Assert.Single(service.Writes);

        vm.CompleteDrag();
        Assert.Equal(0.3, service.Writes[^1]);
        Assert.Equal(2, service.Writes.Count);

        time.Advance(TimeSpan.FromMilliseconds(40));
        vm.DragTo(0.8);
        Assert.Equal(0.8, service.Writes[^1]);
    }

    [Fact]
    public void TheEchoOfOurOwnWrite_DoesNotMoveTheThumb()
    {
        var (vm, service, time) = Make();
        vm.DragTo(0.3);
        time.Advance(TimeSpan.FromMilliseconds(5));
        vm.DragTo(0.6);   // throttled; the service still says 0.3

        service.RaiseChanged();

        Assert.Equal(0.6, vm.Level);

        vm.CompleteDrag();
        var changes = 0;
        vm.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(VolumeViewModel.Level) ? 1 : 0;
        service.RaiseChanged();   // echo of the final write
        Assert.Equal(0, changes);
    }

    [Fact]
    public void WithNoOutputDevice_TheRowIsDisabled_AndNothingThrows()
    {
        var (vm, service, _) = Make(available: false);

        vm.DragTo(0.4);
        vm.CompleteDrag();
        vm.ToggleMute();

        Assert.False(vm.IsAvailable);
        Assert.Empty(service.Writes);
        Assert.False(service.IsMuted);
    }

    [Fact]
    public void ADeviceAppearingLater_EnablesTheRow()
    {
        var (vm, service, _) = Make(available: false);

        service.IsAvailable = true;
        service.ExternalChange(0.4, false);

        Assert.True(vm.IsAvailable);
        Assert.Equal(0.4, vm.Level);
    }

    private static (VolumeViewModel Vm, FakeVolumeService Service, FakeTimeProvider Time) MakeScrolling(bool available = true)
    {
        var made = Make(available);
        made.Vm.ScrollEnabled = true;
        return made;
    }

    [Fact]
    public void Scroll_StepsTwoPercentPerNotch_UpAndDown()
    {
        var (vm, service, time) = MakeScrolling();

        Assert.True(vm.ScrollBy(1));
        Assert.Equal(0.52, vm.Level, 6);
        Assert.Equal(0.52, service.Level, 6);

        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(vm.ScrollBy(-3));
        Assert.Equal(0.46, vm.Level, 6);
        Assert.Equal(0.46, service.Level, 6);
    }

    [Fact]
    public void Scroll_AddsUpFractionalWheelDeltas()
    {
        var (vm, service, time) = MakeScrolling();

        for (var i = 0; i < 8; i++)
        {
            Assert.True(vm.ScrollBy(0.125));
            time.Advance(TimeSpan.FromMilliseconds(50));
        }

        Assert.Equal(0.52, vm.Level, 6);
        Assert.Equal(0.52, service.Level, 6);
    }

    [Theory]
    [InlineData(0.99, 5, 1.0)]
    [InlineData(0.01, -5, 0.0)]
    public void Scroll_StopsAtTheEnds(double start, double notches, double expected)
    {
        var (vm, service, _) = MakeScrolling();
        service.ExternalChange(start, false);

        vm.ScrollBy(notches);

        Assert.Equal(expected, vm.Level, 6);
        Assert.Equal(expected, service.Level, 6);
    }

    [Fact]
    public void Scroll_HeldBackWrites_LandOnCompleteDrag()
    {
        var (vm, service, _) = MakeScrolling();

        vm.ScrollBy(1);
        vm.ScrollBy(1);
        Assert.Equal(0.52, service.Level, 6);

        vm.CompleteDrag();

        Assert.Equal(0.54, service.Level, 6);
    }

    [Fact]
    public void Scroll_ReportsTheNewLevelForTheFeedbackLabel()
    {
        var (vm, _, _) = MakeScrolling();
        double? shown = null;
        vm.ScrollFeedback += (_, level) => shown = level;

        vm.ScrollBy(2);

        Assert.Equal(0.54, shown!.Value, 6);
    }

    [Fact]
    public void Scroll_IsIgnored_WhenTheSettingIsOff_WhenEditing_OrWithoutADevice()
    {
        var (off, offService, _) = Make();
        Assert.False(off.ScrollBy(1));
        Assert.Empty(offService.Writes);

        var (editing, editingService, _) = MakeScrolling();
        editing.ScrollBlocked = true;
        Assert.False(editing.ScrollBy(1));
        Assert.Empty(editingService.Writes);

        var (none, noneService, _) = MakeScrolling(available: false);
        Assert.False(none.ScrollBy(1));
        Assert.Empty(noneService.Writes);
    }
}
