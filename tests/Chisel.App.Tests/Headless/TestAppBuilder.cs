using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Chisel.App.Tests.Headless.TestAppBuilder))]

namespace Chisel.App.Tests.Headless;

// Runs the real App (styles, resources, theme) on the headless platform.
// Skia drawing is on so windows can be rendered to PNG; Inter keeps text the same on every machine.
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
