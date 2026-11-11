using System;
using System.Collections.ObjectModel;
using Chisel.Core.Links;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class QuickLinksViewModel : ViewModelBase
{
    private readonly IQuickLinkRepository _repository;

    public ObservableCollection<QuickLink> QuickLinks { get; } = new();

    [ObservableProperty]
    public partial string NewLinkLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLinkUrl { get; set; } = string.Empty;

    public QuickLinksViewModel(IQuickLinkRepository repository)
    {
        _repository = repository;
        Refresh();
    }

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
}
