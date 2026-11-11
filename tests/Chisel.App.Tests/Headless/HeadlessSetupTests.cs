using Avalonia;
using Avalonia.Headless.XUnit;

namespace Chisel.App.Tests.Headless;

public class HeadlessSetupTests
{
    [AvaloniaFact]
    public void HeadlessApp_IsTheRealAppWithItsResources()
    {
        Assert.IsType<App>(Application.Current);
        Assert.True(Application.Current!.Resources.ContainsKey("WindowBackgroundBrush"));
    }
}
