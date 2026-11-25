using Chisel.App.Startup;
using Chisel.App.Tests.Support;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Startup;

public class AppShellTests
{
    private readonly FakeShellWindow _window = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
    private SystemSettings _settings = SystemSettings.Default;
    private int _shutdowns;

    private AppShell Shell(bool hasTray = true) => new(_window, () => _settings, hasTray, () => _shutdowns++, _time);

    [Fact]
    public void UserClose_WithQuitSetting_IsNotCancelled()
    {
        Assert.False(Shell().HandleClosing(userClose: true));
        Assert.True(_window.IsVisible);
    }

    [Fact]
    public void UserClose_WithTraySetting_HidesInsteadOfClosing()
    {
        _settings = _settings with { CloseToTray = true };
        Assert.True(Shell().HandleClosing(userClose: true));
        Assert.False(_window.IsVisible);
    }

    [Fact]
    public void Close_IsNeverCancelled_WhenQuitting_OrForShutdown_OrWithoutATray()
    {
        _settings = _settings with { CloseToTray = true };
        var shell = Shell();
        Assert.False(shell.HandleClosing(userClose: false));
        Assert.False(Shell(hasTray: false).HandleClosing(userClose: true));
        shell.Quit();
        Assert.Equal(1, _shutdowns);
        Assert.False(shell.HandleClosing(userClose: true));
        Assert.True(_window.IsVisible);
    }

    [Fact]
    public void Toggle_ShowsWhenNotActive_AndHidesWhenActive()
    {
        _settings = _settings with { CloseToTray = true };
        var shell = Shell();
        _window.IsActive = false;
        shell.Toggle();
        Assert.Equal(1, _window.Shows);
        shell.Toggle();
        Assert.False(_window.IsVisible);
    }

    [Fact]
    public void Toggle_MinimizesInsteadOfHiding_WhenClosingQuitsOrThereIsNoTray()
    {
        Shell().Toggle();
        Assert.True(_window.IsMinimized);
        Assert.True(_window.IsVisible);

        _settings = _settings with { CloseToTray = true };
        _window.ShowAndActivate();
        Shell(hasTray: false).Toggle();
        Assert.True(_window.IsMinimized);
        Assert.True(_window.IsVisible);
    }

    [Fact]
    public void TrayClick_JustAfterTheWindowLostFocusToTheTray_Hides()
    {
        _settings = _settings with { CloseToTray = true };
        var shell = Shell();
        _window.IsActive = false;
        shell.NoteDeactivated();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        shell.TrayClicked();
        Assert.False(_window.IsVisible);
    }

    [Fact]
    public void TrayDoubleClick_CountsAsOneClick()
    {
        _settings = _settings with { CloseToTray = true };
        var shell = Shell();
        _window.Hide();
        shell.TrayClicked();
        _time.Advance(TimeSpan.FromMilliseconds(150));
        shell.TrayClicked();
        Assert.True(_window.IsVisible);
        Assert.Equal(1, _window.Shows);
    }

    [Fact]
    public void MenuShowHide_HidesAVisibleWindow_AndShowsAHiddenOrMinimizedOne()
    {
        _settings = _settings with { CloseToTray = true };
        var shell = Shell();
        _window.IsActive = false;
        shell.MenuShowHide();
        Assert.False(_window.IsVisible);
        shell.MenuShowHide();
        Assert.True(_window.IsVisible);
        _window.Minimize();
        shell.MenuShowHide();
        Assert.False(_window.IsMinimized);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void StartsHidden_OnlyWhenLaunchedMinimized_WithTheSettingOn_AndATray(bool launchedMinimized, bool setting, bool hasTray, bool expected)
    {
        _settings = _settings with { StartMinimized = setting };
        Assert.Equal(expected, Shell(hasTray).ShouldStartHidden(launchedMinimized));
    }
}
