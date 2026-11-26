using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Chisel.Core.Clipboard;

namespace Chisel.App.Services;

// AddClipboardFormatListener on a message-only window that lives on its own thread, so clipboard changes never wait on the UI.
// Start creates the window and registers the listener; Stop closes it, which removes the listener, destroys the window and ends
// the thread. Nothing is left behind after Stop.
internal sealed class Win32ClipboardHook : IClipboardHook
{
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmClose = 0x0010;
    private const uint WmDestroy = 0x0002;
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    private readonly WndProc _proc; // kept here so the delegate outlives the window
    private Thread? _thread;
    private IntPtr _hwnd;
    private volatile Action? _changed;

    public Win32ClipboardHook()
    {
        _proc = WindowProc;
    }

    public void Start(Action changed)
    {
        Stop();
        _changed = changed;
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Clipboard listener" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(Wait);
    }

    public void Stop()
    {
        _changed = null;
        var hwnd = Interlocked.Exchange(ref _hwnd, IntPtr.Zero);
        if (hwnd != IntPtr.Zero)
        {
            PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
        _thread?.Join(Wait);
        _thread = null;
    }

    private void Run(ManualResetEventSlim ready)
    {
        var className = "ChiselClipboardListener" + Environment.CurrentManagedThreadId;
        var instance = GetModuleHandle(null);
        var windowClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = instance,
            lpszClassName = className,
        };
        RegisterClassEx(ref windowClass);
        var hwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, instance, IntPtr.Zero);
        if (hwnd != IntPtr.Zero && !AddClipboardFormatListener(hwnd))
        {
            DestroyWindow(hwnd);
            hwnd = IntPtr.Zero;
        }
        _hwnd = hwnd;
        ready.Set();
        if (hwnd != IntPtr.Zero)
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                DispatchMessage(ref message);
            }
        }
        UnregisterClass(className, instance);
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmClipboardUpdate:
                try
                {
                    _changed?.Invoke();
                }
                catch (Exception)
                {
                    // A listener failed; the next change still comes through.
                }
                return IntPtr.Zero;
            case WmClose:
                RemoveClipboardFormatListener(hwnd);
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "UnregisterClassW")]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessage(out Msg message, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern IntPtr DispatchMessage(ref Msg message);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
    private static extern IntPtr GetModuleHandle(string? name);
}

// Reads the Windows clipboard. Opt-out formats and the owning process are checked first, and the text is not read at all when
// either says not to. Nothing read here is ever written to the log: only that the clipboard was busy.
internal static class Win32Clipboard
{
    private const uint CfUnicodeText = 13;
    private const int OpenAttempts = 5;
    private static readonly TimeSpan OpenRetry = TimeSpan.FromMilliseconds(15);

    private static readonly uint ExcludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint HistoryFormat = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint CloudFormat = RegisterClipboardFormat("CanUploadToCloudClipboard");
    private static readonly uint ViewerIgnoreFormat = RegisterClipboardFormat("Clipboard Viewer Ignore");

    public static ClipboardSnapshot Read(int maxChars, IClipboardHistoryLog? log)
    {
        if (!TryOpen())
        {
            log?.Write("clipboard stayed busy; change skipped");
            return new ClipboardSnapshot(null);
        }
        try
        {
            var flags = ClipboardFlags.None;
            if (IsClipboardFormatAvailable(ExcludeFormat)) flags |= ClipboardFlags.ExcludeFromMonitoring;
            if (ReadDword(HistoryFormat) == 0) flags |= ClipboardFlags.NoHistory;
            if (ReadDword(CloudFormat) == 0) flags |= ClipboardFlags.NoCloudUpload;
            if (IsClipboardFormatAvailable(ViewerIgnoreFormat)) flags |= ClipboardFlags.ViewerIgnore;
            var owner = OwnerProcess();
            if (flags != ClipboardFlags.None || ClipboardFilter.IsPasswordManager(owner) || !IsClipboardFormatAvailable(CfUnicodeText))
            {
                return new ClipboardSnapshot(null, flags, owner);
            }
            return new ClipboardSnapshot(ReadText(maxChars), flags, owner);
        }
        finally
        {
            CloseClipboard();
        }
    }

    // Another app may have the clipboard open for a moment: try a few times, waiting a little longer each time.
    private static bool TryOpen()
    {
        for (var attempt = 1; attempt <= OpenAttempts; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }
            Thread.Sleep(OpenRetry * attempt);
        }
        return false;
    }

    private static string? ReadText(int maxChars)
    {
        var handle = GetClipboardData(CfUnicodeText);
        var pointer = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var chars = (int)Math.Min((long)GlobalSize(handle) / 2, maxChars + 1L);
            var text = Marshal.PtrToStringUni(pointer, chars);
            var end = text.IndexOf('\0');
            return end >= 0 ? text[..end] : text;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static int? ReadDword(uint format)
    {
        if (format == 0 || !IsClipboardFormatAvailable(format))
        {
            return null;
        }
        var handle = GetClipboardData(format);
        var pointer = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            return (long)GlobalSize(handle) >= 4 ? Marshal.ReadInt32(pointer) : null;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static string? OwnerProcess()
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero || GetWindowThreadProcessId(owner, out var pid) == 0)
        {
            return null;
        }
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClipboardFormatW")]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
}
