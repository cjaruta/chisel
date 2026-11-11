using Chisel.App.ViewModels;
using Chisel.Core.Links;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class QuickLinksViewModelTests
{
    private sealed class FakeQuickLinkRepository : IQuickLinkRepository
    {
        private readonly List<QuickLink> _items = new();

        public IReadOnlyList<QuickLink> GetAll() => _items;

        public void Add(QuickLink link) => _items.Add(link);

        public void Remove(Guid id) => _items.RemoveAll(l => l.Id == id);
    }

    [Fact]
    public void Constructor_LoadsExistingLinks()
    {
        var repository = new FakeQuickLinkRepository();
        repository.Add(new QuickLink("Docs", "https://docs.example.com"));

        var viewModel = new QuickLinksViewModel(repository);

        Assert.Equal(new[] { "Docs" }, viewModel.QuickLinks.Select(l => l.Label).ToArray());
    }

    [Fact]
    public void SaveQuickLink_WithLabelAndUrlAndNoEditingLink_AddsToRepositoryAndClearsInputs()
    {
        var repository = new FakeQuickLinkRepository();
        var viewModel = new QuickLinksViewModel(repository)
        {
            NewLinkLabel = "Docs",
            NewLinkUrl = "https://docs.example.com"
        };

        viewModel.SaveQuickLinkCommand.Execute(null);

        Assert.Equal(new[] { "Docs" }, repository.GetAll().Select(l => l.Label).ToArray());
        Assert.Equal(string.Empty, viewModel.NewLinkLabel);
        Assert.Equal(string.Empty, viewModel.NewLinkUrl);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void SaveQuickLink_WithInvalidUrl_SetsErrorMessageAndDoesNotAdd()
    {
        var repository = new FakeQuickLinkRepository();
        var viewModel = new QuickLinksViewModel(repository)
        {
            NewLinkLabel = "Docs",
            NewLinkUrl = "not a url"
        };

        viewModel.SaveQuickLinkCommand.Execute(null);

        Assert.Empty(repository.GetAll());
        Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
    }

    [Fact]
    public void SelectQuickLink_LoadsItIntoTheEditFields()
    {
        var repository = new FakeQuickLinkRepository();
        var link = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(link);
        var viewModel = new QuickLinksViewModel(repository);

        viewModel.SelectQuickLinkCommand.Execute(link);

        Assert.Equal("Docs", viewModel.NewLinkLabel);
        Assert.Equal("https://docs.example.com", viewModel.NewLinkUrl);
        Assert.Equal(link.Id, viewModel.EditingLinkId);
    }

    [Fact]
    public void SaveQuickLink_WhileEditingAnExistingLink_ReplacesItKeepingTheSameId()
    {
        var repository = new FakeQuickLinkRepository();
        var link = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(link);
        var viewModel = new QuickLinksViewModel(repository);
        viewModel.SelectQuickLinkCommand.Execute(link);

        viewModel.NewLinkLabel = "Documentation";
        viewModel.NewLinkUrl = "https://docs.example.com/new";
        viewModel.SaveQuickLinkCommand.Execute(null);

        var saved = Assert.Single(repository.GetAll());
        Assert.Equal(link.Id, saved.Id);
        Assert.Equal("Documentation", saved.Label);
        Assert.Equal("https://docs.example.com/new", saved.Url);
        Assert.Null(viewModel.EditingLinkId);
    }

    [Fact]
    public void CancelEdit_ClearsEditingStateInputsAndError()
    {
        var repository = new FakeQuickLinkRepository();
        var link = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(link);
        var viewModel = new QuickLinksViewModel(repository);
        viewModel.SelectQuickLinkCommand.Execute(link);

        viewModel.CancelEditCommand.Execute(null);

        Assert.Null(viewModel.EditingLinkId);
        Assert.Equal(string.Empty, viewModel.NewLinkLabel);
        Assert.Equal(string.Empty, viewModel.NewLinkUrl);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void RemoveQuickLink_ExistingLink_RemovesFromRepository()
    {
        var repository = new FakeQuickLinkRepository();
        var link = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(link);
        var viewModel = new QuickLinksViewModel(repository);

        viewModel.RemoveQuickLinkCommand.Execute(link);

        Assert.Empty(repository.GetAll());
        Assert.Empty(viewModel.QuickLinks);
    }

    [Fact]
    public void OpenQuickLink_ExistingLink_InvokesOpenUrlWithTheLinksUrl()
    {
        var repository = new FakeQuickLinkRepository();
        var link = new QuickLink("Docs", "https://docs.example.com");
        repository.Add(link);
        string? openedUrl = null;
        var viewModel = new QuickLinksViewModel(repository, url => openedUrl = url);

        viewModel.OpenQuickLinkCommand.Execute(link);

        Assert.Equal("https://docs.example.com", openedUrl);
    }
}
