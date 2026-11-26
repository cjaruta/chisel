using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Up to three saved layouts: Save Profile keeps the arrangement in a slot, Load puts it back (as an edit that Cancel undoes).
public class LayoutProfileTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; } = TestLayouts.Classic;
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    private sealed class FakeProfileRepository : ILayoutProfileRepository
    {
        public Dictionary<int, LayoutProfile> Rows { get; } = new();
        public IReadOnlyList<LayoutProfile> GetAll() => Rows.Values.OrderBy(p => p.Slot).ToList();
        public void Save(LayoutProfile profile) => Rows[profile.Slot] = profile;
        public void Delete(int slot) => Rows.Remove(slot);
    }

    private readonly FakeProfileRepository _profiles = new();

    private LayoutViewModel Create()
    {
        var layout = new LayoutViewModel(new FakeLayoutRepository(), null, _profiles);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    private static string Order(WidgetLayout layout) => layout.Describe();

    [Fact]
    public void ThereAreThreeEmptySlots_ToStartWith()
    {
        var layout = Create();

        Assert.Equal(new[] { 1, 2, 3 }, layout.Profiles.Select(p => p.Slot));
        Assert.All(layout.Profiles, p => Assert.False(p.IsSaved));
        Assert.All(layout.Profiles, p => Assert.False(p.LoadCommand.CanExecute(null)));
    }

    [Fact]
    public void SavingAProfile_KeepsTheCurrentArrangement_AndLoadingBringsItBack()
    {
        var layout = Create();
        layout.Apply(WidgetRegistry.DefaultLayout);
        var arranged = Order(layout.Layout);

        layout.Profiles[1].SaveCommand.Execute(null);
        layout.Apply(TestLayouts.Classic);
        Assert.NotEqual(arranged, Order(layout.Layout));
        layout.Profiles[1].LoadCommand.Execute(null);

        Assert.True(layout.Profiles[1].IsSaved);
        Assert.Equal("Profile 2", layout.Profiles[1].Title);
        Assert.Equal(arranged, Order(layout.Layout));
        Assert.Equal(arranged, Order(_profiles.Rows[2].Layout));
    }

    [Fact]
    public void SavingAgain_ReplacesWhatWasInTheSlot()
    {
        var layout = Create();
        layout.Profiles[0].SaveCommand.Execute(null);
        var first = Order(layout.Layout);
        layout.Apply(WidgetRegistry.DefaultLayout);
        var second = Order(layout.Layout);
        Assert.NotEqual(first, second);

        layout.Profiles[0].SaveCommand.Execute(null);

        Assert.Equal(second, Order(_profiles.Rows[1].Layout));
        Assert.Single(_profiles.Rows);
    }

    [Fact]
    public void LoadingAProfile_IsAnEditThatCancelUndoes()
    {
        var layout = Create();
        var before = Order(layout.Layout);
        layout.Apply(WidgetRegistry.DefaultLayout);
        layout.Profiles[0].SaveCommand.Execute(null);
        layout.Apply(TestLayouts.Classic);

        layout.Profiles[0].LoadCommand.Execute(null);
        layout.CancelCommand.Execute(null);

        Assert.Equal(before, Order(layout.Layout));
    }

    [Fact]
    public void DeletingAProfile_EmptiesItsSlot()
    {
        var layout = Create();
        layout.Profiles[2].SaveCommand.Execute(null);

        layout.Profiles[2].DeleteCommand.Execute(null);

        Assert.False(layout.Profiles[2].IsSaved);
        Assert.Empty(_profiles.Rows);
    }

    [Fact]
    public void SavedProfiles_AreThereAtTheNextStart()
    {
        var layout = Create();
        layout.Apply(WidgetRegistry.DefaultLayout);
        layout.Profiles[0].SaveCommand.Execute(null);
        var saved = Order(layout.Layout);

        var next = new LayoutViewModel(new FakeLayoutRepository(), null, _profiles);
        next.EnterEditCommand.Execute(null);
        next.Profiles[0].LoadCommand.Execute(null);

        Assert.True(next.Profiles[0].IsSaved);
        Assert.False(next.Profiles[1].IsSaved);
        Assert.Equal(saved, Order(next.Layout));
    }

    [Fact]
    public void OutsideEditMode_NothingIsSaved()
    {
        var layout = new LayoutViewModel(new FakeLayoutRepository(), null, _profiles);

        layout.Profiles[0].SaveCommand.Execute(null);

        Assert.Empty(_profiles.Rows);
        Assert.False(layout.Profiles[0].IsSaved);
    }
}
