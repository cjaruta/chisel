using System;
using System.Globalization;
using Chisel.Core.Links;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One quick link as the top bar and the Settings list show it. It carries its own commands, so a button inside a menu or a
// list row needs no lookup of the parent view model.
public sealed class QuickLinkItem
{
    internal QuickLinkItem(QuickLink link, QuickLinkKind kind, QuickLinksViewModel owner)
    {
        Id = link.Id;
        Label = link.Label;
        Target = link.Url;
        Kind = kind;
        Initial = FirstLetter(link.Label);
        OpenCommand = new RelayCommand(() => owner.Open(this));
        EditCommand = new RelayCommand(() => owner.BeginEdit(this));
        DeleteCommand = new RelayCommand(() => owner.Delete(this));
        MoveUpCommand = new RelayCommand(() => owner.MoveBy(this, -1));
        MoveDownCommand = new RelayCommand(() => owner.MoveBy(this, 1));
    }

    public Guid Id { get; }
    public string Label { get; }

    // The web address or the full path.
    public string Target { get; }
    public QuickLinkKind Kind { get; }
    public string Initial { get; }
    public bool IsWeb => Kind == QuickLinkKind.Web;
    public bool IsFolder => Kind == QuickLinkKind.Folder;
    public bool IsFile => Kind == QuickLinkKind.File;

    public string Tooltip => $"{Label}\n{Target}";
    public string AutomationName => $"Open {Label}";

    public IRelayCommand OpenCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }

    private static string FirstLetter(string label)
    {
        var text = label.Trim();
        return text.Length == 0 ? "?" : text[..1].ToUpper(CultureInfo.CurrentCulture);
    }
}
