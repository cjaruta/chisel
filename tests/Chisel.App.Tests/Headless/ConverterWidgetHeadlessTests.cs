using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Converter;

namespace Chisel.App.Tests.Headless;

public class ConverterWidgetHeadlessTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ConverterWidgetHeadlessTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static MainWindow Open(double width = 1200, double height = 760, double weight = 3, string? themeMode = null)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, themeMode: themeMode);
        var layout = Main(window).Layout;
        layout.Apply(layout.Layout.AddWidget(WidgetRegistry.Converter, 0, layout.Layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static ConverterWidgetView View(Window w) => w.GetVisualDescendants().OfType<ConverterWidgetView>().Single();

    private static T ByAutomation<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetName(c) == name);

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Registry_HasTheConverter_WithAnOfflineDescription()
    {
        var definition = WidgetRegistry.Get(WidgetRegistry.Converter)!;
        Assert.Equal("Converter", definition.Title);
        Assert.Equal(typeof(ConverterWidgetView), definition.ViewType);
        Assert.Contains("currency", definition.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WidgetRegistry.Converter, WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }

    [AvaloniaFact]
    public void Typing_ConvertsAtOnce_AndTheControlsHaveAutomationNames()
    {
        var window = Open();
        var input = ByAutomation<TextBox>(window, "From value");
        input.Text = "100";
        Settle();

        Assert.Equal("328.0839895", ByAutomation<TextBox>(window, "To value").Text);
        Assert.NotNull(ByAutomation<ComboBox>(window, "From unit"));
        Assert.NotNull(ByAutomation<ComboBox>(window, "To unit"));
        Assert.NotNull(ByAutomation<Button>(window, "Swap units"));
        Assert.NotNull(ByAutomation<Button>(window, "Copy result"));
        window.Close();
    }

    [AvaloniaFact]
    public void PickingAUnit_Converts()
    {
        var window = Open();
        ByAutomation<TextBox>(window, "From value").Text = "2";
        var to = ByAutomation<ComboBox>(window, "To unit");
        to.SelectedIndex = Main(window).Converter.Category.Units.ToList().FindIndex(u => u.Id == "cm");
        Settle();
        Assert.Equal("cm", Main(window).Converter.To.Id);
        Assert.Equal("200", ByAutomation<TextBox>(window, "To value").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlShiftS_Swaps_AndSwapButtonToo()
    {
        var window = Open();
        var input = ByAutomation<TextBox>(window, "From value");
        input.Focus();
        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        Settle();
        Assert.Equal(("ft", "m"), (Main(window).Converter.From.Id, Main(window).Converter.To.Id));

        ByAutomation<Button>(window, "Swap units").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        Assert.Equal(("m", "ft"), (Main(window).Converter.From.Id, Main(window).Converter.To.Id));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Enter_CopiesThePlainNumber()
    {
        var window = Open();
        var input = ByAutomation<TextBox>(window, "From value");
        input.Text = "1000";
        input.Focus();
        Settle();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
        Assert.Equal("3280.839895", await window.Clipboard!.TryGetTextAsync());
        window.Close();
    }

    [AvaloniaFact]
    public void Tabs_SwitchTheCategory()
    {
        var window = Open();
        var tab = window.GetVisualDescendants().OfType<ToggleButton>().First(t => AutomationProperties.GetName(t) == "Temperature");
        tab.IsChecked = true;
        Settle();
        Assert.Equal("temperature", Main(window).Converter.Category.Id);
        Assert.True(tab.IsChecked);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("converter-compact", 260, 110, ConverterDetail.Compact)]
    [InlineData("converter-standard", 300, 220, ConverterDetail.Standard)]
    [InlineData("converter-full", 1200, 760, ConverterDetail.Full)]
    public void Sizes_ShowTheRightDetail_WithNothingClipped(string name, double columnWidth, double height, ConverterDetail detail)
    {
        var window = Open();
        var view = View(window);
        // Put the converter alone in a column of the given width (the rest of the window holds the other widgets).
        var layout = Main(window).Layout;
        var single = new Chisel.Core.Layout.WidgetLayout(new[]
        {
            new Chisel.Core.Layout.LayoutColumn(columnWidth, new[] { new Chisel.Core.Layout.WidgetPlacement(WidgetRegistry.Converter, 1) }),
            new Chisel.Core.Layout.LayoutColumn(1200 - columnWidth, new[] { new Chisel.Core.Layout.WidgetPlacement(WidgetRegistry.Schedule, 1) }),
        }, hiddenWidgetIds: WidgetRegistry.All.Select(w => w.Id).Where(id => id != WidgetRegistry.Converter && id != WidgetRegistry.Schedule));
        layout.Apply(single);
        window.Height = height + 120;
        Settle();
        view = View(window);

        Assert.Equal(detail, view.Layout.Detail);
        Assert.Equal(detail >= ConverterDetail.Standard, ByAutomation<Button>(window, "Swap units").IsEffectivelyVisible);
        Assert.Equal(detail == ConverterDetail.Full, window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "ConverterCommon" && c.IsEffectivelyVisible));
        foreach (var control in view.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Name?.StartsWith("Converter") == true))
        {
            var topLeft = control.TranslatePoint(default, view)!.Value;
            Assert.True(topLeft.Y + control.Bounds.Height <= view.Bounds.Height + 0.5, $"{control.Name} runs past the bottom");
            Assert.True(topLeft.X + control.Bounds.Width <= view.Bounds.Width + 0.5, $"{control.Name} runs past the right edge");
        }
        UiSnapshot.Render(window, name);
        window.Close();
    }

    [AvaloniaFact]
    public void Render_LightTheme()
    {
        var window = Open(themeMode: "light");
        ByAutomation<TextBox>(window, "From value").Text = "12.5";
        Settle();
        UiSnapshot.Render(window, "converter-light");
        window.Close();
    }
}
