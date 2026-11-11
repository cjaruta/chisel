using Avalonia.Headless.XUnit;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

public class UiSnapshotTests
{
    [AvaloniaFact]
    public void MainWindow_SavesPlacementToTheGivenPathInsteadOfAppData()
    {
        var path = Path.Combine(Path.GetTempPath(), "chisel-window-" + Guid.NewGuid() + ".json");
        try
        {
            var window = new MainWindow(path);
            window.Show();
            window.Close();

            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void Render_WritesAPngUnderArtifactsUi()
    {
        var window = UiSnapshot.ShowMainWindow();

        var path = UiSnapshot.Render(window, "snapshot-smoke-test");

        Assert.EndsWith(Path.Combine("artifacts", "ui", "snapshot-smoke-test.png"), path);
        var header = File.ReadAllBytes(path).Take(4).ToArray();
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, header);
        window.Close();
    }
}
