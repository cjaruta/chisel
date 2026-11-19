namespace Chisel.Core.Media;

// What is true at the moment Space is pressed in the main window.
// OverlayOpen: Settings, an editor dialog, or any flyout or popup is open or holds focus.
// FocusUsesSpace: the focused control is a text input or anything that uses Space itself (button, check box, slider, list item...).
// CanPlayPause: there is a media session and it reports play or pause as available.
public readonly record struct SpaceKeyContext(bool IsRepeat, bool OverlayOpen, bool FocusUsesSpace, bool IsEditing, bool CanPlayPause);

public static class MediaShortcuts
{
    public static bool ShouldTogglePlayPause(SpaceKeyContext context) =>
        !context.IsRepeat && !context.OverlayOpen && !context.FocusUsesSpace && !context.IsEditing && context.CanPlayPause;
}
