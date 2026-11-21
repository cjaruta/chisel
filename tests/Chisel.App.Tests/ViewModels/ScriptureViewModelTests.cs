using Chisel.App.ViewModels;
using Chisel.Core.Scripture;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ScriptureViewModelTests
{
    private static readonly ScriptureCatalog Catalog = ScriptureCatalog.Bundled;

    private static FakeTimeProvider At(int hour, int minute = 0, int day = 20)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(-5)).ToUniversalTime());
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-5), "test", "test"));
        return time;
    }

    [Fact]
    public void Today_IsThePassageForTheLocalDate()
    {
        var vm = new ScriptureViewModel(At(9));
        Assert.Same(Catalog.ForDate(new DateOnly(2026, 11, 20)), vm.Passage);
        Assert.Equal(vm.Passage.Text, vm.Text);
        Assert.Equal(vm.Passage.Reference, vm.Reference);
        Assert.Equal("WEB", vm.Translation);
    }

    [Fact]
    public void Today_UsesTheLocalDay_NotUtc()
    {
        // 22:30 local on the 20th is already the 21st in UTC.
        var vm = new ScriptureViewModel(At(22, 30));
        Assert.Same(Catalog.ForDate(new DateOnly(2026, 11, 20)), vm.Passage);
    }

    [Fact]
    public void Today_IsTheSameAfterARestart_AndAfterTicksOnTheSameDay()
    {
        var time = At(9);
        var first = new ScriptureViewModel(time);
        time.Advance(TimeSpan.FromHours(10));
        first.Tick();
        var reopened = new ScriptureViewModel(time);
        Assert.Same(first.Passage, reopened.Passage);
    }

    [Fact]
    public void Tick_ChangesThePassageAtLocalMidnight_AndOnlyThen()
    {
        var time = At(23, 59);
        var vm = new ScriptureViewModel(time);
        var before = vm.Passage;
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        time.Advance(TimeSpan.FromSeconds(30));
        vm.Tick();
        Assert.Same(before, vm.Passage);
        Assert.Empty(changes);

        time.Advance(TimeSpan.FromSeconds(30));
        vm.Tick();
        Assert.Same(Catalog.ForDate(new DateOnly(2026, 11, 21)), vm.Passage);
        Assert.NotSame(before, vm.Passage);
        Assert.Contains(nameof(ScriptureViewModel.Text), changes);
        Assert.Contains(nameof(ScriptureViewModel.Reference), changes);
    }

    [Fact]
    public void Copy_WritesTheVerseReferenceAndTranslation()
    {
        string? copied = null;
        var vm = new ScriptureViewModel(At(9)) { WriteClipboard = text => copied = text };
        vm.CopyCommand.Execute(null);
        Assert.Equal(Catalog.CopyText(vm.Passage), copied);
        Assert.Contains(vm.Reference, copied);
        Assert.Contains("(WEB)", copied);
    }

    [Fact]
    public void Copy_ShowsCopiedForTwoSeconds()
    {
        var time = At(9);
        var vm = new ScriptureViewModel(time) { WriteClipboard = _ => { } };
        Assert.False(vm.IsCopied);
        vm.CopyCommand.Execute(null);
        Assert.True(vm.IsCopied);
        time.Advance(TimeSpan.FromSeconds(3));
        vm.Tick();
        Assert.False(vm.IsCopied);
    }

    [Fact]
    public void Copy_WithNoClipboard_DoesNotShowCopied()
    {
        var vm = new ScriptureViewModel(At(9));
        vm.CopyCommand.Execute(null);
        Assert.False(vm.IsCopied);
    }

    [Fact]
    public void ReadInContext_OpensTheWebChapter()
    {
        string? opened = null;
        var vm = new ScriptureViewModel(At(9), openUrl: url => opened = url);
        vm.ReadInContextCommand.Execute(null);
        Assert.Equal(Catalog.ChapterUrl(vm.Passage), opened);
        Assert.StartsWith("https://ebible.org/eng-web/", opened);
    }
}
