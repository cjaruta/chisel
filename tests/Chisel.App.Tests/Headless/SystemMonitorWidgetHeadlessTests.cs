using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.SystemMonitor;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class SystemMonitorWidgetHeadlessTests
{
    private readonly FakeTimeProvider _clock = new(UiSnapshot.SampleNow);

    // Opens the window with the system monitor added to the first column; weight is its share of that column's height.
    private MainWindow Open(FakeSystemStats? stats = null, double weight = 1.5, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock, themeMode: themeMode, systemStats: stats ?? new FakeSystemStats());
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.SystemMonitor, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private void Tick(int seconds = 1)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        Dispatcher.UIThread.RunJobs();
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static SystemMonitorWidgetView View(Window w) => w.GetVisualDescendants().OfType<SystemMonitorWidgetView>().Single();

    private static string[] Shown(Window w) =>
        View(w).Gauges.Where(g => g.IsVisible).Select(g => AutomationProperties.GetName(g) ?? "").ToArray();

    [AvaloniaFact]
    public void ShowsEachStat_AsAGauge_NamedWithItsValue()
    {
        var window = Open();
        Tick();

        Assert.Equal(new[]
        {
            "CPU 25 percent",
            "RAM 75 percent, 12 of 16 gigabytes used",
            "Disk 80 percent, 400 of 500 gigabytes used",
            "Network down 10 megabits per second, up 1 megabits per second",
        }, Shown(window));
        var view = View(window);
        Assert.Equal(SystemMonitorLayout.SizeFor(view.Bounds.Width, view.Bounds.Height), view.Layout.Size);
        UiSnapshot.Render(window, "system-monitor");
        window.Close();
    }

    [AvaloniaFact]
    public void LightTheme_Renders()
    {
        var window = Open(new FakeSystemStats { Battery = 80, Charging = true }, themeMode: "light");
        Tick();

        Assert.Contains("Battery 80 percent, charging", Shown(window));
        UiSnapshot.Render(window, "system-monitor-light");
        window.Close();
    }

    [AvaloniaFact]
    public void AStatTheSystemCannotRead_IsHidden()
    {
        var window = Open(new FakeSystemStats { HasDisk = false });
        Tick();

        Assert.DoesNotContain(Shown(window), name => name.StartsWith("Disk"));
        Assert.DoesNotContain(Shown(window), name => name.StartsWith("Battery"));
        window.Close();
    }

    [AvaloniaFact]
    public void ChosenStats_AndRefreshRate_FollowSettings()
    {
        var window = Open();
        var settings = Main(window).SystemMonitorSettings;

        settings.ShowRam = false;
        settings.IsRefresh5 = true;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Shown(window), name => name.StartsWith("RAM"));
        Assert.Equal(TimeSpan.FromSeconds(5), Main(window).SystemMonitor.Sampler.Interval);
        window.Close();
    }

    [AvaloniaFact]
    public void Sampler_RunsOnlyWhileTheWidgetIsOnScreen()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760, clock: _clock, systemStats: new FakeSystemStats());
        var main = Main(window);
        Assert.False(main.SystemMonitor.Sampler.IsRunning);

        main.Layout.Apply(main.Layout.Layout.AddWidget(WidgetRegistry.SystemMonitor, 0, 0, 1));
        Dispatcher.UIThread.RunJobs();
        Assert.True(main.SystemMonitor.Sampler.IsRunning);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(main.SystemMonitor.Sampler.IsRunning);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(main.SystemMonitor.Sampler.IsRunning);

        main.Layout.Apply(main.Layout.Layout.RemoveWidget(WidgetRegistry.SystemMonitor));
        Dispatcher.UIThread.RunJobs();
        Assert.False(main.SystemMonitor.Sampler.IsRunning);
        window.Close();
    }

    [AvaloniaFact]
    public void Gauge_ChangesOnlyWhenWhatItShowsChanges()
    {
        var window = Open();
        Tick();
        var cpu = View(window).Gauges.First(g => g.Stat == SystemStat.Cpu);
        var sparks = View(window).Layout.ShowSparklines;
        var before = cpu.Changes;

        Tick();
        Tick();

        // The CPU stays at 25%, so only a sparkline (which moves every sample) changes the gauge.
        Assert.Equal(sparks ? before + 2 : before, cpu.Changes);
        window.Close();
    }

    // Once running, a sample that changes nothing on screen, and drawing every gauge (rings, text and sparklines), allocate
    // next to nothing.
    [AvaloniaFact]
    public void SampleAndDraw_AllocateNothingInSteadyState()
    {
        var window = Open();
        Tick();
        var view = View(window);
        Assert.True(view.Layout.ShowSparklines, "measure the largest size");
        var sampler = Main(window).SystemMonitor.Sampler;
        var gauges = view.Gauges.Where(g => g.IsVisible).ToArray();
        var bitmap = new RenderTargetBitmap(new PixelSize(400, 400));
        using var context = bitmap.CreateDrawingContext();
        void Frame()
        {
            sampler.Sample();
            foreach (var gauge in gauges)
            {
                gauge.Draw(context);
            }
        }
        for (var i = 0; i < 200; i++)
        {
            Frame();
        }

        const int frames = 60;
        var perFrame = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                Frame();
            }
            perFrame = Math.Min(perFrame, (GC.GetAllocatedBytesForCurrentThread() - before) / frames);
        }
        Assert.True(perFrame <= 64, $"{perFrame} B per sample");
        window.Close();
    }
}
