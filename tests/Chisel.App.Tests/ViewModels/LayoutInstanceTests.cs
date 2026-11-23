using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

[Collection(MultiWidgetCollection.Name)]
public class LayoutInstanceTests : IDisposable
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; } = TestLayouts.Classic;
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    private readonly IDisposable _registration = TestMultiWidget.Register();
    private readonly FakeLayoutRepository _repository = new() { Saved = TestLayouts.Classic };

    public void Dispose() => _registration.Dispose();

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    private static string[] Instances(LayoutViewModel layout) =>
        layout.Layout.WidgetIds.Where(id => id.StartsWith(TestMultiWidget.TypeId)).ToArray();

    private static string[] Titles(LayoutViewModel layout) =>
        layout.Columns.SelectMany(c => c.Widgets).SelectMany(w => w.Members)
            .Where(m => m.TypeId == TestMultiWidget.TypeId).Select(m => m.Title).ToArray();

    [Fact]
    public void Registry_ResolvesAnInstanceId_ToItsTypesDefinition()
    {
        var definition = WidgetRegistry.Get(TestMultiWidget.Id("k1"))!;

        Assert.Equal(TestMultiWidget.Id("k1"), definition.Id);
        Assert.Equal(TestMultiWidget.TypeId, definition.TypeId);
        Assert.Equal(typeof(TestFrameView), definition.ViewType);
        Assert.Equal("clock", WidgetRegistry.Get("clock")!.Id);
        Assert.Null(WidgetRegistry.Get("clock:k1"));
        Assert.Null(WidgetRegistry.Get("nothing:k1"));
        Assert.All(WidgetRegistry.All.Where(w => w.Id != WidgetRegistry.PictureFrame), w => Assert.False(w.AllowMultiple));
    }

    [Fact]
    public void Tray_AlwaysOffersAMultiInstanceType_AndOnlyHiddenSingleInstanceWidgets()
    {
        var layout = Editing();
        Assert.Contains(layout.TrayWidgets, w => w.Id == TestMultiWidget.TypeId);
        Assert.True(layout.HasHiddenWidgets);

        layout.AddWidgetFromTray(TestMultiWidget.TypeId);

        Assert.Contains(layout.TrayWidgets, w => w.Id == TestMultiWidget.TypeId);
        Assert.Single(layout.TrayWidgets, w => w.Id == "clock");
        Assert.DoesNotContain(layout.TrayWidgets, w => w.Id == "notes");
    }

    [Fact]
    public void Add_CreatesANewInstanceEachTime_WithNumberedTitles()
    {
        var layout = Editing();

        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        Assert.Equal(new[] { "Test frame" }, Titles(layout));
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);

        Assert.Equal(3, Instances(layout).Distinct().Count());
        Assert.Equal(new[] { "Test frame", "Test frame 2", "Test frame 3" }, Titles(layout));
        Assert.All(Instances(layout), id => Assert.True(InstanceIds.IsInstance(id)));
    }

    [Fact]
    public void Dragging_FromTheTray_CreatesAFreshInstance_OnlyWhenDropped()
    {
        var layout = Editing();

        layout.BeginTrayDrag(TestMultiWidget.TypeId);
        Assert.True(layout.IsDraggingFromTray);
        Assert.True(InstanceIds.IsInstance(layout.DraggedWidgetId!));
        layout.CancelWidgetDrag();
        Assert.Empty(Instances(layout));

        layout.BeginTrayDrag(TestMultiWidget.TypeId);
        var dragged = layout.DraggedWidgetId;
        Assert.True(layout.DropWidget(1, 0));
        Assert.Equal(new[] { dragged }, Instances(layout));
    }

    [Fact]
    public void Remove_DeletesAnInstance_WithoutAHiddenEntry()
    {
        var layout = Editing();
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        var first = Instances(layout)[0];

        layout.RemoveWidget(first);

        Assert.Single(Instances(layout));
        Assert.DoesNotContain(first, layout.Layout.HiddenWidgetIds);
        Assert.Equal(new[] { "Test frame" }, Titles(layout));
    }

    [Fact]
    public void Remove_OfASingleInstanceWidget_StillHidesIt()
    {
        var layout = Editing();

        layout.RemoveWidget("notes");

        Assert.Contains("notes", layout.Layout.HiddenWidgetIds);
        Assert.Contains(layout.TrayWidgets, w => w.Id == "notes");
    }

    [Fact]
    public void Cancel_RestoresRemovedInstances_AndDropsAddedOnes()
    {
        var layout = Editing();
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.DoneCommand.Execute(null);
        var kept = Instances(layout).Single();

        layout.EnterEditCommand.Execute(null);
        layout.RemoveWidget(kept);
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.CancelCommand.Execute(null);

        Assert.Equal(new[] { kept }, Instances(layout));
    }

    [Fact]
    public void Reset_ReturnsToTheDefaultLayout_AndDoneSavesIt()
    {
        var layout = Editing();
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);

        layout.ResetCommand.Execute(null);
        Assert.Empty(Instances(layout));
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());

        layout.DoneCommand.Execute(null);
        Assert.Empty(_repository.Saved!.WidgetIds.Where(id => id.StartsWith(TestMultiWidget.TypeId)));
    }

    [Fact]
    public void Done_SavesInstances_AndTheNextLoadKeepsThemWithTheirTitles()
    {
        var layout = Editing();
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.DoneCommand.Execute(null);

        var reloaded = new LayoutViewModel(_repository);

        Assert.Equal(Instances(layout), Instances(reloaded));
        Assert.Equal(new[] { "Test frame", "Test frame 2" }, Titles(reloaded));
        Assert.Equal(TestLayouts.Classic.HiddenWidgetIds, reloaded.Layout.HiddenWidgetIds);
    }

    [Fact]
    public void Load_DropsInstancesOfAnUnknownType_AndAKeyedSingleInstanceId()
    {
        var extra = new LayoutColumn(1, new[]
        {
            new WidgetPlacement("gone:k1"), new WidgetPlacement("notes:k1"), new WidgetPlacement(TestMultiWidget.Id("ok")),
        });
        _repository.Saved = new WidgetLayout(TestLayouts.Classic.Columns.Append(extra), TestLayouts.Classic.HiddenWidgetIds);

        var layout = new LayoutViewModel(_repository);

        Assert.Equal(new[] { TestMultiWidget.Id("ok") }, Instances(layout));
        Assert.DoesNotContain("gone:k1", layout.Layout.WidgetIds);
        Assert.DoesNotContain("notes:k1", layout.Layout.WidgetIds);
    }

    [Fact]
    public void SeveralInstances_CanShareATabGroup_WithNumberedTabs()
    {
        var layout = Editing();
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        layout.AddWidgetFromTray(TestMultiWidget.TypeId);
        var ids = Instances(layout);

        layout.BeginWidgetDrag(ids[1]);
        Assert.True(layout.DropOnWidget(ids[0]));

        var slot = layout.Columns.SelectMany(c => c.Widgets).Single(w => w.IsGroup);
        Assert.Equal(ids, slot.Members.Select(m => m.Id));
        Assert.Equal(new[] { "Test frame", "Test frame 2" }, slot.Members.Select(m => m.Title));
    }
}
