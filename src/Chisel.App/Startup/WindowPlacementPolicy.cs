using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Chisel.App.Startup;

public static class WindowPlacementPolicy
{
    // Only a normal window has a meaningful size and position. Minimized windows
    // report a far off-screen position, and maximized ones report the screen size.
    public static WindowPlacement? ForSave(WindowState state, double width, double height, PixelPoint position) =>
        state == WindowState.Normal ? new WindowPlacement(width, height, position.X, position.Y) : null;

    // The title bar strip must land on a connected screen, or the window could reopen out of reach.
    public static bool IsOnScreen(WindowPlacement placement, IEnumerable<PixelRect> workingAreas)
    {
        if (placement.X is not { } x || placement.Y is not { } y)
        {
            return true;
        }

        var titleBar = new PixelRect((int)x, (int)y, 200, 40);
        return workingAreas.Any(area => area.Intersects(titleBar));
    }
}
