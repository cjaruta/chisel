using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Chisel.Infrastructure.Startup;

namespace Chisel.App.Startup;

// The last line of defence for errors nothing else caught. Every one is written to the crash log.
//  - UI thread: Chisel saves, says something went wrong, and keeps running. If errors keep coming (more than three in ten
//    seconds) it saves, removes its tray icon and lets the error end the app, rather than flood the screen.
//  - A background task nobody waited for: only logged.
//  - Fatal (the process is ending): saves and removes the tray icon once, so no ghost icon or lost note is left behind.
public sealed class CrashHandler
{
    private const int MaxUiErrors = 3;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly CrashLog _log;
    private readonly TimeProvider _time;
    private readonly Queue<DateTimeOffset> _recent = new();
    private int _ended;

    public CrashHandler(CrashLog log, TimeProvider? time = null)
    {
        _log = log;
        _time = time ?? TimeProvider.System;
    }

    public CrashLog Log => _log;

    // Filled in once the app is up: how to tell the user, save pending work, and remove the tray icon.
    public Action<string, string>? ShowMessage { get; set; }
    public Action? Save { get; set; }
    public Action? CleanUp { get; set; }

    // Before Avalonia starts: errors on other threads and unobserved tasks.
    public void InstallProcessHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                OnFatal(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            OnUnobservedTask(e.Exception);
            e.SetObserved();
        };
    }

    // Once Avalonia is running.
    public void InstallUiHandler() => Dispatcher.UIThread.UnhandledException += (_, e) => e.Handled = OnUiThread(e.Exception);

    // True when Chisel carries on; false when the error should end the app.
    public bool OnUiThread(Exception exception)
    {
        _log.Write(exception, "UI thread");
        var now = _time.GetUtcNow();
        _recent.Enqueue(now);
        while (_recent.Count > 0 && now - _recent.Peek() > Window)
        {
            _recent.Dequeue();
        }
        if (_recent.Count > MaxUiErrors)
        {
            End();
            return false;
        }

        var saved = Try(Save);
        Try(() => ShowMessage?.Invoke("Something went wrong",
            (saved ? "Chisel ran into an unexpected problem, saved your work and kept running." : "Chisel ran into an unexpected problem and kept running.")
            + " If it keeps happening, Settings > Help > Open logs folder has a short report to attach to an issue."));
        return true;
    }

    public void OnUnobservedTask(Exception exception) => _log.Write(exception, "background task");

    public void OnFatal(Exception exception)
    {
        _log.Write(exception, "fatal");
        End();
    }

    private void End()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0)
        {
            Try(Save);
            Try(CleanUp);
        }
    }

    private static bool Try(Action? action)
    {
        try
        {
            action?.Invoke();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
