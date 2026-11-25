using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Clipboard;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClipboardHistoryViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeClipboardStore _store = new();
    private readonly FakeClipboardSettings _settings = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly CapturingClipboardLog _log = new();

    private ClipboardHistoryViewModel Create() => new(_clipboard, _settings, _store, _time, action => action(), _log);

    private static string[] Texts(ClipboardHistoryViewModel vm) => vm.Items.Select(i => i.Text).ToArray();

    [Fact]
    public void NothingIsMonitored_UntilStart()
    {
        var vm = Create();
        _clipboard.Copy("before start");

        Assert.False(vm.IsRecording);
        Assert.False(_clipboard.IsListening);
        Assert.Equal(0, _clipboard.Hook.Starts);
        Assert.Empty(vm.Items);

        vm.Start();
        _clipboard.Copy("after start");

        Assert.True(vm.IsRecording);
        Assert.Equal(new[] { "after start" }, Texts(vm));
    }

    [Fact]
    public void Stop_ReleasesTheListener_AndNothingMoreIsCaptured()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("one");

        vm.Stop();
        _clipboard.Copy("two");

        Assert.False(_clipboard.IsListening);
        Assert.Equal((1, 1), (_clipboard.Hook.Starts, _clipboard.Hook.Stops));
        Assert.Equal(new[] { "one" }, Texts(vm));
    }

    [Fact]
    public void Pause_StopsListening_UntilResumed()
    {
        var vm = Create();
        vm.Start();
        vm.TogglePause();
        _clipboard.Copy("while paused");

        Assert.True(vm.IsPaused);
        Assert.False(_clipboard.IsListening);
        Assert.Empty(vm.Items);

        vm.TogglePause();
        _clipboard.Copy("resumed");
        Assert.Equal(new[] { "resumed" }, Texts(vm));
    }

    [Fact]
    public void TakingTheWidgetOffTheDashboard_StopsRecording()
    {
        var vm = Create();
        vm.IsOnDashboard = true;
        vm.Start();

        vm.IsOnDashboard = false;

        Assert.False(vm.IsRecording);
        Assert.False(_clipboard.IsListening);
    }

    [Fact]
    public void Dispose_ReleasesTheListener()
    {
        var vm = Create();
        vm.Start();
        vm.Dispose();
        Assert.False(_clipboard.IsListening);
    }

    [Fact]
    public void Duplicates_MoveToTheTop_AndTheCapApplies()
    {
        _settings.Saved = _settings.Saved with { MaxItems = 2 };
        var vm = Create();
        vm.Start();
        foreach (var text in new[] { "a", "b", "a", "c" })
        {
            _clipboard.Copy(text);
        }
        Assert.Equal(new[] { "c", "a" }, Texts(vm));
    }

    [Fact]
    public void ExcludedLongAndSecretCopies_AreSkipped_AndTheReadIsCappedAtTheLimit()
    {
        _settings.Saved = _settings.Saved with { MaxLength = 200 };
        var vm = Create();
        vm.Start();

        _clipboard.Copy("from a password manager", ClipboardFlags.ExcludeFromMonitoring);
        _clipboard.Copy("owned by one", owner: "KeePass");
        _clipboard.Copy(new string('x', 201));
        _clipboard.Copy("4111 1111 1111 1111");
        _clipboard.Copy(null); // a picture or files: not text
        _clipboard.Copy("kept");

        Assert.Equal(new[] { "kept" }, Texts(vm));
        Assert.Equal(200, _clipboard.MaxCharsAsked);
    }

    [Fact]
    public async Task CopyingAnItemBack_IsNotRecordedAgain_AndShowsCopiedBriefly()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("first");
        _clipboard.Copy("second");
        var first = vm.Items[1];

        await vm.CopyAsync(first.Id);

        Assert.Equal("first", _clipboard.Current.Text);
        Assert.Equal(new[] { "second", "first" }, Texts(vm));
        Assert.Equal(first.Id, vm.CopiedId);

        _time.Advance(ClipboardHistoryViewModel.CopiedFor);
        Assert.Null(vm.CopiedId);

        _clipboard.Copy("first"); // copied elsewhere later: a real copy again
        Assert.Equal(new[] { "first", "second" }, Texts(vm));
    }

    [Fact]
    public void PinDeleteAndClearAll_ChangeTheList()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("a");
        _clipboard.Copy("b");

        vm.TogglePin(vm.Items[1].Id);
        Assert.Equal(new[] { "a", "b" }, Texts(vm));

        vm.Delete(vm.Items[1].Id);
        Assert.Equal(new[] { "a" }, Texts(vm));

        vm.ClearAll();
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Search_FiltersTheVisibleItems()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("apple pie");
        _clipboard.Copy("banana");
        vm.Query = "APPLE";
        Assert.Equal(new[] { "apple pie" }, vm.Visible.Select(i => i.Text));
        vm.Query = "";
        Assert.Equal(2, vm.Visible.Count);
    }

    [Fact]
    public void History_IsNotSavedByDefault()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("in memory only");
        vm.TogglePin(vm.Items[0].Id);
        vm.ClearAll();

        Assert.False(vm.KeepHistory);
        Assert.Equal(0, _store.Saves);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public void KeepingHistory_SavesAndReloads_AndTurningItOffDeletesTheFile()
    {
        var vm = Create();
        vm.Start();
        _clipboard.Copy("before keeping");
        vm.KeepHistory = true;
        _clipboard.Copy("pinned");
        vm.TogglePin(vm.Items[0].Id);

        Assert.True(_settings.Saved.KeepHistory);
        Assert.Equal(new[] { "pinned", "before keeping" }, _store.Saved!.Select(e => e.Text));

        var reopened = Create();
        Assert.False(reopened.IsRecording); // recording always starts off
        Assert.Equal(new[] { "pinned", "before keeping" }, Texts(reopened));
        Assert.True(reopened.Items[0].Pinned);

        reopened.KeepHistory = false;
        Assert.Null(_store.Saved);
        Assert.True(_store.Deletes > 0);
        Assert.Empty(Create().Items);
    }

    [Fact]
    public void MaxItems_IsClampedAndSaved()
    {
        var vm = Create();
        vm.MaxItems = 500;
        Assert.Equal(ClipboardHistorySettings.MaxItemsLimit, vm.MaxItems);
        Assert.Equal(ClipboardHistorySettings.MaxItemsLimit, _settings.Saved.MaxItems);
    }

    [Fact]
    public async Task NoClipboardText_EverReachesTheLog()
    {
        var texts = new[] { "my private note", "4111 1111 1111 1111", "aZ3kP9qL2mX7vB4nR8tY1wC6", "owned by a password manager" };
        var vm = Create();
        vm.Start();
        _clipboard.Copy(texts[0]);
        _clipboard.Copy(texts[1]);
        _clipboard.Copy(texts[2]);
        _clipboard.Copy(texts[3], owner: "1Password");
        _clipboard.Copy(texts[0]);
        vm.TogglePin(vm.Items[0].Id);
        await vm.CopyAsync(vm.Items[0].Id);
        vm.KeepHistory = true;
        vm.Delete(vm.Items[0].Id);
        vm.ClearAll();
        vm.Stop();

        Assert.NotEmpty(_log.Lines);
        Assert.All(_log.Lines, line => Assert.All(texts, t => Assert.DoesNotContain(t, line)));
        Assert.All(_log.Lines, line => Assert.DoesNotContain("private", line));
    }

    [Fact]
    public void SharedListener_StartsOnce_AndStopsWhenTheLastListenerLeaves()
    {
        var hook = new FakeClipboardHook();
        var shared = new SharedClipboardListener(hook);
        var calls = 0;

        var first = shared.Listen(() => calls++);
        var second = shared.Listen(() => calls++);
        hook.Fire();

        Assert.Equal(1, hook.Starts);
        Assert.Equal(2, calls);

        first.Dispose();
        first.Dispose(); // twice is harmless
        Assert.True(shared.IsListening);
        hook.Fire();
        Assert.Equal(3, calls);

        second.Dispose();
        Assert.False(shared.IsListening);
        Assert.Equal((1, 1), (hook.Starts, hook.Stops));
    }
}
