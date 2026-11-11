using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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

    // Renders the common window sizes (main-WIDTHxHEIGHT.png), plus the side panel scrolled
    // to the bottom, so layout changes can be checked by looking at artifacts/ui.
    [AvaloniaTheory]
    [InlineData(1200, 760)]
    [InlineData(1000, 650)]
    [InlineData(1904, 984)]
    public void Render_CommonWindowSizes(int width, int height)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        UiSnapshot.Render(window, $"main-{width}x{height}");

        var sidePanel = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "SidePanel");
        sidePanel.FindAncestorOfType<ScrollViewer>()!.ScrollToEnd();
        var path = UiSnapshot.Render(window, $"main-{width}x{height}-scrolled");

        Assert.True(File.Exists(path));
        window.Close();
    }
}
