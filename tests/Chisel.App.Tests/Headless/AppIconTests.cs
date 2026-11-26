using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

public class AppIconTests
{
    [AvaloniaFact]
    public void IconResource_IsEmbeddedAndIsAnIco()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Chisel.App/Assets/chiselIcon.ico"));
        var header = new byte[4];
        Assert.Equal(4, stream.Read(header, 0, 4));
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, header);
    }

    [AvaloniaFact]
    public void MainWindow_HasTheAppIcon()
    {
        var window = new MainWindow();
        Assert.NotNull(window.Icon);
    }
}
