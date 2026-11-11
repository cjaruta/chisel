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
    private void AddQuickLink()
    {
        QuickLink link;
        try
        {
            link = new QuickLink(NewLinkLabel, NewLinkUrl);
        }
        catch (ArgumentException)
        {
            return;
        }

        _repository.Add(link);
        NewLinkLabel = string.Empty;
        NewLinkUrl = string.Empty;
        Refresh();
    }

    [RelayCommand]
    private void RemoveQuickLink(QuickLink link)
    {
        _repository.Remove(link.Id);
        Refresh();
    }

    [RelayCommand]
    private void OpenQuickLink(QuickLink link) => _openUrl(link.Url);
}
