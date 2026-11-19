using Chisel.Core.Media;

namespace Chisel.Core.Tests.Media;

public class MediaShortcutsTests
{
    private static readonly SpaceKeyContext Ready = new(IsRepeat: false, OverlayOpen: false, FocusUsesSpace: false, IsEditing: false, CanPlayPause: true);

    [Fact]
    public void Space_TogglesWhenNothingGetsInTheWay() => Assert.True(MediaShortcuts.ShouldTogglePlayPause(Ready));

    public static TheoryData<string, SpaceKeyContext> Blocked => new()
    {
        { "key repeat", Ready with { IsRepeat = true } },
        { "settings, dialog, flyout or popup open", Ready with { OverlayOpen = true } },
        { "focus is a text box or other control that uses Space", Ready with { FocusUsesSpace = true } },
        { "widget editing", Ready with { IsEditing = true } },
        { "no session, or play/pause not available", Ready with { CanPlayPause = false } },
    };

    [Theory]
    [MemberData(nameof(Blocked))]
    public void Space_DoesNothing_WhenBlocked(string reason, SpaceKeyContext context)
    {
        Assert.False(MediaShortcuts.ShouldTogglePlayPause(context), reason);
    }
}
