using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Models;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.Headless;

// The schedule drawn in each palette, and choosing a palette in the settings panel.
public class PaletteSettingHeadlessTests
{
    // Now is 09:30 (UiSnapshot.SampleNow): "Commute" is past, "Focus" is current.
    private static readonly ScheduleBlock[] Blocks =
    {
        new("Wake up", new TimeOnly(6, 30), new TimeOnly(7, 0), BlockColor.Amber),
        new("Breakfast", new TimeOnly(7, 0), new TimeOnly(8, 0), BlockColor.Rose),
        new("Commute", new TimeOnly(8, 0), new TimeOnly(9, 0), BlockColor.Gray),
        new("Focus", new TimeOnly(9, 0), new TimeOnly(10, 30), BlockColor.Slate),
        new("Email", new TimeOnly(10, 30), new TimeOnly(11, 30), BlockColor.Teal),
        new("Lunch", new TimeOnly(11, 30), new TimeOnly(12, 30), BlockColor.Terracotta),
        new("Workout", new TimeOnly(12, 30), new TimeOnly(13, 30), BlockColor.Sage),
        new("Read", new TimeOnly(13, 30), new TimeOnly(15, 0), BlockColor.Lavender),
    };

    public static TheoryData<string> PaletteIds => new(BlockPalettes.All.Select(p => p.Id));

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static MainWindow Show(string? palette)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 1000, blocks: Blocks, palette: palette);
        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 6 * 60);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border Block(Window window, string title) =>
        window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("gridBlock"))
            .Single(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Classes.Contains("blockTitle") && t.Text == title));

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color StripeOf(Border block) =>
        ColorOf(block.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("blockStripe")).Background);

    private static Color TitleOf(Border block) =>
        ColorOf(block.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("blockTitle")).Foreground);

    private static void Click(Window window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertDrawnIn(Window window, BlockPalette palette)
    {
        foreach (var block in Blocks)
        {
            var border = Block(window, block.Title);
            var swatch = palette.Get(block.Color);
            Assert.Equal(swatch.Fill, ColorOf(border.Background));
            Assert.Equal(swatch.Stripe, StripeOf(border));
            Assert.Equal(swatch.Text, TitleOf(border));
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(PaletteIds))]
    public void Schedule_IsDrawnInTheSavedPaletteWithCurrentOutlinedAndPastDimmed(string id)
    {
        var window = Show(id);

        AssertDrawnIn(window, BlockPalettes.Get(id));
        var current = Block(window, "Focus");
        Assert.Contains("current", current.Classes);
        Assert.Equal(ColorOf(Application.Current!.FindResource("CurrentBlockOutlineBrush") as IBrush), ColorOf(current.BorderBrush));
        Assert.True(Block(window, "Commute").Opacity < 1);
        UiSnapshot.Render(window, $"palette-{id}");
        window.Close();
    }

    [AvaloniaFact]
    public void SettingsPanel_GroupsSettingsAndShowsEveryPaletteWithEightColorPreviews()
    {
        var window = Show(null);
        Named<Button>(window, "SettingsButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var general = Named<Control>(window, "GeneralPage").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("CLOCK FORMAT", general);
        ((MainViewModel)window.DataContext!).Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        Dispatcher.UIThread.RunJobs();

        var items = Named<ListBox>(window, "PaletteList").GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Equal(new[] { "Default Minimal", "Default Saturated", "Pastel", "Earth", "Coastal", "Jewel" }, items.Select(i => ((PaletteOption)i.DataContext!).Name));
        Assert.All(items, item => Assert.Equal(8, item.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("paletteChip"))));
        Assert.True(items[0].IsSelected);
        UiSnapshot.Render(window, "settings-panel");
        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingSaturatedInSettings_RecolorsTheScheduleRightAway()
    {
        var window = Show(null);
        Named<Button>(window, "SettingsButton").Command!.Execute(null);
        ((MainViewModel)window.DataContext!).Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        Dispatcher.UIThread.RunJobs();
        AssertDrawnIn(window, BlockPalettes.Minimal);

        var saturated = Named<ListBox>(window, "PaletteList").GetVisualDescendants().OfType<ListBoxItem>()
            .Single(i => ((PaletteOption)i.DataContext!).Id == BlockPalettes.Saturated.Id);
        Click(window, saturated);

        Assert.True(saturated.IsSelected);
        AssertDrawnIn(window, BlockPalettes.Saturated);
        UiSnapshot.Render(window, "settings-panel-saturated");
        window.Close();
    }
}
