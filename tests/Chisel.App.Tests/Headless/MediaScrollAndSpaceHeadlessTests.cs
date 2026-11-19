using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;

namespace Chisel.App.Tests.Headless;

public class MediaScrollAndSpaceHeadlessTests
{
    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main, FakeMediaService Media, FakeVolumeService Volume) Open(bool withWidget, double weight = 1.4)
    {
        var media = new FakeMediaService { Current = FakeMediaService.Sample() };
        var volume = new FakeVolumeService();
        var window = UiSnapshot.ShowMainWindow(media: media, volume: volume);
        var main = (MainViewModel)window.DataContext!;
        if (withWidget)
        {
            main.Layout.Apply(main.Layout.Layout.AddWidget(WidgetRegistry.Media, 0, 0, weight));
        }
        Dispatcher.UIThread.RunJobs();
        return (window, main, media, volume);
    }

    // Spins the mouse wheel over the middle of the media widget and says whether the event ended up handled.
    private static bool Wheel(Chisel.App.Views.MainWindow window, double notches)
    {
        var view = window.GetVisualDescendants().OfType<MediaWidgetView>().Single();
        var at = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), window)!.Value;
        var handled = false;
        window.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) => handled = e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);
        window.MouseMove(at);
        window.MouseWheel(at, new Vector(0, notches));
        Dispatcher.UIThread.RunJobs();
        return handled;
    }

    private static void PressSpace(Chisel.App.Views.MainWindow window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Wheel_OverTheWidget_ChangesTheVolumeAndIsHandled_WhenTheSettingIsOn()
    {
        var (window, main, _, volume) = Open(withWidget: true);
        main.MediaSettings.ScrollVolume = true;

        var handled = Wheel(window, 1);

        Assert.True(handled);
        Assert.Equal(0.52, volume.Level, 6);
    }

    [AvaloniaFact]
    public void Wheel_OverTheWidget_IsLeftAlone_WhenTheSettingIsOff()
    {
        var (window, _, _, volume) = Open(withWidget: true);

        var handled = Wheel(window, 1);

        Assert.False(handled);
        Assert.Equal(0.5, volume.Level, 6);
    }

    [AvaloniaFact]
    public void Wheel_ShowsAQuietPercentage_WhenTheVolumeRowIsHidden()
    {
        var (window, main, _, _) = Open(withWidget: true);
        main.MediaSettings.ScrollVolume = true;
        main.MediaSettings.ShowVolume = false;
        Dispatcher.UIThread.RunJobs();
        var label = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "MediaVolumeFeedback");
        Assert.False(label.IsEffectivelyVisible);

        Wheel(window, 2);

        Assert.True(label.IsEffectivelyVisible);
        Assert.Equal("54%", label.Text);
    }

    [AvaloniaFact]
    public void Space_OnTheMainWindow_TogglesPlayPause_WithoutAMediaWidget()
    {
        var (window, _, media, _) = Open(withWidget: false);

        PressSpace(window);

        Assert.Equal(["Toggle"], media.Calls);
    }

    [AvaloniaFact]
    public void Space_InATextBox_DoesNotToggle()
    {
        var (window, _, media, _) = Open(withWidget: false);
        var box = window.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible);
        box.Focus();

        PressSpace(window);

        Assert.Empty(media.Calls);
    }

    [AvaloniaFact]
    public void Space_WhileSettingsAreOpen_OrEditingWidgets_DoesNotToggle()
    {
        var (window, main, media, _) = Open(withWidget: false);
        main.Settings.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        PressSpace(window);
        main.Settings.IsOpen = false;
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        PressSpace(window);

        Assert.Empty(media.Calls);
    }

    // Looked at by eye: artifacts/ui/media-scroll-feedback.png (volume row hidden, percentage showing).
    [AvaloniaFact]
    public void Render_WritesAPng_WithThePercentageShowing()
    {
        var (window, main, _, _) = Open(withWidget: true, weight: 1.4);
        main.MediaSettings.ScrollVolume = true;
        main.MediaSettings.ShowVolume = false;
        Dispatcher.UIThread.RunJobs();
        Wheel(window, 3);

        var path = UiSnapshot.Render(window, "media-scroll-feedback");

        Assert.True(File.Exists(path));
    }
}
