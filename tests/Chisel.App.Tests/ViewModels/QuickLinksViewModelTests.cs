using Chisel.App.ViewModels;
using Chisel.Core.Links;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class QuickLinksViewModelTests
{
    private sealed class FakeQuickLinkRepository : IQuickLinkRepository
    {
        public List<QuickLink> Items { get; } = new();

        public IReadOnlyList<QuickLink> GetAll() => Items.ToList();

        public void Add(QuickLink link) => Items.Add(link);

        public void Update(QuickLink link)
        {
            var index = Items.FindIndex(l => l.Id == link.Id);
            if (index < 0)
            {
                throw new InvalidOperationException("missing");
            }
            Items[index] = link;
        }

        public void Remove(Guid id) => Items.RemoveAll(l => l.Id == id);

        public void Reorder(IReadOnlyList<Guid> orderedIds)
        {
            var order = orderedIds.ToList();
            Items.Sort((x, y) => order.IndexOf(x.Id).CompareTo(order.IndexOf(y.Id)));
        }
    }

    private sealed class FakeProbe(params string[] existing) : IPathProbe
    {
        public bool DirectoryExists(string path) => existing.Contains(path) && !Path.HasExtension(path);
        public bool FileExists(string path) => existing.Contains(path) && Path.HasExtension(path);
    }

    private static readonly FakeProbe Probe = new(@"C:\Docs", @"C:\Docs\plan.pdf");

    private static (QuickLinksViewModel Vm, FakeQuickLinkRepository Repo, List<string> Opened) Make(int maxVisible = 6, params QuickLink[] links)
    {
        var repo = new FakeQuickLinkRepository();
        repo.Items.AddRange(links);
        var opened = new List<string>();
        return (new QuickLinksViewModel(repo, opened.Add, Probe, maxVisible), repo, opened);
    }

    private static QuickLink Web(string label, string host = "example.com") => new(label, $"https://{host}");

    private static string[] Labels(IEnumerable<QuickLinkItem> items) => items.Select(i => i.Label).ToArray();

    [Fact]
    public void Constructor_LoadsTheSavedLinksInOrder_AndSplitsTheTopBarFromTheMenu()
    {
        var (vm, _, _) = Make(2, Web("A"), Web("B"), Web("C"), Web("D"));

        Assert.Equal(new[] { "A", "B", "C", "D" }, Labels(vm.Links));
        Assert.Equal(new[] { "A", "B" }, Labels(vm.Visible));
        Assert.Equal(new[] { "C", "D" }, Labels(vm.Overflow));
        Assert.True(vm.HasLinks);
        Assert.True(vm.HasOverflow);
    }

    [Fact]
    public void WithNoLinks_NothingIsShown()
    {
        var (vm, _, _) = Make();

        Assert.False(vm.HasLinks);
        Assert.False(vm.HasOverflow);
        Assert.Empty(vm.Visible);
    }

    [Fact]
    public void ItemsKnowTheirKindInitialAndTooltip()
    {
        var (vm, _, _) = Make(6, new QuickLink("github", "https://github.com"), new QuickLink("Docs", @"C:\Docs"), new QuickLink("Plan", @"C:\Docs\plan.pdf"));

        Assert.Equal(new[] { QuickLinkKind.Web, QuickLinkKind.Folder, QuickLinkKind.File }, vm.Links.Select(l => l.Kind).ToArray());
        Assert.Equal("G", vm.Links[0].Initial);
        Assert.Contains("github", vm.Links[0].Tooltip);
        Assert.Contains("https://github.com", vm.Links[0].Tooltip);
        Assert.Equal("Open github", vm.Links[0].AutomationName);
    }

    [Fact]
    public void Save_AddsAValidLinkAtTheEnd_SavesIt_AndClearsTheForm()
    {
        var (vm, repo, _) = Make(6, Web("A"));

        vm.NewLinkLabel = "  Mail  ";
        vm.NewLinkTarget = "https://mail.example.com";
        vm.SaveQuickLinkCommand.Execute(null);

        Assert.Equal(new[] { "A", "Mail" }, Labels(vm.Links));
        Assert.Equal(new[] { "A", "Mail" }, repo.Items.Select(l => l.Label).ToArray());
        Assert.Equal(string.Empty, vm.NewLinkLabel);
        Assert.Equal(string.Empty, vm.NewLinkTarget);
        Assert.Null(vm.ErrorMessage);
    }

    [Theory]
    [InlineData("", "https://example.com", "name")]
    [InlineData("X", "", "address")]
    [InlineData("X", "ftp://example.com", "http")]
    [InlineData("X", "file:///C:/Windows/notepad.exe", "http")]
    [InlineData("X", @"C:\Missing\file.txt", "doesn't exist")]
    [InlineData("X", @"C:\Docs\setup.exe", "programs and scripts")]
    public void Save_WithBadInput_ShowsWhatIsWrong_AndSavesNothing(string label, string target, string expected)
    {
        var (vm, repo, _) = Make();

        vm.NewLinkLabel = label;
        vm.NewLinkTarget = target;
        vm.SaveQuickLinkCommand.Execute(null);

        Assert.Contains(expected, vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Items);
        Assert.Empty(vm.Links);
    }

    [Fact]
    public void Save_AcceptsAFolderAndADocument_ThatExist()
    {
        var (vm, repo, _) = Make();

        vm.NewLinkLabel = "Docs";
        vm.NewLinkTarget = @"C:\Docs";
        vm.SaveQuickLinkCommand.Execute(null);
        vm.NewLinkLabel = "Plan";
        vm.NewLinkTarget = "\"C:\\Docs\\plan.pdf\"";
        vm.SaveQuickLinkCommand.Execute(null);

        Assert.Equal(new[] { QuickLinkKind.Folder, QuickLinkKind.File }, vm.Links.Select(l => l.Kind).ToArray());
        Assert.Equal(@"C:\Docs\plan.pdf", repo.Items[1].Url);
    }

    [Fact]
    public void Edit_LoadsTheFormAndSavesInPlace_WithoutMovingTheLink()
    {
        var (vm, repo, _) = Make(6, Web("A"), Web("B"), Web("C"));
        var b = vm.Links[1];

        b.EditCommand.Execute(null);
        Assert.True(vm.IsEditing);
        Assert.Equal("B", vm.NewLinkLabel);
        Assert.Equal("https://example.com", vm.NewLinkTarget);

        vm.NewLinkLabel = "Bee";
        vm.NewLinkTarget = "https://bee.example.com";
        vm.SaveQuickLinkCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(new[] { "A", "Bee", "C" }, Labels(vm.Links));
        Assert.Equal(b.Id, vm.Links[1].Id);
        Assert.Equal("https://bee.example.com", repo.Items[1].Url);
    }

    [Fact]
    public void CancelEdit_ClearsTheFormWithoutSaving()
    {
        var (vm, repo, _) = Make(6, Web("A"));
        vm.Links[0].EditCommand.Execute(null);
        vm.NewLinkLabel = "Changed";

        vm.CancelEditCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(string.Empty, vm.NewLinkLabel);
        Assert.Equal("A", repo.Items[0].Label);
    }

    [Fact]
    public void Delete_RemovesTheLink_AndEndsAnEditOfIt()
    {
        var (vm, repo, _) = Make(6, Web("A"), Web("B"));
        vm.Links[0].EditCommand.Execute(null);

        vm.Links[0].DeleteCommand.Execute(null);

        Assert.Equal(new[] { "B" }, Labels(vm.Links));
        Assert.Equal(new[] { "B" }, repo.Items.Select(l => l.Label).ToArray());
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void Reorder_MovesAndSavesTheOrder_AndRefreshesTheTopBarSplit()
    {
        var (vm, repo, _) = Make(2, Web("A"), Web("B"), Web("C"));

        vm.Links[2].MoveUpCommand.Execute(null);

        Assert.Equal(new[] { "A", "C", "B" }, Labels(vm.Links));
        Assert.Equal(new[] { "A", "C" }, Labels(vm.Visible));
        Assert.Equal(new[] { "B" }, Labels(vm.Overflow));
        Assert.Equal(new[] { "A", "C", "B" }, repo.Items.Select(l => l.Label).ToArray());

        vm.Links[0].MoveDownCommand.Execute(null);
        Assert.Equal(new[] { "C", "A", "B" }, Labels(vm.Links));
    }

    [Fact]
    public void Reorder_AtTheEnds_DoesNothing()
    {
        var (vm, _, _) = Make(6, Web("A"), Web("B"));

        vm.Links[0].MoveUpCommand.Execute(null);
        vm.Links[1].MoveDownCommand.Execute(null);

        Assert.Equal(new[] { "A", "B" }, Labels(vm.Links));
    }

    [Fact]
    public void TheOrderAndContents_SurviveAReload()
    {
        var (vm, repo, _) = Make(6, Web("A"), Web("B"), Web("C"));
        vm.Links[2].MoveUpCommand.Execute(null);
        vm.NewLinkLabel = "D";
        vm.NewLinkTarget = "https://d.example.com";
        vm.SaveQuickLinkCommand.Execute(null);

        var reloaded = new QuickLinksViewModel(repo, _ => { }, Probe);

        Assert.Equal(new[] { "A", "C", "B", "D" }, Labels(reloaded.Links));
    }

    [Fact]
    public void Open_SendsAValidTargetToTheShell_AndNothingElse()
    {
        var (vm, _, opened) = Make(6, Web("A"), new QuickLink("Docs", @"C:\Docs"), new QuickLink("Plan", @"C:\Docs\plan.pdf"));

        vm.Links[0].OpenCommand.Execute(null);
        vm.Links[1].OpenCommand.Execute(null);
        vm.Links[2].OpenCommand.Execute(null);

        Assert.Equal(new[] { "https://example.com", @"C:\Docs", @"C:\Docs\plan.pdf" }, opened.ToArray());
    }

    [Fact]
    public void Open_OfAPathThatIsGone_OpensNothing_AndSaysSo()
    {
        var (vm, _, opened) = Make(6, new QuickLink("Old", @"C:\Gone\file.txt"));

        vm.Links[0].OpenCommand.Execute(null);

        Assert.Empty(opened);
        Assert.Contains("doesn't exist", vm.OpenError);
    }

    [Fact]
    public void Open_NeverStartsAProgram_EvenIfTheStoredTextIsOne()
    {
        // A link stored by some other route must still be refused when it is opened.
        var repo = new FakeQuickLinkRepository();
        repo.Items.Add(new QuickLinkBypass().Make());
        var opened = new List<string>();
        var vm = new QuickLinksViewModel(repo, opened.Add, new FakeProbe(@"C:\Tools\setup.exe"));

        vm.Links[0].OpenCommand.Execute(null);

        Assert.Empty(opened);
        Assert.NotNull(vm.OpenError);
    }

    // Builds a link whose target the domain type would not accept today, the way an old or hand-edited row could.
    private sealed class QuickLinkBypass
    {
        public QuickLink Make()
        {
            var link = new QuickLink("Setup", @"C:\Tools\ok.txt");
            typeof(QuickLink).GetField("<Url>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(link, @"C:\Tools\setup.exe");
            return link;
        }
    }
}
