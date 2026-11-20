using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Calculator;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.Headless;

public class CalculatorWidgetHeadlessTests
{
    // The window with only the calculator on the dashboard, so its size is the window's.
    private static (MainWindow Window, MainViewModel Main) Open(double width, double height, bool advanced = false)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        var main = (MainViewModel)window.DataContext!;
        main.CalculatorSettings.Advanced = advanced;
        var others = WidgetRegistry.All.Where(w => w.Id != WidgetRegistry.Calculator && !w.AllowMultiple).Select(w => w.Id).ToArray();
        main.Layout.Apply(new WidgetLayout(new[] { new LayoutColumn(1, new[] { new WidgetPlacement(WidgetRegistry.Calculator, 1) }) }, others));
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static CalculatorWidgetView View(Window window) => window.GetVisualDescendants().OfType<CalculatorWidgetView>().Single();

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<Button> Keys(Window window) =>
        View(window).GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Key_") == true).ToList();

    private static string Display(Window window) => Named<TextBlock>(window, "CalculatorDisplay").Text!;

    private static void ClickKey(Window window, CalcKey key)
    {
        var button = Named<Button>(window, "Key_" + key);
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Calculator_IsInTheTray_CanBeAdded_AndDoesNotOpenTheTrayByItself()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.CustomizeLayoutCommand.Execute(null);
        Settle();

        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == WidgetRegistry.Calculator);
        Assert.Empty(window.GetVisualDescendants().OfType<CalculatorWidgetView>());

        main.Layout.AddWidgetFromTray(WidgetRegistry.Calculator);
        Settle();
        Assert.Single(window.GetVisualDescendants().OfType<CalculatorWidgetView>());

        main.Layout.RemoveWidget(WidgetRegistry.Calculator);
        Settle();
        Assert.Empty(window.GetVisualDescendants().OfType<CalculatorWidgetView>());
        window.Close();
    }

    [AvaloniaFact]
    public void ClickingKeys_Calculates()
    {
        var (window, main) = Open(320, 760);
        foreach (var key in new[] { CalcKey.Digit1, CalcKey.Digit2, CalcKey.Add, CalcKey.Digit3, CalcKey.Equals })
        {
            ClickKey(window, key);
        }

        Assert.Equal("15", Display(window));
        Assert.Equal("15", main.Calculator.Display);
        window.Close();
    }

    [AvaloniaFact]
    public void DivideByZero_ShowsAShortError_AndTheNextKeyRecovers()
    {
        var (window, _) = Open(320, 760);
        foreach (var key in new[] { CalcKey.Digit6, CalcKey.Divide, CalcKey.Digit0, CalcKey.Equals })
        {
            ClickKey(window, key);
        }
        Assert.Equal("Error", Display(window));

        ClickKey(window, CalcKey.Digit4);
        Assert.Equal("4", Display(window));
        window.Close();
    }

    [AvaloniaFact]
    public void TheKeys_AreInertInEditMode_AndWorkAgainAfterwards()
    {
        var (window, main) = Open(320, 760);
        main.CustomizeLayoutCommand.Execute(null);
        Settle();
        ClickKey(window, CalcKey.Digit5);
        Assert.Equal("0", Display(window));

        main.Layout.DoneCommand.Execute(null);
        Settle();
        ClickKey(window, CalcKey.Digit5);
        Assert.Equal("5", Display(window));
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_TypesDigitsOperators_EnterEqualsBackspaceAndEscape()
    {
        var (window, _) = Open(320, 760);
        View(window).Focus();
        Settle();

        window.KeyTextInput("12");
        window.KeyTextInput("+");
        window.KeyTextInput("3");
        Assert.Equal("3", Display(window));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
        Assert.Equal("15", Display(window));

        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        window.KeyTextInput("7");
        window.KeyTextInput("8");
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Settle();
        Assert.Equal("7", Display(window));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Settle();
        Assert.Equal("0", Display(window));
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_IsIgnoredInEditMode()
    {
        var (window, main) = Open(320, 760);
        View(window).Focus();
        main.CustomizeLayoutCommand.Execute(null);
        Settle();
        window.KeyTextInput("9");
        Settle();
        Assert.Equal("0", main.Calculator.Display);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CtrlC_CopiesTheResult()
    {
        var (window, _) = Open(320, 760);
        View(window).Focus();
        window.KeyTextInput("0.1");
        window.KeyTextInput("+");
        window.KeyTextInput("0.2");
        window.KeyTextInput("=");
        Settle();

        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
        Settle();
        var copied = await window.Clipboard!.TryGetTextAsync();
        Assert.Equal("0.3", copied);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("calculator-narrow-tall", 300, 760, false, false)]
    [InlineData("calculator-normal", 460, 760, false, false)]
    [InlineData("calculator-wide", 1000, 620, false, false)]
    [InlineData("calculator-narrow-advanced-falls-back", 300, 760, true, false)]
    [InlineData("calculator-normal-advanced-falls-back", 460, 760, true, false)]
    [InlineData("calculator-wide-advanced", 1000, 620, true, true)]
    [InlineData("calculator-advanced-compact", 620, 520, true, true)]
    public void Render_ShowsTheRightKeypad_WithNothingClippedOrSquished(string name, double width, double height, bool advanced, bool panel)
    {
        var (window, main) = Open(width, height, advanced);
        var view = View(window);

        Assert.Equal(panel, main.Calculator.ShowAdvanced);
        Assert.Equal(panel, window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "CalculatorAdvancedPanel" && c.IsEffectivelyVisible));
        Assert.Equal(panel ? 22 + 20 : 20, Keys(window).Count);

        var layout = main.Calculator.Layout;
        foreach (var key in Keys(window))
        {
            var topLeft = key.TranslatePoint(default, view)!.Value;
            Assert.True(topLeft.X >= -0.5 && topLeft.Y >= -0.5, $"{key.Name} starts outside the widget");
            Assert.True(topLeft.X + key.Bounds.Width <= view.Bounds.Width + 0.5, $"{key.Name} runs past the right edge");
            Assert.True(topLeft.Y + key.Bounds.Height <= view.Bounds.Height + 0.5, $"{key.Name} runs past the bottom");
            Assert.True(key.Bounds.Width >= 40 && key.Bounds.Height >= 26, $"{key.Name} is squished: {key.Bounds}");

            var text = key.GetVisualDescendants().OfType<TextBlock>().Single();
            text.Measure(Size.Infinity);
            Assert.True(text.DesiredSize.Width <= key.Bounds.Width - 4, $"{key.Name}: its text ({text.DesiredSize.Width}) is wider than the key ({key.Bounds.Width})");
            Assert.True(text.DesiredSize.Height <= key.Bounds.Height - 4, $"{key.Name}: its text is taller than the key");
        }

        UiSnapshot.Render(window, name);
        window.Close();
    }

    [AvaloniaFact]
    public void TurningAdvancedOnAndOff_ShowsAndHidesThePanel_WithoutLosingTheNumber()
    {
        var (window, main) = Open(1000, 620);
        ClickKey(window, CalcKey.Digit4);
        ClickKey(window, CalcKey.Digit2);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "CalculatorAdvancedPanel");

        main.CalculatorSettings.Advanced = true;
        Settle();
        Assert.Contains(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "CalculatorAdvancedPanel");
        Assert.Equal("42", Display(window));

        ClickKey(window, CalcKey.Clear);
        ClickKey(window, CalcKey.Sqrt);
        ClickKey(window, CalcKey.Digit1);
        ClickKey(window, CalcKey.Digit6);
        ClickKey(window, CalcKey.Equals);
        Assert.Equal("4", Display(window));

        main.CalculatorSettings.Advanced = false;
        Settle();
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "CalculatorAdvancedPanel");
        Assert.Equal("4", Display(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ResizingTheWindow_SwitchesBetweenThePanelAndTheBasicKeypad()
    {
        var (window, main) = Open(1000, 620, advanced: true);
        Assert.True(main.Calculator.ShowAdvanced);

        window.Width = 420;
        Settle();
        Settle();
        Assert.False(main.Calculator.ShowAdvanced);
        Assert.Equal(20, Keys(window).Count);

        window.Width = 1000;
        Settle();
        Settle();
        Assert.True(main.Calculator.ShowAdvanced);
        window.Close();
    }

    [AvaloniaFact]
    public void AdvancedKeys_ShowTheAngleMode_AndTheMemoryMark()
    {
        var (window, main) = Open(1000, 620, advanced: true);
        Assert.Equal("DEG", Named<TextBlock>(window, "CalculatorAngle").Text);
        Assert.False(Named<TextBlock>(window, "CalculatorMemory").IsVisible);

        ClickKey(window, CalcKey.ToggleAngle);
        Assert.Equal("RAD", Named<TextBlock>(window, "CalculatorAngle").Text);

        ClickKey(window, CalcKey.Digit5);
        ClickKey(window, CalcKey.MemoryAdd);
        Assert.True(Named<TextBlock>(window, "CalculatorMemory").IsVisible);
        Assert.True(main.Calculator.HasMemory);
        window.Close();
    }

    [AvaloniaFact]
    public void LongNumbers_ShrinkTheDisplayToFit()
    {
        var (window, main) = Open(300, 760);
        var text = Named<TextBlock>(window, "CalculatorDisplay");
        var full = main.Calculator.Layout.ResultFont;
        Assert.Equal(full, text.FontSize);

        foreach (var c in "123456789012345")
        {
            main.Calculator.PressChar(c);
        }
        Settle();
        Assert.Equal("123456789012345", text.Text);
        Assert.True(text.FontSize < full, $"{text.FontSize} vs {full}");
        var host = (Control)text.Parent!;
        Assert.True(text.Bounds.Width <= host.Bounds.Width + 0.5);
        UiSnapshot.Render(window, "calculator-long-number");
        window.Close();
    }

    [AvaloniaFact]
    public void ALongExpression_KeepsTheEndInView()
    {
        var (window, main) = Open(1000, 620, advanced: true);
        foreach (var c in "123456789+987654321*123456789-987654321+111111111")
        {
            main.Calculator.PressChar(c);
        }
        Settle();
        var text = Named<TextBlock>(window, "CalculatorDisplay");
        var host = (Control)text.Parent!;
        Assert.True(text.Bounds.Width <= host.Bounds.Width + 0.5);
        Assert.EndsWith("111111111", text.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void HidingTheHeader_RemovesTheWholeRow_AndTheKeypadTakesTheSpace()
    {
        var (window, main) = Open(320, 760);
        var frame = window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).Single(f => f.WidgetId == WidgetRegistry.Calculator);
        Assert.True(frame.ShowHeader);
        var withHeader = View(window).Bounds.Height;

        main.CustomizeLayoutCommand.Execute(null);
        main.Layout.ToggleHeader(WidgetRegistry.Calculator);
        main.Layout.DoneCommand.Execute(null);
        Settle();

        Assert.False(frame.ShowHeader);
        Assert.Null(frame.Header.GetVisualParent());
        Assert.DoesNotContain(frame.GetVisualDescendants().OfType<Control>(), c => c.Classes.Contains("widgetHeader") || c.Classes.Contains("widgetHeading"));
        Assert.True(View(window).Bounds.Height > withHeader);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingsPage_HasTheAdvancedToggleAndTheAngleChoice()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "calculator");
        Settle();

        var advanced = Named<CheckBox>(window, "CalculatorAdvancedToggle");
        Assert.False(advanced.IsChecked);
        advanced.IsChecked = true;
        Settle();
        Assert.True(main.CalculatorSettings.Advanced);

        Named<RadioButton>(window, "CalculatorRadians").IsChecked = true;
        Settle();
        Assert.Equal(AngleMode.Radians, main.CalculatorSettings.Angle);
        UiSnapshot.Render(window, "settings-calculator");
        window.Close();
    }
}
