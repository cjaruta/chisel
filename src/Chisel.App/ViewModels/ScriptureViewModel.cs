using System;
using System.Diagnostics;
using Chisel.Core.Scripture;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The daily verse widget's state: today's passage (one per local calendar day, from the date alone, so it never changes
// on resize, hide/show or reopening and is the same after a restart), and the two actions on it. Tick is called often;
// it moves to the next passage when the local date changes.
public partial class ScriptureViewModel : ViewModelBase
{
    private static readonly TimeSpan CopiedFor = TimeSpan.FromSeconds(2);

    private readonly TimeProvider _time;
    private readonly ScriptureCatalog _catalog;
    private readonly Action<string> _openUrl;
    private DateOnly _date;
    private DateTimeOffset? _copiedAt;

    public ScriptureViewModel(TimeProvider time, ScriptureCatalog? catalog = null, Action<string>? openUrl = null)
    {
        _time = time;
        _catalog = catalog ?? ScriptureCatalog.Bundled;
        _openUrl = openUrl ?? DefaultOpenUrl;
        _date = Today();
        Passage = _catalog.ForDate(_date);
    }

    public ScripturePassage Passage { get; private set; }

    public string Text => Passage.Text;

    public string Reference => Passage.Reference;

    public string Translation => _catalog.Source.Abbreviation;

    // Puts text on the clipboard. Set by the view, which can reach the clipboard; Copy does nothing without it.
    public Action<string>? WriteClipboard { get; set; }

    // True for a moment after Copy, so the button can say so.
    public bool IsCopied => _copiedAt is not null;

    public void Tick()
    {
        var today = Today();
        if (today != _date)
        {
            _date = today;
            Passage = _catalog.ForDate(today);
            OnPropertyChanged(nameof(Passage));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Reference));
        }
        if (_copiedAt is { } at && _time.GetUtcNow() - at >= CopiedFor)
        {
            _copiedAt = null;
            OnPropertyChanged(nameof(IsCopied));
        }
    }

    [RelayCommand]
    private void Copy()
    {
        if (WriteClipboard is not { } write)
        {
            return;
        }
        write(_catalog.CopyText(Passage));
        _copiedAt = _time.GetUtcNow();
        OnPropertyChanged(nameof(IsCopied));
    }

    [RelayCommand]
    private void ReadInContext() => _openUrl(_catalog.ChapterUrl(Passage));

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private static void DefaultOpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
