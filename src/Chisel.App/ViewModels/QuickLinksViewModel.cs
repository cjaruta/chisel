using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Chisel.Core.Links;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class QuickLinksViewModel : ViewModelBase
{
    private readonly IQuickLinkRepository _repository;
    private readonly Action<string> _openUrl;

    public ObservableCollection<QuickLink> QuickLinks { get; } = new();

    [ObservableProperty]
    public partial string NewLinkLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLinkUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Guid? EditingLinkId { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public QuickLinksViewModel(IQuickLinkRepository repository, Action<string>? openUrl = null)
    {
        _repository = repository;
        _openUrl = openUrl ?? DefaultOpenUrl;
        Refresh();
    }

    private static void DefaultOpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public void Refresh()
    {
        QuickLinks.Clear();
        foreach (var link in _repository.GetAll())
        {
            QuickLinks.Add(link);
        }
    }

    [RelayCommand]
    private void SelectQuickLink(QuickLink link)
    {
        EditingLinkId = link.Id;
        NewLinkLabel = link.Label;
        NewLinkUrl = link.Url;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingLinkId = null;
        NewLinkLabel = string.Empty;
        NewLinkUrl = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void SaveQuickLink()
    {
        QuickLink link;
        try
        {
            link = EditingLinkId is { } id
                ? new QuickLink(id, NewLinkLabel, NewLinkUrl)
                : new QuickLink(NewLinkLabel, NewLinkUrl);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        if (EditingLinkId is not null)
        {
            _repository.Remove(link.Id);
        }

        _repository.Add(link);
        EditingLinkId = null;
        NewLinkLabel = string.Empty;
        NewLinkUrl = string.Empty;
        ErrorMessage = null;
        Refresh();
    }

    [RelayCommand]
    private void RemoveQuickLink(QuickLink link)
    {
        if (EditingLinkId == link.Id)
        {
            EditingLinkId = null;
            NewLinkLabel = string.Empty;
            NewLinkUrl = string.Empty;
            ErrorMessage = null;
        }

        _repository.Remove(link.Id);
        Refresh();
    }

    [RelayCommand]
    private void OpenQuickLink(QuickLink link) => _openUrl(link.Url);
}
