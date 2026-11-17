using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Column widths follow the same working-copy rules as everything else in edit mode:
// Done saves them, Cancel puts the starting widths back, Reset restores the default widths.
public class LayoutColumnResizeTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    private readonly FakeLayoutRepository _repository = new();

    private static double[] Widths(WidgetLayout layout) => layout.Columns.Select(c => c.WidthWeight).ToArray();

    private static readonly double[] DefaultWidths = Widths(WidgetRegistry.DefaultLayout);

    private LayoutViewModel ResizedWhileEditing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.BeginResize();
        layout.PreviewColumnResize(0, 0.1, 0.1);
        layout.CommitResize();
        return layout;
    }

    [Fact]
    public void PreviewColumnResize_MovesWidthBetweenNeighbours_FromTheStartingWidths()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.BeginResize();

        layout.PreviewColumnResize(0, 0.05, 0.1);
        layout.PreviewColumnResize(0, 0.1, 0.1);

        var total = DefaultWidths.Sum();
        var widths = Widths(layout.Layout);
        Assert.Equal(DefaultWidths[0] + 0.1 * total, widths[0], 9);
        Assert.Equal(DefaultWidths[1] - 0.1 * total, widths[1], 9);
    }

    [Fact]
    public void Done_SavesTheNewWidths()
    {
        var layout = ResizedWhileEditing();
        Assert.Equal(DefaultWidths, Widths(_repository.Saved!));

        layout.DoneCommand.Execute(null);

        Assert.NotEqual(DefaultWidths, Widths(_repository.Saved!));
        Assert.Equal(Widths(layout.Layout), Widths(_repository.Saved!));
    }

    [Fact]
    public void Cancel_RestoresTheStartingWidths()
    {
        var layout = ResizedWhileEditing();

        layout.CancelCommand.Execute(null);

        Assert.Equal(DefaultWidths, Widths(layout.Layout));
        Assert.Equal(DefaultWidths, Widths(_repository.Saved!));
    }

    [Fact]
    public void Reset_RestoresTheDefaultWidths_AndIsOnlySavedOnDone()
    {
        var layout = ResizedWhileEditing();
        layout.DoneCommand.Execute(null);
        layout.EnterEditCommand.Execute(null);

        layout.ResetCommand.Execute(null);

        Assert.Equal(DefaultWidths, Widths(layout.Layout));
        Assert.NotEqual(DefaultWidths, Widths(_repository.Saved!));
        layout.DoneCommand.Execute(null);
        Assert.Equal(DefaultWidths, Widths(_repository.Saved!));
    }
}
