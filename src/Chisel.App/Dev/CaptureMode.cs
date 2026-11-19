using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Dev;

// Dev only: `dotnet run --project src/Chisel.App -- --capture drag|remove|add|clock`. Drives the real window through a
// scripted edit, saves a render of it every ~16 ms into artifacts/ui/capture/<scenario>/, then exits.
internal static class CaptureMode
{
    // The weather-motion scenario's data: always rain, no network, no location.
    public static Chisel.Core.Weather.IWeatherService RainWeather() => new RainService();

    private sealed class RainService : Chisel.Core.Weather.IWeatherService
    {
        public System.Threading.Tasks.Task<Chisel.Core.Weather.WeatherSnapshot> GetAsync(System.Threading.CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(new Chisel.Core.Weather.WeatherSnapshot(14, 12, Chisel.Core.Weather.WeatherCondition.Rain, 16, 9,
                Enumerable.Range(0, 8).Select(i => new Chisel.Core.Weather.HourlyWeather(new TimeOnly(10 + i, 0), Chisel.Core.Weather.WeatherCondition.Rain, 14 - i)).ToList()));
    }

    // The media scenario's data: sample metadata and a made-up cover, no system media session involved.
    public static Chisel.Core.Media.MediaSessionInfo SampleMedia() =>
        Chisel.App.Services.FakeMediaService.Sample() with { AlbumArt = SampleArt.Png() };

    public static string? Scenario(string[]? args)
    {
        var i = Array.IndexOf(args ?? [], "--capture");
        return i >= 0 && i + 1 < args!.Length ? args[i + 1] : null;
    }

    public static void Run(MainWindow window, string scenario, Action exit)
    {
        var dir = Path.Combine(FindRoot(), "artifacts", "ui", "capture", scenario);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        window.Width = 1200;
        window.Height = 760;
        var vm = (MainViewModel)window.DataContext!;
        LayoutHost host = null!;
        var saves = new List<Task>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var frameNo = 0;
        var step = 0;
        var marks = new List<(int At, string Name)>();
        var log = new List<string>();
        ClockFace? face = null;

        void Mark(string name) { marks.Add((frameNo, name)); }
        var script = new List<(int Ms, Action Do)>();
        if (scenario is not ("clock" or "weather-motion" or "media" or "media-visualizer"))
        {
            script.Add((600, () => { vm.CustomizeLayoutCommand.Execute(null); Mark("edit mode"); }));
        }
        switch (scenario)
        {
            case "clock":
                // Shows the clock without edit mode or saving, then just runs so the second hand can be watched.
                script.Add((500, () =>
                {
                    var layout = vm.Layout.Layout;
                    // Already on the saved dashboard? Then it is just there; otherwise show it without saving.
                    if (layout.Find(WidgetRegistry.Clock) is null)
                    {
                        vm.Layout.Apply(layout.AddWidget(WidgetRegistry.Clock, 0, layout.Columns[0].Widgets.Count, 1));
                    }
                    face = window.GetVisualDescendants().OfType<ClockFace>().FirstOrDefault();
                    Mark("clock shown");
                }));
                script.Add((3500, () => Mark("clock running")));
                break;
            case "media":
                // Shows the media widget with sample metadata (nothing needs to be playing); nothing is saved.
                script.Add((500, () =>
                {
                    var layout = vm.Layout.Layout;
                    if (layout.Find(WidgetRegistry.Media) is null)
                    {
                        vm.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1.4));
                    }
                    Mark("media shown");
                }));
                script.Add((1500, () => Mark("media playing")));
                break;
            case "media-visualizer":
            {
                // The media widget playing sample metadata with a synthetic signal through the real spectrum processor and bars
                // (App runs this on a temp database; nothing is captured from the system). CPU and GC are logged over a window
                // with the bars on (A) and one with them off (B), after a warmup so JIT is not counted.
                var vcpu = new List<string>();
                (TimeSpan Cpu, long Bytes, int G0, int G1, int G2, long Ms) vMark = default;
                void VStart() => vMark = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime, GC.GetTotalAllocatedBytes(false),
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), clock.ElapsedMilliseconds);
                void VStop(string name)
                {
                    var wall = (clock.ElapsedMilliseconds - vMark.Ms) / 1000.0;
                    var used = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - vMark.Cpu).TotalSeconds;
                    vcpu.Add($"{name}: cpu {used:F3}s over {wall:F2}s = {100 * used / wall:F1}% of one core; allocated {(GC.GetTotalAllocatedBytes(false) - vMark.Bytes) / wall / 1024:F1} KB/s; GCs gen0 {GC.CollectionCount(0) - vMark.G0} gen1 {GC.CollectionCount(1) - vMark.G1} gen2 {GC.CollectionCount(2) - vMark.G2}");
                    File.WriteAllLines(Path.Combine(dir, "cpu.txt"), vcpu);
                }
                script.Add((100, () =>
                {
                    var layout = vm.Layout.Layout;
                    if (layout.Find(WidgetRegistry.Media) is null)
                    {
                        vm.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, 1.4));
                    }
                    Mark("media shown");
                }));
                script.Add((3000, () => Mark("bars moving")));
                script.Add((6000, VStart));
                script.Add((8000, () => { VStop("A bars on"); vm.MediaSettings.ShowVisualizer = false; }));
                script.Add((8500, VStart));
                script.Add((10500, () => VStop("B bars off")));
                break;
            }
            case "weather-motion":
                // Rain scene on the weather widget from a fake service; App runs this scenario on a temp database and an in-memory
                // weather store, so no real data is read or written. Frames are saved densely around a forced layout
                // rebuild; CPU and GC are logged over a window with motion (A) and one without (B, weather Loading).
                var cpu = new List<string>();
                (TimeSpan Cpu, long Bytes, int G0, int G1, int G2, long Ms, long Callbacks, long Renders, double RenderMs) mark0 = default;
                void Start() => mark0 = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime, GC.GetTotalAllocatedBytes(false),
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), clock.ElapsedMilliseconds,
                    Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Callbacks, Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Renders, Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.RenderMilliseconds);
                void Stop(string name)
                {
                    var wall = (clock.ElapsedMilliseconds - mark0.Ms) / 1000.0;
                    var used = (System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime - mark0.Cpu).TotalSeconds;
                    cpu.Add($"{name}: cpu {used:F3}s over {wall:F2}s = {100 * used / wall:F1}% of one core; allocated {(GC.GetTotalAllocatedBytes(false) - mark0.Bytes) / wall / 1024:F1} KB/s; GCs gen0 {GC.CollectionCount(0) - mark0.G0} gen1 {GC.CollectionCount(1) - mark0.G1} gen2 {GC.CollectionCount(2) - mark0.G2}; motion handlers running {Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Running}, frame callbacks {(Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Callbacks - mark0.Callbacks) / wall:F0}/s, draws {(Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Renders - mark0.Renders) / wall:F1}/s taking {(Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.RenderMilliseconds - mark0.RenderMs) / Math.Max(1, Chisel.App.Views.Widgets.Backdrop.WeatherMotionStats.Renders - mark0.Renders):F3} ms each");
                    File.WriteAllLines(Path.Combine(dir, "cpu.txt"), cpu);
                }
                script.Add((100, () => Mark("start")));
                script.Add((300, () =>
                {
                    var layout = vm.Layout.Layout;
                    if (layout.Find(WidgetRegistry.Weather) is null)
                    {
                        vm.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, 0.6));
                    }
                }));
                script.Add((4700, () => Mark("before rebuild")));
                script.Add((5000, () =>
                {
                    Mark("rebuild");
                    var layout = vm.Layout.Layout;
                    vm.Layout.Apply(layout.Find(WidgetRegistry.Clock) is null
                        ? layout.AddWidget(WidgetRegistry.Clock, 1, 0, 0.5)
                        : layout.RemoveWidget(WidgetRegistry.Clock));
                }));
                // Measure after a few seconds of warmup so JIT of the motion code isn't counted as its running cost.
                script.Add((9000, Start));
                script.Add((11000, () => { Stop("A motion on"); vm.Weather.Show(WeatherState.Loading); }));
                script.Add((11500, Start));
                script.Add((13500, () => Stop("B motion off")));
                break;
            case "drag":
                script.Add((1500, () => { host.ScriptPickUp("links"); Mark("pick up links"); }));
                script.Add((2600, () => { host.ScriptMove(host.CenterOf("tasks", 0.7)); Mark("move"); }));
                script.Add((3700, () => { host.ScriptDrop(host.CenterOf("tasks", 0.7)); Mark("drop"); }));
                break;
            case "remove":
                script.Add((1500, () => { vm.Layout.RemoveWidget("notes"); Mark("remove notes"); }));
                break;
            case "add":
                script.Add((1500, () => { vm.Layout.RemoveWidget("notes"); Mark("remove notes"); }));
                script.Add((3000, () => { vm.Layout.AddWidgetFromTray("notes"); Mark("add notes"); }));
                break;
        }
        var end = script[^1].Ms + 2000;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        void OnTick()
        {
            var ms = clock.ElapsedMilliseconds;
            while (step < script.Count && ms >= script[step].Ms)
            {
                try { script[step++].Do(); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString()); timer.Stop(); exit(); return; }
            }
            if (ms > 400 && (scenario is not ("weather-motion" or "media-visualizer") || (scenario == "weather-motion" ? ms is > 4600 and < 5700 : ms is > 3000 and < 3300)))
            {
                var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                if (size.Width > 0)
                {
                    face ??= window.GetVisualDescendants().OfType<ClockFace>().FirstOrDefault();
                    log.Add(scenario == "clock" ? $"{frameNo} {ms} {face?.SecondAngle}" : $"{frameNo} {ms} {host.DebugBorder("links")}");
                    var bmp = new RenderTargetBitmap(size);
                    bmp.Render(window);
                    var path = Path.Combine(dir, $"{frameNo:D4}_{ms:D5}ms.png");
                    frameNo++;
                    saves.Add(Task.Run(() => { bmp.Save(path); bmp.Dispose(); }));
                }
            }
            if (ms > end)
            {
                timer.Stop();
                File.WriteAllLines(Path.Combine(dir, "border.log"), log);
                File.WriteAllLines(Path.Combine(dir, "marks.txt"), marks.Select(m => $"{m.At} {m.Name}"));
                Task.WhenAll(saves).ContinueWith(_ => Dispatcher.UIThread.Post(() => { if (scenario == "clock" && face is not null) ClockSheet.Write(dir, marks[0].At, frameNo, window, face); else ContactSheet.Write(dir, marks, frameNo); exit(); }));
            }
        }
        // Anything thrown while capturing is written to error.txt and the app exits, instead of hanging.
        timer.Tick += (_, _) =>
        {
            try { OnTick(); }
            catch (Exception ex) { timer.Stop(); File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString()); exit(); }
        };
        window.Opened += (_, _) => { host = window.GetVisualDescendants().OfType<LayoutHost>().Single(); clock.Restart(); timer.Start(); };
    }

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Chisel.slnx")) && !File.Exists(Path.Combine(d.FullName, "Chisel.sln")))
            d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }
}

// One 3x2 sheet per marked step: 6 frames, every 4th one, starting just before the step.
internal static class ContactSheet
{
    public static void Write(string dir, List<(int At, string Name)> marks, int total)
    {
        foreach (var (at, name) in marks.Skip(1))
        {
            var target = new RenderTargetBitmap(new PixelSize(1200, 760));
            using (var ctx = target.CreateDrawingContext())
            {
                for (var i = 0; i < 6; i++)
                {
                    var n = at - 1 + i * 4;
                    var file = Directory.GetFiles(dir, $"{n:D4}_*.png").FirstOrDefault();
                    if (file is null) continue;
                    using var bmp = new Bitmap(file);
                    ctx.DrawImage(bmp, new Rect(bmp.Size), new Rect(i % 3 * 400, i / 3 * 380, 400, 380));
                }
            }
            target.Save(Path.Combine(dir, $"sheet_{name.Replace(' ', '_')}.png"));
        }
    }
}

// The clock scenario: the clock card cropped from 6 frames spread evenly over the run, to see the second hand step.
internal static class ClockSheet
{
    public static void Write(string dir, int first, int total, Window window, ClockFace face)
    {
        var origin = face.TranslatePoint(new Point(0, 0), window) ?? default;
        var side = Math.Min(face.Bounds.Width, face.Bounds.Height);
        var crop = new Rect(origin.X + (face.Bounds.Width - side) / 2, origin.Y + (face.Bounds.Height - side) / 2, side, side);
        var target = new RenderTargetBitmap(new PixelSize(1200, 800));
        using (var ctx = target.CreateDrawingContext())
        {
            for (var i = 0; i < 6; i++)
            {
                var n = first + i * (total - first) / 6;
                var file = Directory.GetFiles(dir, $"{n:D4}_*.png").FirstOrDefault();
                if (file is null) continue;
                using var bmp = new Bitmap(file);
                ctx.DrawImage(bmp, crop, new Rect(i % 3 * 400, i / 3 * 400, 400, 400));
            }
        }
        target.Save(Path.Combine(dir, "sheet_clock.png"));
    }
}
