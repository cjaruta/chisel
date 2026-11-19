using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;
using Xunit;

namespace Chisel.App.Tests.Headless;

// Several instances of one widget type on the real window, using a test-only type that is not in the app's registry:
// each instance has its own view and state, however the cards are arranged, and goes away with the instance.
[Collection(MultiWidgetCollection.Name)]
public class WidgetInstanceHeadlessTests : IDisposable
{
    private const string A = "testframe:aaa111";
    private const string B = "testframe:bbb222";
    private const string C = "testframe:ccc333";

    private readonly IDisposable _registration = TestMultiWidget.Register();

    public void Dispose() => _registration.Dispose();

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static List<WidgetFrame> Frames(Window window) => Host(window).Frames.SelectMany(c => c).ToList();

    private static WidgetFrame Frame(Window window, string id) => Frames(window).Single(f => f.WidgetId == id);

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static TestFrameView ViewOf(Window window, string id)
    {
        var frame = Frames(window).Single(f => f.MemberIds.Contains(id));
        return (TestFrameView)frame.Views[frame.MemberIds.ToList().IndexOf(id)];
    }

    private static MainWindow Open(WidgetLayout layout)
    {
        var window = UiSnapshot.ShowMainWindow();
        Main(window).Layout.Apply(layout);
        Settle();
        return window;
    }

    private static WidgetLayout Dashboard(params WidgetPlacement[] widgets) =>
        new(new[] { new LayoutColumn(1, widgets) }, WidgetRegistry.DefaultLayout.HiddenWidgetIds);

    private static WidgetPlacement Group(GroupViewMode mode, params string[] ids) =>
        new(ids[0], 1, null, ids.Select(id => new GroupMember(id)).ToList(), 0, mode);

    [AvaloniaFact]
    public void TwoInstancesOnTheDashboard_HaveTheirOwnViewsTitlesAndState()
    {
        var window = Open(Dashboard(new WidgetPlacement("notes"), new WidgetPlacement(A), new WidgetPlacement(B)));

        var first = ViewOf(window, A);
        var second = ViewOf(window, B);
        Assert.NotSame(first, second);
        first.Draft.Text = "one";
        second.Draft.Text = "two";
        Main(window).Layout.EnterEditCommand.Execute(null);
        Main(window).Layout.ToggleHeader(A);
        Main(window).Layout.DoneCommand.Execute(null);
        Settle();

        Assert.Equal("one", ViewOf(window, A).Draft.Text);
        Assert.Equal("two", ViewOf(window, B).Draft.Text);
        Assert.Equal("Test frame", Main(window).Layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Id == A).Title);
        Assert.Equal("Test frame 2", Main(window).Layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Id == B).Title);
        window.Close();
    }

    [AvaloniaFact]
    public void TwoInstancesInATabGroup_KeepTheirOwnStateWhenTabsSwitch_AndOnlyTheActiveOneIsLive()
    {
        var window = Open(Dashboard(Group(GroupViewMode.Tabs, A, B), new WidgetPlacement("notes")));
        var frame = Frame(window, A);
        var first = ViewOf(window, A);
        var second = ViewOf(window, B);
        first.Draft.Text = "one";
        second.Draft.Text = "two";

        Assert.True(first.IsEffectivelyVisible);
        Assert.False(second.IsEffectivelyVisible);
        Assert.Equal("Test frame", ((TextBlock)frame.TabButton(A).Content!).Text);
        Assert.Equal("Test frame 2", ((TextBlock)frame.TabButton(B).Content!).Text);

        Main(window).Layout.SelectTab(B);
        Settle();

        Assert.False(first.IsEffectivelyVisible);
        Assert.True(second.IsEffectivelyVisible);
        Assert.Equal("one", first.Draft.Text);
        Assert.Equal("two", second.Draft.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void TwoInstancesInSplitPanes_ShowSideBySide_WithTheirOwnState()
    {
        var window = Open(Dashboard(Group(GroupViewMode.Split, A, B)));
        var frame = Frame(window, A);

        Assert.True(frame.IsSplitShown);
        var first = ViewOf(window, A);
        var second = ViewOf(window, B);
        first.Draft.Text = "left";
        second.Draft.Text = "right";

        Assert.True(first.IsEffectivelyVisible && second.IsEffectivelyVisible);
        Assert.True(frame.Pane(A).Bounds.Left < frame.Pane(B).Bounds.Left);
        Assert.Equal("left", first.Draft.Text);
        Assert.Equal("right", second.Draft.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void ADeletedInstance_ReleasesItsView_OnDone_AndTheOthersAreRenumbered()
    {
        var window = Open(Dashboard(new WidgetPlacement("notes"), new WidgetPlacement(A), new WidgetPlacement(B)));
        var layout = Main(window).Layout;
        var removed = ViewOf(window, A);
        var kept = ViewOf(window, B);
        Assert.Equal("Test frame 2", Frame(window, B).MemberTitles.Single());

        layout.EnterEditCommand.Execute(null);
        layout.RemoveWidget(A);
        Settle();
        layout.DoneCommand.Execute(null);
        Settle();

        Assert.True(removed.IsDisposed);
        Assert.False(kept.IsDisposed);
        Assert.DoesNotContain(Frames(window), f => f.WidgetId == A);
        Assert.Same(kept, ViewOf(window, B));
        Assert.Equal("Test frame", Frame(window, B).MemberTitles.Single());
        window.Close();
    }

    [AvaloniaFact]
    public void CancellingAnEdit_BringsBackARemovedInstance_WithItsView()
    {
        var window = Open(Dashboard(new WidgetPlacement("notes"), new WidgetPlacement(A), new WidgetPlacement(B)));
        var layout = Main(window).Layout;
        var view = ViewOf(window, A);
        view.Draft.Text = "kept";

        layout.EnterEditCommand.Execute(null);
        layout.RemoveWidget(A);
        Settle();
        Assert.False(view.IsDisposed);
        layout.CancelCommand.Execute(null);
        Settle();

        Assert.False(view.IsDisposed);
        Assert.Same(view, ViewOf(window, A));
        Assert.Equal("kept", ViewOf(window, A).Draft.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void AnInstanceTakenOutOfAGroupAndDeleted_LeavesTheOtherTabsAlone()
    {
        var window = Open(Dashboard(Group(GroupViewMode.Tabs, A, B, C), new WidgetPlacement("notes")));
        var layout = Main(window).Layout;
        var gone = ViewOf(window, B);

        layout.EnterEditCommand.Execute(null);
        layout.Unmerge(B);
        layout.RemoveWidget(B);
        layout.DoneCommand.Execute(null);
        Settle();

        var frame = Frame(window, A);
        Assert.Equal(new[] { A, C }, frame.MemberIds);
        Assert.True(gone.IsDisposed);
        Assert.Equal("Test frame 2", ((TextBlock)frame.TabButton(C).Content!).Text);
        window.Close();
    }
}
