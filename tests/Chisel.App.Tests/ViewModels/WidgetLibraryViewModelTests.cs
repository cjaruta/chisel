using Chisel.App.ViewModels;

namespace Chisel.App.Tests.ViewModels;

public class WidgetLibraryViewModelTests
{
    private static (LayoutViewModel Layout, WidgetLibraryViewModel Library) Editing()
    {
        var layout = new LayoutViewModel();
        var library = new WidgetLibraryViewModel(layout);
        layout.EnterEditCommand.Execute(null);
        return (layout, library);
    }

    private static string[] Ids(IEnumerable<WidgetLibraryRow> rows) => rows.Select(r => r.Id).ToArray();

    [Fact]
    public void EveryRegisteredWidget_IsListedOnce_SortedByName_WithAnIconAndDescription()
    {
        var (_, library) = Editing();

        var rows = library.Available.Concat(library.OnDashboard).ToList();

        Assert.Equal(WidgetRegistry.AllTypes.Select(w => w.Id).Order(), rows.Select(r => r.Id).Order());
        Assert.Equal(new[] { "Calculator", "Calendar", "Clock", "Daily verse", "Media", "Picture frame", "Weather" }, library.Available.Select(r => r.Title));
        Assert.Equal(new[] { "Notes", "Tasks", "Today" }, library.OnDashboard.Select(r => r.Title));
        Assert.All(rows, r =>
        {
            Assert.NotEmpty(r.Icon);
            Assert.NotEmpty(r.Description);
        });
    }

    [Fact]
    public void Adding_MovesAHiddenWidgetToOnDashboard_AtOnce_AndKeepsTheLibraryOpen()
    {
        var (layout, library) = Editing();
        library.ToggleCommand.Execute(null);

        library.AddCommand.Execute(WidgetRegistry.Clock);

        Assert.True(library.IsOpen);
        Assert.Contains(WidgetRegistry.Clock, layout.Layout.WidgetIds);
        Assert.DoesNotContain(WidgetRegistry.Clock, Ids(library.Available));
        var row = Assert.Single(library.OnDashboard, r => r.Id == WidgetRegistry.Clock);
        Assert.Equal("On dashboard", row.Status);
        Assert.False(row.CanAdd);
    }

    [Fact]
    public void RemovingAWidget_MovesItBackToAvailable_ASoftHideNotADeletion()
    {
        var (layout, library) = Editing();

        layout.RemoveWidget("notes");

        Assert.Contains("notes", Ids(library.Available));
        Assert.DoesNotContain("notes", Ids(library.OnDashboard));
    }

    [Fact]
    public void ASingleInstanceWidget_CannotBeAddedTwice()
    {
        var (layout, library) = Editing();
        var before = layout.Layout;

        library.AddCommand.Execute("notes");

        Assert.Same(before, layout.Layout);
    }

    [Fact]
    public void AMultiInstanceWidget_StaysAvailable_AndCountsWhatIsPlaced()
    {
        var (layout, library) = Editing();
        Assert.Equal("", Assert.Single(library.Available, r => r.Id == WidgetRegistry.PictureFrame).Status);

        library.AddCommand.Execute(WidgetRegistry.PictureFrame);
        library.AddCommand.Execute(WidgetRegistry.PictureFrame);

        var row = Assert.Single(library.Available, r => r.Id == WidgetRegistry.PictureFrame);
        Assert.True(row.CanAdd);
        Assert.Equal("2 on dashboard", row.Status);
        Assert.Equal(2, layout.Layout.WidgetIds.Count(id => id.StartsWith(WidgetRegistry.PictureFrame)));
    }

    [Fact]
    public void Search_MatchesNameAndDescription_AcrossBothGroups_IgnoringCase()
    {
        var (_, library) = Editing();

        library.Query = "  CLOCK ";
        Assert.Contains("clock", Ids(library.Available));
        Assert.Contains(library.OnDashboard.Concat(library.Available), r => r.Description.Contains("clock", StringComparison.OrdinalIgnoreCase));

        library.Query = "task";
        Assert.Contains("tasks", Ids(library.OnDashboard));
        Assert.Empty(library.Available.Where(r => r.Id == "tasks"));

        library.Query = "no such widget";
        Assert.True(library.IsEmpty);
        Assert.False(library.HasResults);

        library.Query = "";
        Assert.False(library.IsEmpty);
    }

    [Fact]
    public void Closing_LeavesEditMode_Alone_ButLeavingEditMode_ClosesTheLibrary()
    {
        var (layout, library) = Editing();
        library.ToggleCommand.Execute(null);
        Assert.True(library.IsOpen);

        library.CloseCommand.Execute(null);
        Assert.False(library.IsOpen);
        Assert.True(layout.IsEditing);

        library.ToggleCommand.Execute(null);
        library.Query = "clock";
        layout.CancelCommand.Execute(null);
        Assert.False(library.IsOpen);
        Assert.Equal("", library.Query);

        library.ToggleCommand.Execute(null);
        Assert.False(library.IsOpen); // not editing: it cannot open
    }
}
