using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The notepad widget. Typing is saved by the view model; this class only moves focus to where the user is about to type
// (the editor after New, the name box when renaming) and saves pending edits when the widget leaves the window
// (hidden from the dashboard, or the window closing).
public partial class NotesWidgetView : WidgetView
{
    private NotesViewModel? _notes;

    // A very short card still shows a whole line to type in: the editor's padding tightens when a line would not fit with the
    // usual padding, and only when even that is too little does the pad bar step aside (it comes back on a taller card).
    private const double LineRoom = 22;
    private const double UsualPadding = 10 * 2;
    private const double ShortPadding = 6 * 2;
    private double _barRoom = 42;

    public NotesWidgetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        NotepadRoot.SizeChanged += (_, e) => FitEditor(e.NewSize.Height);
    }

    private void FitEditor(double height)
    {
        if (PadBar.IsVisible && PadBar.DesiredSize.Height > 0)
        {
            _barRoom = PadBar.DesiredSize.Height;
        }
        var showBar = height - _barRoom >= LineRoom + ShortPadding;
        PadBar.IsVisible = showBar;
        var editor = height - (showBar ? _barRoom : 0);
        PadEditor.Classes.Set("short", editor < LineRoom + UsualPadding);
    }

    // Like the other widgets: side margins always, and with no title bar extra room at the top for the edit-mode badges,
    // so entering edit mode moves nothing.
    protected override void OnHeaderShownChanged() =>
        NotepadRoot.Margin = BodyMargin(HeaderShown);


    private void Attach()
    {
        if (_notes is not null)
        {
            _notes.PropertyChanged -= OnNotesChanged;
        }
        _notes = (DataContext as MainViewModel)?.Notes;
        if (_notes is not null)
        {
            _notes.PropertyChanged += OnNotesChanged;
        }
    }

    private void OnNotesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotesViewModel.IsRenaming) && _notes is { IsRenaming: true })
        {
            Dispatcher.UIThread.Post(() =>
            {
                RenameBox.Focus();
                RenameBox.SelectAll();
            }, DispatcherPriority.Input);
        }
        else if (e.PropertyName == nameof(NotesViewModel.IsRenaming) && _notes is { IsRenaming: false })
        {
            Dispatcher.UIThread.Post(() => PadEditor.Focus(), DispatcherPriority.Input);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _notes?.Flush();
        base.OnDetachedFromVisualTree(e);
    }
}
