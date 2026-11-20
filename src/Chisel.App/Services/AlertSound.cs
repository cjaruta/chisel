using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Chisel.App.Services;

// The sound that goes with an alert.
public interface IAlertSound
{
    void Play();
}

public sealed class NoAlertSound : IAlertSound
{
    public void Play() { }
}

// Three system beeps, off the UI thread. Uses the Windows MessageBeep call, so it needs no extra package.
public sealed class WindowsAlertSound : IAlertSound
{
    private const uint Exclamation = 0x30;

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    public void Play() => Task.Run(async () =>
    {
        for (var i = 0; i < 3; i++)
        {
            MessageBeep(Exclamation);
            await Task.Delay(500);
        }
    });
}
