using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class PictureSettingsViewModelTests
{
    private sealed class FakeSettings : IUserSettingsRepository
    {
        public UserSettings Settings { get; set; } = new("Test User", new TimeOnly(7, 0), new TimeOnly(23, 0));
        public UserSettings? Get() => Settings;
        public void Save(UserSettings settings) => Settings = settings;
    }

    private readonly PictureServicesFixture _pictures = new();
    private readonly FakeSettings _settings = new();
    private readonly LayoutViewModel _layout;
    private readonly PictureAsset[] _assets;

    public PictureSettingsViewModelTests()
    {
        _layout = new LayoutViewModel(null, _pictures.Catalog);
        _assets = [_pictures.Add(1), _pictures.Add(2), _pictures.Add(3)];
    }

    private string AddFrame(params PictureAsset[] assets)
    {
        _layout.EnterEditCommand.Execute(null);
        _layout.AddWidgetFromTray(WidgetRegistry.PictureFrame);
        var id = _layout.Layout.WidgetIds.Last(i => i.StartsWith(WidgetRegistry.PictureFrame));
        _layout.UpdatePictures(p => p.AddPictures(id, assets.Select(a => a.Id)));
        _layout.DoneCommand.Execute(null);
        return id;
    }

    private PictureSettingsViewModel Settings() => new(_settings, _layout, _pictures.Catalog);

    [Fact]
    public void TheTwoGlobalOptions_StartAtTheirDefaults_AndSaveAtOnce()
    {
        var vm = Settings();
        Assert.True(vm.KeepOriginals);
        Assert.Equal(0, vm.MultiDropIndex);

        vm.KeepOriginals = false;
        vm.MultiDropIndex = 1;

        Assert.False(_settings.Settings.PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneFramePerPicture, _settings.Settings.PictureMultiDrop);
        Assert.False(Settings().KeepOriginals);
        Assert.Equal(1, Settings().MultiDropIndex);
    }

    [Fact]
    public void WithNoFrames_ThereIsAHintInsteadOfOptions()
    {
        var vm = Settings();

        Assert.False(vm.HasFrames);
        Assert.Null(vm.SelectedFrame);
        Assert.Empty(vm.Pictures);
    }

    [Fact]
    public void Frames_AreListedWithNumberedTitles_AndFollowTheLayout()
    {
        var vm = Settings();
        var first = AddFrame(_assets[0]);
        var second = AddFrame(_assets[1]);

        Assert.Equal(new[] { first, second }, vm.Frames.Select(f => f.Id));
        Assert.Equal(new[] { "Picture frame", "Picture frame 2" }, vm.Frames.Select(f => f.Title));
        Assert.Equal(first, vm.SelectedFrame!.Id);

        _layout.EnterEditCommand.Execute(null);
        _layout.RemoveWidget(first);
        _layout.DoneCommand.Execute(null);

        Assert.Equal(new[] { second }, vm.Frames.Select(f => f.Id));
        Assert.Equal(second, vm.SelectedFrame!.Id);
        Assert.Equal("Picture frame", vm.Frames[0].Title);
    }

    [Fact]
    public void TheSelectedFramesOptions_AreShown_AndEachChangeIsSaved()
    {
        var id = AddFrame(_assets[0], _assets[1]);
        var vm = Settings();

        Assert.Equal(0, vm.FitIndex);
        vm.FitIndex = 1;
        vm.BlurFill = true;
        vm.ModeIndex = 1;
        vm.OrderIndex = 1;
        vm.TransitionIndex = 1;

        var saved = _pictures.Repository.Frames.Get(id).Options;
        Assert.Equal(FitMode.Fit, saved.Fit);
        Assert.True(saved.BlurFill);
        Assert.Equal(SlideshowMode.Cycle, saved.Mode);
        Assert.Equal(SlideshowOrder.Shuffle, saved.Order);
        Assert.Equal(SlideshowTransition.None, saved.Transition);

        var again = Settings();
        Assert.Equal((1, true, 1, 1, 1), (again.FitIndex, again.BlurFill, again.ModeIndex, again.OrderIndex, again.TransitionIndex));
    }

    [Fact]
    public void Options_BelongToTheFrameSelected()
    {
        var first = AddFrame(_assets[0]);
        var second = AddFrame(_assets[1]);
        var vm = Settings();

        vm.SelectedFrame = vm.Frames[1];
        vm.ModeIndex = 1;
        vm.SelectedFrame = vm.Frames[0];

        Assert.Equal(0, vm.ModeIndex);
        Assert.Equal(SlideshowMode.Manual, _pictures.Repository.Frames.Get(first).Options.Mode);
        Assert.Equal(SlideshowMode.Cycle, _pictures.Repository.Frames.Get(second).Options.Mode);
    }

    [Fact]
    public void TheInterval_OffersThePresets_AndACustomNumberAndUnit()
    {
        var id = AddFrame(_assets[0], _assets[1]);
        var vm = Settings();

        Assert.Equal(new[] { "10 s", "30 s", "1 min", "5 min", "10 min", "15 min", "30 min", "1 h", "Custom" }, vm.IntervalChoices);
        Assert.Equal(1, vm.IntervalIndex);

        vm.IntervalIndex = 3;
        Assert.Equal(300, _pictures.Repository.Frames.Get(id).Options.IntervalSeconds);
        Assert.False(vm.IsCustomInterval);

        vm.IntervalIndex = vm.IntervalChoices.Count - 1;
        Assert.True(vm.IsCustomInterval);
        vm.CustomIntervalText = "2 min";
        Assert.Equal(120, _pictures.Repository.Frames.Get(id).Options.IntervalSeconds);
        vm.CustomIntervalText = "nonsense";
        Assert.True(vm.CustomIntervalInvalid);
        Assert.Equal(120, _pictures.Repository.Frames.Get(id).Options.IntervalSeconds);
    }

    [Fact]
    public void ACustomIntervalFromBefore_IsShownAsCustom()
    {
        var id = AddFrame(_assets[0]);
        _layout.UpdatePictures(p => p.SetOptions(id, p.Get(id).Options with { IntervalSeconds = 90 }));

        var vm = Settings();

        Assert.True(vm.IsCustomInterval);
        Assert.Equal("90 s", vm.CustomIntervalText);
    }

    [Fact]
    public void ThePictureList_ShowsEachPicture_AndRemovesAndMovesThem()
    {
        var id = AddFrame(_assets);
        var vm = Settings();

        Assert.Equal(new[] { "p1x10.png", "p2x10.png", "p3x10.png" }, vm.Pictures.Select(p => p.FileName));
        Assert.All(vm.Pictures, p => Assert.StartsWith("pictures/", p.ThumbnailPath));

        vm.Pictures[0].MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "p2x10.png", "p1x10.png", "p3x10.png" }, vm.Pictures.Select(p => p.FileName));
        Assert.Equal(new[] { _assets[1].Id, _assets[0].Id, _assets[2].Id }, _pictures.Repository.Frames.Get(id).Pictures.Select(p => p.AssetId));

        vm.Pictures[2].MoveUpCommand.Execute(null);
        Assert.Equal(new[] { "p2x10.png", "p3x10.png", "p1x10.png" }, vm.Pictures.Select(p => p.FileName));

        vm.Pictures[1].RemoveCommand.Execute(null);
        Assert.Equal(new[] { "p2x10.png", "p1x10.png" }, vm.Pictures.Select(p => p.FileName));
        Assert.False(_pictures.Store.Exists(_assets[2].StoredName));
    }

    [Fact]
    public void TheFirstPicturesUpButton_AndTheLastOnesDownButton_AreOff()
    {
        AddFrame(_assets);
        var vm = Settings();

        Assert.False(vm.Pictures[0].CanMoveUp);
        Assert.True(vm.Pictures[0].CanMoveDown);
        Assert.True(vm.Pictures[2].CanMoveUp);
        Assert.False(vm.Pictures[2].CanMoveDown);
    }

    [Fact]
    public void TheDropLog_IsShownWithItsPath_AndItsFolderCanBeOpened()
    {
        var folder = Path.Combine(Path.GetTempPath(), "chisel-logs-" + Guid.NewGuid().ToString("N"));
        string? opened = null;
        var vm = new PictureSettingsViewModel(_settings, _layout, _pictures.Catalog, Path.Combine(folder, "picture-drop.log"), f => opened = f);

        Assert.True(vm.HasDropLog);
        Assert.Equal("Drop log: " + Path.Combine(folder, "picture-drop.log"), vm.DropLogText);

        vm.OpenDropLogFolderCommand.Execute(null);

        Assert.Equal(folder, opened);
        Directory.Delete(folder);
        Assert.False(Settings().HasDropLog);
    }
}
