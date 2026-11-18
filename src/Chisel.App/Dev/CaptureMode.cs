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

namespace Chisel.App.Dev;

// Dev only: `dotnet run --project src/Chisel.App -- --capture drag|remove|add`. Drives the real window through a
// scripted edit, saves a render of it every ~16 ms into artifacts/ui/capture/<scenario>/, then exits.
internal static class CaptureMode
{
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

        void Mark(string name) { marks.Add((frameNo, name)); }
        var script = new List<(int Ms, Action Do)>();
        script.Add((600, () => { vm.CustomizeLayoutCommand.Execute(null); Mark("edit mode"); }));
        switch (scenario)
        {
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
        timer.Tick += (_, _) =>
        {
            var ms = clock.ElapsedMilliseconds;
            while (step < script.Count && ms >= script[step].Ms)
            {
                script[step++].Do();
            }
            if (ms > 400)
            {
                var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                if (size.Width > 0)
                {
                    log.Add($"{frameNo} {ms} {host.DebugBorder("links")}");
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
                Task.WhenAll(saves).ContinueWith(_ => Dispatcher.UIThread.Post(() => { ContactSheet.Write(dir, marks, frameNo); exit(); }));
            }
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
