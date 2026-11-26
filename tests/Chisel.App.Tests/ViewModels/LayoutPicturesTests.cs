using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;

namespace Chisel.App.Tests.ViewModels;

// The picture frames' lists are part of the layout's working copy: edit mode changes them, Done saves and sweeps,
// Cancel puts everything back.
public class LayoutPicturesTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; } = TestLayouts.Classic;
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    private readonly PictureServicesFixture _pictures = new();
    private readonly FakeLayoutRepository _repository = new() { Saved = WidgetRegistry.DefaultLayout };

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository, _pictures.Catalog);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    private static string AddFrame(LayoutViewModel layout)
    {
        layout.AddWidgetFromTray(WidgetRegistry.PictureFrame);
        return layout.Layout.WidgetIds.Single(id => id.StartsWith(WidgetRegistry.PictureFrame));
    }

    [Fact]
    public void ThePictureFrame_IsAMultiInstanceWidget_AlwaysOfferedInTheTray()
    {
        var layout = Editing();

        Assert.True(WidgetRegistry.Get(WidgetRegistry.PictureFrame)!.AllowMultiple);
        Assert.False(WidgetRegistry.Get(WidgetRegistry.PictureFrame)!.DefaultShowHeader);
        Assert.Contains(layout.TrayWidgets, w => w.Id == WidgetRegistry.PictureFrame);
        Assert.DoesNotContain(WidgetRegistry.DefaultLayout.WidgetIds, id => id.StartsWith(WidgetRegistry.PictureFrame));
    }

    [Fact]
    public void Pictures_AreLoadedWithTheLayout()
    {
        var asset = _pictures.Add(5);
        _pictures.Repository.SaveFrames(PictureFrameSet.Empty.AddPictures("pictureframe:k1", new[] { asset.Id }));

        var layout = new LayoutViewModel(_repository, _pictures.Catalog);

        Assert.Equal(new[] { asset.Id }, layout.Pictures.Get("pictureframe:k1").Pictures.Select(p => p.AssetId));
    }

    [Fact]
    public void InEditMode_ChangesAreOnlyTheWorkingCopy_AndDoneSavesThem()
    {
        var asset = _pictures.Add(5);
        var layout = Editing();
        var frame = AddFrame(layout);

        layout.UpdatePictures(p => p.AddPictures(frame, new[] { asset.Id }));

        Assert.Single(layout.Pictures.Get(frame).Pictures);
        Assert.Equal(0, _pictures.Repository.FrameSaves);
        layout.DoneCommand.Execute(null);
        Assert.Equal(new[] { asset.Id }, _pictures.Repository.Frames.Get(frame).Pictures.Select(p => p.AssetId));
        Assert.Single(_pictures.Repository.Stored);
    }

    [Fact]
    public void Cancel_RestoresTheListsAndTheFrames_AndDeletesNothing()
    {
        var asset = _pictures.Add(5);
        var layout = Editing();
        var frame = AddFrame(layout);
        layout.UpdatePictures(p => p.AddPictures(frame, new[] { asset.Id }));
        layout.DoneCommand.Execute(null);

        layout.EnterEditCommand.Execute(null);
        layout.UpdatePictures(p => p.RemovePicture(frame, 0));
        layout.RemoveWidget(frame);
        layout.CancelCommand.Execute(null);

        Assert.Contains(frame, layout.Layout.WidgetIds);
        Assert.Single(layout.Pictures.Get(frame).Pictures);
        Assert.Single(_pictures.Repository.Stored);
        Assert.True(_pictures.Store.Exists(asset.StoredName));
    }

    [Fact]
    public void Done_SweepsPicturesOfADeletedFrame_ButNotBefore()
    {
        var asset = _pictures.Add(5);
        var layout = Editing();
        var frame = AddFrame(layout);
        layout.UpdatePictures(p => p.AddPictures(frame, new[] { asset.Id }));
        layout.DoneCommand.Execute(null);

        layout.EnterEditCommand.Execute(null);
        layout.RemoveWidget(frame);
        Assert.True(_pictures.Store.Exists(asset.StoredName));
        layout.DoneCommand.Execute(null);

        Assert.Empty(_pictures.Repository.Stored);
        Assert.False(_pictures.Store.Exists(asset.StoredName));
        Assert.False(layout.Pictures.Has(frame));
    }

    [Fact]
    public void Reset_ThenDone_RemovesThePictureFramesAndTheirPictures()
    {
        var asset = _pictures.Add(5);
        var layout = Editing();
        var frame = AddFrame(layout);
        layout.UpdatePictures(p => p.AddPictures(frame, new[] { asset.Id }));
        layout.ResetCommand.Execute(null);
        layout.DoneCommand.Execute(null);

        Assert.Empty(_pictures.Repository.Stored);
        Assert.DoesNotContain(layout.Layout.WidgetIds, id => id.StartsWith(WidgetRegistry.PictureFrame));
    }

    [Fact]
    public void OutsideEditMode_AChangeIsSavedAtOnce()
    {
        var asset = _pictures.Add(5);
        var layout = Editing();
        var frame = AddFrame(layout);
        layout.UpdatePictures(p => p.AddPictures(frame, new[] { asset.Id }));
        layout.DoneCommand.Execute(null);
        var saves = _pictures.Repository.FrameSaves;

        layout.UpdatePictures(p => p.SetOptions(frame, p.Get(frame).Options with { Mode = SlideshowMode.Cycle }));

        Assert.Equal(saves + 1, _pictures.Repository.FrameSaves);
        Assert.Equal(SlideshowMode.Cycle, _pictures.Repository.Frames.Get(frame).Options.Mode);
    }

    [Fact]
    public void ADropOutsideEditMode_StartsNothing()
    {
        var layout = new LayoutViewModel(_repository, _pictures.Catalog);

        Assert.False(layout.BeginPictureDrag());
        Assert.False(layout.IsDraggingWidget);
    }

    [Fact]
    public void ADrop_IntoAGap_AddsNewFramesInOrder_AndIntoANewColumn()
    {
        var layout = Editing();

        Assert.True(layout.BeginPictureDrag());
        var placement = layout.DropPictures(PictureDropKind.Gap, 1, 1, frameCount: 3)!;

        Assert.True(placement.CreatedFrames);
        Assert.Equal(3, placement.FrameIds.Count);
        Assert.Equal(placement.FrameIds, layout.Layout.Columns[1].Widgets.Skip(1).Take(3).Select(w => w.WidgetId));
        Assert.False(layout.IsDraggingWidget);

        layout.BeginPictureDrag();
        var other = layout.DropPictures(PictureDropKind.NewColumn, 0, 0, frameCount: 2)!;
        Assert.Equal(other.FrameIds, layout.Layout.Columns[0].Widgets.Select(w => w.WidgetId));
    }

    [Fact]
    public void ADrop_OnAPictureFramesCenter_GoesIntoThatFrame_WithoutANewOne()
    {
        var layout = Editing();
        var frame = AddFrame(layout);
        var (column, index) = layout.Layout.Find(frame)!.Value;
        var before = layout.Layout.WidgetIds.Count();

        layout.BeginPictureDrag();
        var placement = layout.DropPictures(PictureDropKind.Card, column, index, frameCount: 4)!;

        Assert.False(placement.CreatedFrames);
        Assert.Equal(new[] { frame }, placement.FrameIds);
        Assert.Equal(before, layout.Layout.WidgetIds.Count());
    }

    [Fact]
    public void ADrop_OnAnotherWidgetsCenter_MakesANewFrameAsATabThere()
    {
        var layout = Editing();
        var (column, index) = layout.Layout.Find("notes")!.Value;

        layout.BeginPictureDrag();
        var placement = layout.DropPictures(PictureDropKind.Card, column, index, frameCount: 1, cardWidth: 600)!;

        var card = layout.Layout.Columns[column].Widgets.Single(w => w.Contains("notes"));
        Assert.True(placement.CreatedFrames);
        Assert.True(card.IsGroup);
        Assert.Equal(new[] { "notes", placement.FrameIds[0] }, card.MemberIds);
        Assert.Equal("notes", card.ActiveId);
    }

    [Fact]
    public void ADrop_OnAGroupWhoseActiveTabIsAPictureFrame_GoesIntoThatFrame()
    {
        var layout = Editing();
        var frame = AddFrame(layout);
        layout.BeginWidgetDrag("notes");
        Assert.True(layout.DropOnWidget(frame));
        layout.SelectTab(frame);
        var (column, index) = layout.Layout.Find(frame)!.Value;

        layout.BeginPictureDrag();
        var placement = layout.DropPictures(PictureDropKind.Card, column, index, frameCount: 1)!;

        Assert.False(placement.CreatedFrames);
        Assert.Equal(new[] { frame }, placement.FrameIds);
    }

    [Fact]
    public void CancellingTheDrag_AddsNothing()
    {
        var layout = Editing();
        var before = layout.Layout.Describe();

        layout.BeginPictureDrag();
        layout.CancelWidgetDrag();

        Assert.Equal(before, layout.Layout.Describe());
        Assert.False(layout.IsDraggingWidget);
    }
}
