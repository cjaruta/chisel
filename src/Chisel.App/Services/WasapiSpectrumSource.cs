using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Chisel.Core.Media;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace Chisel.App.Services;

// Listens to what the speakers are playing (WASAPI loopback) and feeds it to a SpectrumProcessor. Capture only exists
// between Start and Stop. Audio is mixed to mono and only the newest couple of thousand samples are held in memory;
// nothing is written or logged. If the device goes away or capture fails, the bars go flat and it tries again every
// few seconds. Start, Stop and ReadLevels are called from the UI thread; the audio thread only reaches OnData.
public sealed class WasapiSpectrumSource : ISpectrumSource
{
    private const int Bands = 20;
    private static readonly long RetryTicks = 3 * Stopwatch.Frequency;
    private static readonly long SilentAfterTicks = Stopwatch.Frequency / 8;

    private readonly float[] _scratch = new float[4096];
    private WasapiLoopbackCapture? _capture;
    private SpectrumProcessor? _processor;
    private MMDeviceEnumerator? _enumerator;
    private DeviceWatcher? _watcher;
    private int _channels;
    private int _rate;
    private bool _float;
    private volatile bool _reopen;
    private long _lastData;
    private long _retryAt;

    public int BandCount => Bands;
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }
        IsRunning = true;
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _watcher = new DeviceWatcher(this);
            _enumerator.RegisterEndpointNotificationCallback(_watcher);
        }
        catch (Exception)
        {
            // No device notifications; failures are still retried by ReadLevels.
        }
        Open();
    }

    public void Stop()
    {
        IsRunning = false;
        Close();
        try
        {
            if (_enumerator is not null && _watcher is not null)
            {
                _enumerator.UnregisterEndpointNotificationCallback(_watcher);
            }
            _enumerator?.Dispose();
        }
        catch (Exception)
        {
        }
        _enumerator = null;
        _watcher = null;
        _processor?.Clear();
    }

    public void ReadLevels(Span<float> levels, double elapsedSeconds)
    {
        if (IsRunning && (_reopen || (_capture is null && Stopwatch.GetTimestamp() >= _retryAt)))
        {
            Close();
            Open();
        }
        var processor = _processor;
        if (!IsRunning || processor is null)
        {
            levels.Clear();
            return;
        }
        if (_capture is null || Stopwatch.GetTimestamp() - Volatile.Read(ref _lastData) > SilentAfterTicks)
        {
            processor.Clear();
        }
        processor.Update(elapsedSeconds);
        processor.CopyLevels(levels);
    }

    public void Dispose() => Stop();

    private void Open()
    {
        _reopen = false;
        WasapiLoopbackCapture? capture = null;
        try
        {
            capture = new WasapiLoopbackCapture();
            var format = capture.WaveFormat;
            var standard = format is WaveFormatExtensible extensible ? extensible.ToStandardWaveFormat() : format;
            _float = standard.Encoding == WaveFormatEncoding.IeeeFloat;
            if (!_float && format.BitsPerSample != 16)
            {
                throw new NotSupportedException();
            }
            _channels = Math.Max(1, format.Channels);
            if (_processor is null || _rate != format.SampleRate)
            {
                _processor = new SpectrumProcessor(format.SampleRate, Bands);
                _rate = format.SampleRate;
            }
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnStopped;
            Volatile.Write(ref _lastData, 0);
            capture.StartRecording();
            _capture = capture;
        }
        catch (Exception)
        {
            try
            {
                capture?.Dispose();
            }
            catch (Exception)
            {
            }
            _capture = null;
            _retryAt = Stopwatch.GetTimestamp() + RetryTicks;
        }
    }

    private void Close()
    {
        var capture = _capture;
        _capture = null;
        if (capture is null)
        {
            return;
        }
        capture.DataAvailable -= OnData;
        capture.RecordingStopped -= OnStopped;
        try
        {
            capture.StopRecording();
        }
        catch (Exception)
        {
        }
        try
        {
            capture.Dispose();
        }
        catch (Exception)
        {
        }
        _processor?.Clear();
    }

    // Audio thread: mix to mono and hand to the processor.
    private void OnData(object? sender, WaveInEventArgs e)
    {
        var processor = _processor;
        if (processor is null || e.BytesRecorded == 0)
        {
            return;
        }
        Volatile.Write(ref _lastData, Stopwatch.GetTimestamp());
        var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
        var channels = _channels;
        var isFloat = _float;
        var frameBytes = channels * (isFloat ? 4 : 2);
        var frames = bytes.Length / frameBytes;
        var done = 0;
        while (done < frames)
        {
            var count = Math.Min(_scratch.Length, frames - done);
            for (var i = 0; i < count; i++)
            {
                float sum = 0;
                var at = (done + i) * frameBytes;
                for (var c = 0; c < channels; c++)
                {
                    sum += isFloat
                        ? MemoryMarshal.Read<float>(bytes.Slice(at + c * 4, 4))
                        : MemoryMarshal.Read<short>(bytes.Slice(at + c * 2, 2)) / 32768f;
                }
                _scratch[i] = sum / channels;
            }
            processor.Push(_scratch.AsSpan(0, count));
            done += count;
        }
    }

    // The capture ended on its own (device unplugged, driver reset): flat bars now, a retry soon.
    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (ReferenceEquals(sender, _capture))
        {
            Close();
            _retryAt = Stopwatch.GetTimestamp() + RetryTicks;
        }
    }

    // Switching the default playback device means loopback must be reopened on the new one.
    private sealed class DeviceWatcher(WasapiSpectrumSource owner) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                owner._reopen = true;
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner._reopen = true;
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
