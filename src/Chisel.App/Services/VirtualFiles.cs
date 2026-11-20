using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Chisel.Core.Pictures;

namespace Chisel.App.Services;

// Opens the "virtual file" a browser offers for a dragged picture (Windows: FileGroupDescriptorW with FileContents).
public interface IVirtualFiles
{
    // Called on the UI thread during the drop: does what must be done there and returns the reader to run off it, or null
    // when this drop has no readable virtual file. The reader returns null when the file cannot be read.
    Func<CancellationToken, Task<VirtualFile?>>? Open(IDataTransfer data, IPictureDropLog? log);
}

public static class PlatformVirtualFiles
{
    public static IVirtualFiles? Default { get; } = OperatingSystem.IsWindows() ? new WindowsVirtualFiles() : null;
}

// Reads the first virtual file of a drop through COM. Avalonia only reads whole-object formats (FileContents needs an index),
// so the native IDataObject behind its drop data is taken (by reflection: it is not public) and read directly. Every step
// can fail (another Avalonia version, a source that gives no stream); then Open or the reader gives null and the caller
// falls back to the other candidates. The object is handed to a worker thread through the COM stream marshaller, so the
// UI thread is never blocked.
internal sealed class WindowsVirtualFiles : IVirtualFiles
{
    private const string Descriptor = "FileGroupDescriptorW";
    private const string Contents = "FileContents";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public Func<CancellationToken, Task<VirtualFile?>>? Open(IDataTransfer data, IPictureDropLog? log)
    {
        try
        {
            if (!data.Formats.Any(f => f.Identifier == Contents))
            {
                log?.Write("virtual file: no FileContents");
                return null;
            }
            var descriptor = data.TryGetValue(DataFormat.CreateBytesPlatformFormat(Descriptor));
            var name = (descriptor is null ? null : FileDescriptors.Names(descriptor).FirstOrDefault()) is { Length: > 0 } first ? first : "picture";
            var stream = Marshal(data);
            if (stream is null)
            {
                log?.Write("virtual file: the native data object is not reachable");
                return null;
            }
            return cancellation => Task.Run(() => Read(stream, name, log), cancellation).WaitAsync(Timeout, cancellation);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log?.Write($"virtual file: not available ({ex.GetType().Name})");
            return null;
        }
    }

    // The drop's native IDataObject, marshalled into a stream any thread can unpack.
    private static IStream? Marshal(IDataTransfer data)
    {
        var field = data.GetType().GetField("_oleDataObject", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var proxy = field?.GetValue(data);
        if (proxy is null)
        {
            return null;
        }
        var pointer = (IntPtr?)Properties(proxy.GetType()).FirstOrDefault(p => p.Name == "NativePointer" && p.PropertyType == typeof(IntPtr))?.GetValue(proxy);
        if (pointer is not { } native || native == IntPtr.Zero)
        {
            return null;
        }
        var obj = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(native);
        var iid = typeof(System.Runtime.InteropServices.ComTypes.IDataObject).GUID;
        return CoMarshalInterThreadInterfaceInStream(ref iid, obj, out var stream) == 0 ? stream : null;
    }

    private static IEnumerable<PropertyInfo> Properties(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                yield return property;
            }
        }
    }

    private static VirtualFile? Read(IStream marshalled, string name, IPictureDropLog? log)
    {
        try
        {
            var iid = typeof(System.Runtime.InteropServices.ComTypes.IDataObject).GUID;
            if (CoGetInterfaceAndReleaseStream(marshalled, ref iid, out var obj) != 0 || obj is not System.Runtime.InteropServices.ComTypes.IDataObject data)
            {
                log?.Write("virtual file: could not reach the data object");
                return null;
            }
            var format = new FORMATETC
            {
                cfFormat = unchecked((short)RegisterClipboardFormat(Contents)),
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = 0,
                ptd = IntPtr.Zero,
                tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL,
            };
            data.GetData(ref format, out var medium);
            try
            {
                var bytes = medium.tymed switch
                {
                    TYMED.TYMED_ISTREAM => ReadStream((IStream)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(medium.unionmember)),
                    TYMED.TYMED_HGLOBAL => ReadGlobal(medium.unionmember),
                    _ => null,
                };
                log?.Write($"virtual file: read {bytes?.Length ?? 0} bytes ({medium.tymed})");
                return bytes is { Length: > 0 } ? new VirtualFile(bytes, name) : null;
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or IOException or ArgumentException or InvalidOperationException)
        {
            log?.Write($"virtual file: read failed ({ex.GetType().Name} {ex.Message})");
            return null;
        }
    }

    private static byte[]? ReadStream(IStream stream)
    {
        var read = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
        try
        {
            stream.Seek(0, 0, IntPtr.Zero);
            using var all = new MemoryStream();
            var buffer = new byte[64 * 1024];
            while (all.Length <= ImageLimits.MaxDownloadBytes)
            {
                stream.Read(buffer, buffer.Length, read);
                var count = System.Runtime.InteropServices.Marshal.ReadInt32(read);
                if (count <= 0)
                {
                    break;
                }
                all.Write(buffer, 0, count);
            }
            return all.ToArray();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(read);
        }
    }

    private static byte[]? ReadGlobal(IntPtr handle)
    {
        var size = (long)GlobalSize(handle);
        if (size <= 0 || size > ImageLimits.MaxDownloadBytes + 1)
        {
            return null;
        }
        var memory = GlobalLock(handle);
        if (memory == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var bytes = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(memory, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoMarshalInterThreadInterfaceInStream(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] object unknown, out IStream stream);

    [DllImport("ole32.dll")]
    private static extern int CoGetInterfaceAndReleaseStream(IStream stream, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object result);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
}
