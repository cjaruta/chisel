using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Chisel.App.Services;
using Chisel.Core.Links;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// Quick links: shown as small buttons in the top bar (the first few, the rest in a "more" menu) and managed in Settings
// (add, edit in place, reorder, delete). A link is a web address, or a full path to a document or folder that exists;
// programs and scripts are refused, and a target is checked again when it is opened. Opening hands the target to the shell
// with no arguments.
public partial class QuickLinksViewModel : ViewModelBase
{
    // How many links fit in the top bar before the rest go into the menu.
    public const int DefaultMaxVisible = 6;

    private readonly IQuickLinkRepository _repository;
    private readonly Action<string> _openTarget;
    private readonly IPathProbe _probe;
    private readonly int _maxVisible;

    public ObservableCollection<QuickLinkItem> Links { get; } = new();

    // The top bar buttons, and what did not fit.
    public ObservableCollection<QuickLinkItem> Visible { get; } = new();

    public ObservableCollection<QuickLinkItem> Overflow { get; } = new();

    public bool HasLinks => Links.Count > 0;

    public bool HasOverflow => Overflow.Count > 0;

    [ObservableProperty]
    public partial string NewLinkLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLinkTarget { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(FormTitle), nameof(SaveButtonText))]
    public partial Guid? EditingLinkId { get; set; }

    // What is wrong with the form, or null.
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // Why the last link could not be opened, or null.
    [ObservableProperty]
    public partial string? OpenError { get; set; }

    public bool IsEditing => EditingLinkId is not null;

    public string FormTitle => IsEditing ? "Edit link" : "Add a link";

    public string SaveButtonText => IsEditing ? "Save changes" : "Add link";

    public QuickLinksViewModel(IQuickLinkRepository repository, Action<string>? openTarget = null, IPathProbe? probe = null,
        int maxVisible = DefaultMaxVisible)
    {
        _repository = repository;
        _openTarget = openTarget ?? DefaultOpen;
        _probe = probe ?? new FileSystemPathProbe();
        _maxVisible = Math.Max(1, maxVisible);
        Links.CollectionChanged += OnLinksChanged;
        Refresh();
    }

    // The shell opens a web address in the default browser and a document or folder in its default app. Only text that
    // passed QuickLinkTarget reaches here, and no arguments are ever added.
    private static void DefaultOpen(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    public void Refresh()
    {
        Links.Clear();
        foreach (var link in _repository.GetAll())
        {
            Links.Add(new QuickLinkItem(link, Kind(link.Url), this));
        }
    }

    // A saved link's kind: an address is Web; a path is a folder if one is there, otherwise a file.
    private QuickLinkKind Kind(string target)
    {
        var check = QuickLinkTarget.Validate(target, _probe);
        return check.IsValid ? check.Kind : (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? QuickLinkKind.Web : QuickLinkKind.File);
    }

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e) => Split();

    private void Split()
    {
        Visible.Clear();
        Overflow.Clear();
        for (var i = 0; i < Links.Count; i++)
        {
            (i < _maxVisible ? Visible : Overflow).Add(Links[i]);
        }
        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(HasOverflow));
    }

    [RelayCommand]
    private void SaveQuickLink()
    {
        var label = NewLinkLabel.Trim();
        if (label.Length == 0)
        {
            ErrorMessage = "Enter a name for the link.";
            return;
        }
        var check = QuickLinkTarget.Validate(NewLinkTarget, _probe);
        if (!check.IsValid)
        {
            ErrorMessage = check.Error;
            return;
        }

        if (EditingLinkId is { } id)
        {
            var updated = new QuickLink(id, label, check.Target);
            _repository.Update(updated);
            var index = IndexOf(id);
            if (index >= 0)
            {
                Links[index] = new QuickLinkItem(updated, check.Kind, this);
            }
        }
        else
        {
            var link = new QuickLink(label, check.Target);
            _repository.Add(link);
            Links.Add(new QuickLinkItem(link, check.Kind, this));
        }
        ClearForm();
    }

    [RelayCommand]
    private void CancelEdit() => ClearForm();

    private void ClearForm()
    {
        EditingLinkId = null;
        NewLinkLabel = string.Empty;
        NewLinkTarget = string.Empty;
        ErrorMessage = null;
    }

    private int IndexOf(Guid id)
    {
        for (var i = 0; i < Links.Count; i++)
        {
            if (Links[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    internal void BeginEdit(QuickLinkItem item)
    {
        EditingLinkId = item.Id;
        NewLinkLabel = item.Label;
        NewLinkTarget = item.Target;
        ErrorMessage = null;
    }

    internal void Delete(QuickLinkItem item)
    {
        if (EditingLinkId == item.Id)
        {
            ClearForm();
        }
        _repository.Remove(item.Id);
        Links.Remove(item);
    }

    // Moves a link up (-1) or down (+1) one place; stops at the ends.
    internal void MoveBy(QuickLinkItem item, int delta)
    {
        var index = IndexOf(item.Id);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Links.Count)
        {
            return;
        }
        Links.Move(index, target);
        _repository.Reorder(Links.Select(l => l.Id).ToList());
    }

    // Checks the saved target again (it may have been edited elsewhere, or the file may be gone) and opens it.
    internal void Open(QuickLinkItem item)
    {
        var check = QuickLinkTarget.Validate(item.Target, _probe);
        if (!check.IsValid)
        {
            OpenError = $"{item.Label}: {check.Error}";
            return;
        }
        OpenError = null;
        try
        {
            _openTarget(check.Target);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            OpenError = $"{item.Label} could not be opened.";
        }
    }
}
