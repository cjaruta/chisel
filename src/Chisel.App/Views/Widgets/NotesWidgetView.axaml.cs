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

    public NotesWidgetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }


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
