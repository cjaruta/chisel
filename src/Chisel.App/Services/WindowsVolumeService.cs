using System;
using Avalonia.Threading;
using Chisel.Core.Media;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Chisel.App.Services;

// The default playback device's master volume and mute (WASAPI endpoint volume). It reads both at start, listens for
// changes made elsewhere, and re-attaches when the default device changes, appears or goes away. COM callbacks arrive
// on other threads, so they only post to the UI thread; every public member is used from the UI thread. All failures
// are swallowed: with no usable device IsAvailable is false and the setters do nothing.
public sealed class WindowsVolumeService : IVolumeService, IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private Watcher? _watcher;
    private MMDevice? _device;
    private AudioEndpointVolume? _endpoint;
    private bool _disposed;

    public bool IsAvailable { get; private set; }
    public double Level { get; private set; }
    public bool IsMuted { get; private set; }

    public event EventHandler? Changed;

    public WindowsVolumeService()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _watcher = new Watcher(this);
            _enumerator.RegisterEndpointNotificationCallback(_watcher);
        }
        catch (Exception)
        {
            // Without device notifications the volume still works for the device found now.
        }
        Attach();
    }

    public void SetLevel(double level)
    {
        try
        {
            if (_endpoint is not null)
            {
                _endpoint.MasterVolumeLevelScalar = (float)VolumeMath.Clamp(level);
            }
        }
        catch (Exception)
        {
        }
    }

    public void SetMuted(bool muted)
    {
        try
        {
            if (_endpoint is not null)
            {
                _endpoint.Mute = muted;
            }
        }
        catch (Exception)
        {
        }
    }

    public void ToggleMute() => SetMuted(!IsMuted);

    public void Dispose()
    {
        _disposed = true;
        Detach();
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
    }

    // Finds the current default playback device, reads its state and starts listening to it.
    private void Attach()
    {
        Detach();
        try
        {
            if (_enumerator is null)
            {
                throw new InvalidOperationException();
            }
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _endpoint = _device.AudioEndpointVolume;
            Level = VolumeMath.Clamp(_endpoint.MasterVolumeLevelScalar);
            IsMuted = _endpoint.Mute;
            _endpoint.OnVolumeNotification += OnVolume;
            IsAvailable = true;
        }
        catch (Exception)
        {
            Detach();
            IsAvailable = false;
            Level = 0;
            IsMuted = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Detach()
    {
        try
        {
            if (_endpoint is not null)
            {
                _endpoint.OnVolumeNotification -= OnVolume;
            }
            _endpoint?.Dispose();
            _device?.Dispose();
        }
        catch (Exception)
        {
        }
        _endpoint = null;
        _device = null;
    }

    // Any thread: hand the new numbers to the UI thread.
    private void OnVolume(AudioVolumeNotificationData data)
    {
        var level = VolumeMath.Clamp(data.MasterVolume);
        var muted = data.Muted;
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || _endpoint is null)
            {
                return;
            }
            Level = level;
            IsMuted = muted;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private void Reattach() => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            Attach();
        }
    });

    private sealed class Watcher(WindowsVolumeService owner) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                owner.Reattach();
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner.Reattach();
        public void OnDeviceAdded(string pwstrDeviceId) => owner.Reattach();
        public void OnDeviceRemoved(string deviceId) => owner.Reattach();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
