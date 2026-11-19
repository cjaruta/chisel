using Avalonia.Input;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Pictures;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// A frame's slideshow: what it shows, when it advances (on a fake clock), clicks and keys, and when it stays still.
public class PictureFrameViewModelTests
{
    private const string Frame = "pictureframe:k1";

    private readonly PictureServicesFixture _pictures = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 9, 30, 0, TimeSpan.Zero));
    private readonly LayoutViewModel _layout;
    private readonly PictureFramesViewModel _frames;
    private readonly Guid[] _ids;

    public PictureFrameViewModelTests()
    {
        _layout = new LayoutViewModel(null, _pictures.Catalog);
        _layout.Apply(_layout.Layout.AddWidget(Frame, 0, 0, 1));
        _ids = Enumerable.Range(1, 4).Select(i => _pictures.Add(i).Id).ToArray();
        _layout.UpdatePictures(p => p.AddPictures(Frame, _ids));
        _frames = new PictureFramesViewModel(_layout, _time, shuffleSeed: 42, catalog: _pictures.Catalog);
    }

    private PictureFrameViewModel Show(FrameOptions? options = null)
    {
        if (options is not null)
        {
            _layout.UpdatePictures(p => p.SetOptions(Frame, options));
        }
        var frame = _frames.For(Frame);
        frame.IsVisible = true;
        return frame;
    }

    private void Wait(int seconds)
    {
        _time.Advance(TimeSpan.FromSeconds(seconds));
        _frames.Tick();
    }

    private static FrameOptions Cycle(int seconds = 30) => new(Mode: SlideshowMode.Cycle, IntervalSeconds: seconds);

    [Fact]
    public void AFrame_ShowsItsFirstPicture_AndKnowsHowManyThereAre()
    {
        var frame = Show();

        Assert.Equal(4, frame.Count);
        Assert.Equal(0, frame.CurrentIndex);
        Assert.Equal(_ids[0], frame.Current!.AssetId);
    }

    [Fact]
    public void AnEmptyFrame_HasNothingToShow_AndClicksDoNothing()
    {
        var frame = _frames.For("pictureframe:empty");

        Assert.Null(frame.Current);
        Assert.False(frame.HandleClick(300, 400, 0, 0));
        Assert.False(frame.HandleKey(Key.Right));
    }

    [Fact]
    public void ManualMode_NeverAdvancesByItself()
    {
        var frame = Show();

        Wait(3600);

        Assert.Equal(0, frame.CurrentIndex);
    }

    [Fact]
    public void CycleMode_AdvancesAfterTheInterval_AndWrapsAround()
    {
        var frame = Show(Cycle(30));

        Wait(0);
        Wait(29);
        Assert.Equal(0, frame.CurrentIndex);
        Wait(1);
        Assert.Equal(1, frame.CurrentIndex);
        Wait(30);
        Wait(30);
        Assert.Equal(3, frame.CurrentIndex);
        Wait(30);
        Assert.Equal(0, frame.CurrentIndex);
    }

    [Fact]
    public void AClick_OnTheRightHalfGoesForward_TheLeftHalfBack_WithWrapAround()
    {
        var frame = Show();

        Assert.True(frame.HandleClick(300, 400, 1, 1));
        Assert.Equal(1, frame.CurrentIndex);
        Assert.True(frame.HandleClick(100, 400, 0, 0));
        Assert.Equal(0, frame.CurrentIndex);
        Assert.True(frame.HandleClick(100, 400, 0, 0));
        Assert.Equal(3, frame.CurrentIndex);
        Assert.True(frame.HandleClick(300, 400, 0, 0));
        Assert.Equal(0, frame.CurrentIndex);
    }

    [Fact]
    public void ADrag_IsNotAClick()
    {
        var frame = Show();

        Assert.False(frame.HandleClick(300, 400, 12, 0));
        Assert.False(frame.HandleClick(300, 400, 3, 4.5));
        Assert.Equal(0, frame.CurrentIndex);
    }

    [Fact]
    public void WithOnePicture_ClicksAndKeysDoNothing()
    {
        _layout.UpdatePictures(p => p.RemovePicture(Frame, 3).RemovePicture(Frame, 2).RemovePicture(Frame, 1));
        var frame = Show();

        Assert.Equal(1, frame.Count);
        Assert.False(frame.HandleClick(300, 400, 0, 0));
        Assert.False(frame.HandleKey(Key.Right));
        Assert.Equal(0, frame.CurrentIndex);
    }

    [Fact]
    public void ArrowKeys_DoWhatClicksDo_AndOtherKeysAreLeftAlone()
    {
        var frame = Show();

        Assert.True(frame.HandleKey(Key.Right));
        Assert.Equal(1, frame.CurrentIndex);
        Assert.True(frame.HandleKey(Key.Left));
        Assert.True(frame.HandleKey(Key.Left));
        Assert.Equal(3, frame.CurrentIndex);
        Assert.False(frame.HandleKey(Key.A));
        Assert.False(frame.HandleKey(Key.Up));
    }

    [Fact]
    public void AClick_InCycleMode_RestartsTheTimer()
    {
        var frame = Show(Cycle(30));
        Wait(0);
        Wait(25);

        frame.HandleClick(300, 400, 0, 0);
        Assert.Equal(1, frame.CurrentIndex);
        Wait(25);
        Assert.Equal(1, frame.CurrentIndex);
        Wait(5);
        Assert.Equal(2, frame.CurrentIndex);
    }

    [Fact]
    public void ClicksAndKeys_WorkInCycleModeToo_ButNotInEditMode()
    {
        var frame = Show(Cycle());
        Assert.True(frame.HandleKey(Key.Right));

        _layout.EnterEditCommand.Execute(null);
        Assert.False(frame.HandleClick(300, 400, 0, 0));
        Assert.False(frame.HandleKey(Key.Right));
        Assert.Equal(1, frame.CurrentIndex);
    }

    [Fact]
    public void AHiddenFrame_DoesNotAdvance_AndDoesNotJumpWhenItComesBack()
    {
        var frame = Show(Cycle(30));
        Wait(0);

        frame.IsVisible = false;
        Wait(3600);
        Assert.Equal(0, frame.CurrentIndex);

        frame.IsVisible = true;
        Wait(1);
        Assert.Equal(0, frame.CurrentIndex);
        Wait(30);
        Assert.Equal(1, frame.CurrentIndex);
    }

    [Fact]
    public void AMinimizedWindow_AndEditMode_StopEveryTimer()
    {
        var frame = Show(Cycle(10));
        Wait(0);

        _frames.WindowMinimized = true;
        Wait(600);
        Assert.Equal(0, frame.CurrentIndex);
        Assert.False(frame.IsRunning);

        _frames.WindowMinimized = false;
        _layout.EnterEditCommand.Execute(null);
        Wait(600);
        Assert.Equal(0, frame.CurrentIndex);
        Assert.False(frame.IsRunning);

        _layout.CancelCommand.Execute(null);
        Assert.True(frame.IsRunning);
    }

    [Fact]
    public void ManualMode_SavesTheCurrentPicture_AfterAShortWhile_NotOnEveryClick()
    {
        var frame = Show();
        var saves = _pictures.Repository.FrameSaves;

        frame.HandleKey(Key.Right);
        frame.HandleKey(Key.Right);
        Wait(1);
        Assert.Equal(saves, _pictures.Repository.FrameSaves);
        Wait(2);

        Assert.Equal(saves + 1, _pictures.Repository.FrameSaves);
        Assert.Equal(2, _pictures.Repository.Frames.Get(Frame).Options.CurrentIndex);
    }

    [Fact]
    public void CycleMode_DoesNotSaveWhereItIs()
    {
        var frame = Show(Cycle(10));
        var saves = _pictures.Repository.FrameSaves;
        Wait(0);

        Wait(10);
        Wait(10);
        Wait(10);

        Assert.Equal(saves, _pictures.Repository.FrameSaves);
        Assert.Equal(3, frame.CurrentIndex);
    }

    [Fact]
    public void ASavedPosition_IsWhereTheFrameStartsAgain()
    {
        _layout.UpdatePictures(p => p.SetOptions(Frame, p.Get(Frame).Options with { CurrentIndex = 2 }));

        var frame = new PictureFramesViewModel(_layout, _time).For(Frame);

        Assert.Equal(2, frame.CurrentIndex);
    }

    [Fact]
    public void Shuffle_ShowsEveryPictureOnceBeforeRepeating_InTheSameOrderForTheSameSeed()
    {
        var frame = Show(new FrameOptions(Order: SlideshowOrder.Shuffle));
        var seen = new List<int> { frame.CurrentIndex };
        for (var i = 0; i < 3; i++)
        {
            frame.HandleKey(Key.Right);
            seen.Add(frame.CurrentIndex);
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, seen.OrderBy(i => i));
        var again = new PictureFramesViewModel(_layout, _time, shuffleSeed: 42).For(Frame);
        var second = new List<int> { again.CurrentIndex };
        for (var i = 0; i < 3; i++)
        {
            again.HandleKey(Key.Right);
            second.Add(again.CurrentIndex);
        }
        Assert.Equal(seen, second);
    }

    [Fact]
    public void RemovingThePictureShown_ShowsAnotherOne_AndTellsTheView()
    {
        var frame = Show();
        frame.HandleKey(Key.Right);
        var changes = 0;
        frame.CurrentChanged += () => changes++;

        _layout.UpdatePictures(p => p.RemovePicture(Frame, 1));

        Assert.Equal(3, frame.Count);
        Assert.Equal(_ids[2], frame.Current!.AssetId);
        Assert.True(changes > 0);
    }

    [Fact]
    public void AddingPictures_KeepsTheOneShown()
    {
        var frame = Show();
        frame.HandleKey(Key.Right);
        var extra = _pictures.Add(9).Id;

        _layout.UpdatePictures(p => p.AddPictures(Frame, new[] { extra }));

        Assert.Equal(5, frame.Count);
        Assert.Equal(_ids[1], frame.Current!.AssetId);
    }

    [Fact]
    public void TheIntervalIsReadFromTheFramesOptions_SoAChangeApplies()
    {
        var frame = Show(Cycle(60));
        Wait(0);
        Wait(30);
        _layout.UpdatePictures(p => p.SetOptions(Frame, p.Get(Frame).Options with { IntervalSeconds = 10 }));

        Wait(1);

        Assert.Equal(1, frame.CurrentIndex);
    }

    // ----- adjusting a picture (edit mode)

    private PictureFrameViewModel Adjusting()
    {
        var frame = Show();
        _layout.EnterEditCommand.Execute(null);
        Assert.True(frame.BeginAdjust());
        return frame;
    }

    [Fact]
    public void Adjusting_NeedsEditMode_AndAStillPicture()
    {
        var frame = Show();
        Assert.False(frame.BeginAdjust());

        var gif = PictureAsset.Create(new string('e', 64), "a.gif", ImageFormats.Gif, 10, 10, 10, null, DateTimeOffset.UnixEpoch);
        _pictures.Repository.AddAsset(gif);
        _layout.UpdatePictures(p => p.AddPictures("pictureframe:gif", new[] { gif.Id }));
        _layout.Apply(_layout.Layout.AddWidget("pictureframe:gif", 0, 0, 1));
        _layout.EnterEditCommand.Execute(null);
        Assert.False(_frames.For("pictureframe:gif").BeginAdjust());
        Assert.True(frame.BeginAdjust());
        Assert.True(frame.IsAdjusting);
    }

    [Fact]
    public void Adjusting_EndsWithDoneOrCancel_AndStopsClicksFromChangingPicture()
    {
        var frame = Adjusting();
        Assert.False(frame.HandleClick(300, 400, 0, 0));
        Assert.False(frame.HandleKey(Key.Right));

        _layout.CancelCommand.Execute(null);
        Assert.False(frame.IsAdjusting);

        _layout.EnterEditCommand.Execute(null);
        frame.BeginAdjust();
        _layout.DoneCommand.Execute(null);
        Assert.False(frame.IsAdjusting);
    }

    [Fact]
    public void Dragging_MovesTheFocalPoint_AndWheelZoomsBetweenOneAndEight()
    {
        // The picture is 1 wide and 10 tall, shown in a 100 x 100 view: it can only pan up and down.
        var frame = Adjusting();

        frame.AdjustPan(100, 100, 0, 5);
        Assert.Equal(0.5, frame.Current!.FocalX, 3);
        Assert.NotEqual(0.5, frame.Current!.FocalY);

        frame.AdjustZoom(3);
        Assert.True(frame.Current!.Zoom > 1.3);
        frame.AdjustZoom(-50);
        Assert.Equal(1, frame.Current!.Zoom, 3);
        frame.AdjustZoom(500);
        Assert.Equal(FramePicture.MaxZoom, frame.Current!.Zoom, 3);
    }

    [Fact]
    public void Reset_PutsTheCropBack_AndTheCropIsPerPicture()
    {
        var frame = Adjusting();
        frame.AdjustZoom(10);
        var zoomed = frame.Current!.Zoom;
        Assert.True(zoomed > 1);

        frame.Step(1);
        Assert.Equal(1, frame.Current!.Zoom);
        frame.Step(-1);
        Assert.Equal(zoomed, frame.Current!.Zoom, 6);

        frame.ResetCrop();
        Assert.Equal((0.5, 0.5, 1.0), (frame.Current!.FocalX, frame.Current.FocalY, frame.Current.Zoom));
    }

    [Fact]
    public void ACropMadeInEditMode_IsDroppedByCancel_AndKeptByDone()
    {
        var frame = Adjusting();
        frame.AdjustZoom(10);
        _layout.CancelCommand.Execute(null);
        Assert.Equal(1, frame.Current!.Zoom);

        _layout.EnterEditCommand.Execute(null);
        frame.BeginAdjust();
        frame.AdjustZoom(10);
        var zoom = frame.Current!.Zoom;
        _layout.DoneCommand.Execute(null);

        Assert.Equal(zoom, _pictures.Repository.Frames.Get(Frame).Pictures[0].Zoom, 6);
    }
}
