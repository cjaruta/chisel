using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Chisel.App.Services;

// A soft sound for the end of a focus or break phase. volume: 1 to 100 (0 plays nothing).
public interface IChime
{
    void Play(int volume);
}

public sealed class NoChime : IChime
{
    public void Play(int volume) { }
}

// Two quiet bell-like notes made in memory (no sound file) and played off the UI thread with NAudio, which the app already uses.
// The volume scales the samples themselves, so the system volume and other apps are left alone. With no audio device it is silent.
public sealed class SoftChime : IChime
{
    public const int SampleRate = 44100;
    public const double PeakAtFullVolume = 0.3; // even at 100 the chime stays well under full scale
    private static readonly (double Hz, double Start)[] Notes = { (880, 0), (1318.5, 0.18) };
    private const double NoteLength = 0.55;

    public void Play(int volume)
    {
        if (volume <= 0)
        {
            return;
        }
        var pcm = Build(volume);
        Task.Run(() =>
        {
            try
            {
                using var stream = new RawSourceWaveStream(new MemoryStream(pcm), new WaveFormat(SampleRate, 16, 1));
                using var output = new WaveOutEvent();
                output.Init(stream);
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(50);
                }
            }
            catch (Exception)
            {
                // No audio device, or it is busy: the visible flash still tells the user.
            }
        });
    }

    // The chime as 16-bit mono PCM: each note a sine with a soft attack and an exponential fade. Loudness follows volume squared,
    // which sounds closer to even steps than a straight line.
    public static byte[] Build(int volume)
    {
        var level = Math.Pow(Math.Clamp(volume, 0, 100) / 100.0, 2) * PeakAtFullVolume;
        var length = (int)((Notes[^1].Start + NoteLength) * SampleRate);
        var pcm = new byte[length * 2];
        for (var i = 0; i < length; i++)
        {
            var t = (double)i / SampleRate;
            var sample = 0.0;
            foreach (var (hz, start) in Notes)
            {
                var local = t - start;
                if (local is >= 0 and < NoteLength)
                {
                    var envelope = Math.Min(1, local / 0.008) * Math.Exp(-local * 7);
                    sample += Math.Sin(2 * Math.PI * hz * local) * envelope * 0.5;
                }
            }
            var value = (short)Math.Round(Math.Clamp(sample * level, -1, 1) * short.MaxValue);
            pcm[2 * i] = (byte)value;
            pcm[2 * i + 1] = (byte)(value >> 8);
        }
        return pcm;
    }
}
