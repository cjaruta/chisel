using System.Collections.Generic;
using Chisel.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public sealed record Alert(string Title, string Message);

// Alerts for things that happen while the app is open (an alarm ringing, a timer done): a banner over the window that stays until
// it is dismissed, with a sound. Several at once are shown one at a time. Not a system notification: nothing shows while the
// app is closed.
public partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertSound _sound;
    private readonly Queue<Alert> _waiting = new();

    [ObservableProperty]
    public partial Alert? Current { get; private set; }

    public int WaitingCount => _waiting.Count;

    public bool HasWaiting => _waiting.Count > 0;

    public AlertsViewModel(IAlertSound? sound = null)
    {
        _sound = sound ?? new NoAlertSound();
    }

    public void Show(string title, string message)
    {
        _sound.Play();
        var alert = new Alert(title, message);
        if (Current is null)
        {
            Current = alert;
        }
        else
        {
            _waiting.Enqueue(alert);
            OnPropertyChanged(nameof(WaitingCount));
        OnPropertyChanged(nameof(HasWaiting));
            OnPropertyChanged(nameof(HasWaiting));
        }
    }

    [RelayCommand]
    private void Dismiss()
    {
        Current = _waiting.Count > 0 ? _waiting.Dequeue() : null;
        OnPropertyChanged(nameof(WaitingCount));
        OnPropertyChanged(nameof(HasWaiting));
    }
}
