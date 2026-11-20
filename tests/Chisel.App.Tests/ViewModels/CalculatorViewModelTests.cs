using System.Globalization;
using Chisel.App.ViewModels;
using Chisel.Core.Calculator;

namespace Chisel.App.Tests.ViewModels;

public class CalculatorViewModelTests
{
    private sealed class FakeRepository(CalculatorSettings? initial = null) : ICalculatorSettingsRepository
    {
        public CalculatorSettings Saved { get; private set; } = initial ?? CalculatorSettings.Default;
        public int Saves { get; private set; }
        public CalculatorSettings Get() => Saved;

        public void Save(CalculatorSettings settings)
        {
            Saved = settings;
            Saves++;
        }
    }

    private static CalculatorViewModel Create(CalculatorSettings? settings = null, FakeRepository? repository = null)
    {
        repository ??= new FakeRepository(settings);
        return new CalculatorViewModel(new CalculatorSettingsViewModel(repository), CultureInfo.InvariantCulture);
    }

    private static void Type(CalculatorViewModel vm, string chars)
    {
        foreach (var c in chars)
        {
            Assert.True(vm.PressChar(c), $"'{c}' was not handled");
        }
    }

    [Fact]
    public void KeySequences_ShowTheirResult_AndKeepTheRunningLine()
    {
        var vm = Create();
        Assert.Equal("0", vm.Display);
        Type(vm, "12+");
        Assert.Equal("12 +", vm.Secondary);
        Type(vm, "30=");
        Assert.Equal("42", vm.Display);
        Assert.Equal("", vm.Secondary);
    }

    [Fact]
    public void PressCommand_PressesAKey()
    {
        var vm = Create();
        vm.PressCommand.Execute(CalcKey.Digit7);
        vm.PressCommand.Execute(CalcKey.Negate);
        Assert.Equal("-7", vm.Display);
    }

    [Fact]
    public void Display_RaisesChangeNotifications()
    {
        var vm = Create();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.PressCommand.Execute(CalcKey.Digit5);
        Assert.Contains(nameof(CalculatorViewModel.Display), changed);
        Assert.Contains(nameof(CalculatorViewModel.Result), changed);
    }

    [Fact]
    public void DivideByZero_ShowsErrorAndRecovers()
    {
        var vm = Create();
        Type(vm, "6/0=");
        Assert.True(vm.IsError);
        Assert.Equal("Error", vm.Display);
        Type(vm, "4");
        Assert.False(vm.IsError);
        Assert.Equal("4", vm.Display);
    }

    [Fact]
    public void RepeatedEquals_RepeatsTheLastOperation()
    {
        var vm = Create();
        Type(vm, "5+3===");
        Assert.Equal("14", vm.Display);
    }

    [Theory]
    [InlineData('1', CalcKey.Digit1)]
    [InlineData('0', CalcKey.Digit0)]
    [InlineData('.', CalcKey.Decimal)]
    [InlineData(',', CalcKey.Decimal)]
    [InlineData('+', CalcKey.Add)]
    [InlineData('-', CalcKey.Subtract)]
    [InlineData('*', CalcKey.Multiply)]
    [InlineData('x', CalcKey.Multiply)]
    [InlineData('/', CalcKey.Divide)]
    [InlineData('=', CalcKey.Equals)]
    [InlineData('%', CalcKey.Percent)]
    [InlineData('(', CalcKey.OpenParen)]
    [InlineData(')', CalcKey.CloseParen)]
    [InlineData('^', CalcKey.Power)]
    [InlineData('!', CalcKey.Factorial)]
    public void Characters_MapToKeys(char c, CalcKey key) => Assert.Equal(key, CalculatorKeys.FromChar(c));

    [Fact]
    public void UnknownCharacters_AreNotHandled()
    {
        var vm = Create();
        Assert.False(vm.PressChar('q'));
        Assert.False(vm.PressChar(' '));
        Assert.Equal("0", vm.Display);
    }

    [Fact]
    public void Enter_BackspaceAndEscape_AreKeys()
    {
        Assert.Equal(CalcKey.Equals, CalculatorKeys.Enter);
        Assert.Equal(CalcKey.Backspace, CalculatorKeys.Back);
        Assert.Equal(CalcKey.Clear, CalculatorKeys.Escape);
    }

    [Fact]
    public void Result_IsTheNumberToCopy()
    {
        var vm = Create();
        Type(vm, "0.1+0.2=");
        Assert.Equal("0.3", vm.Result);
    }

    [Fact]
    public void TheDecimalSeparator_FollowsTheCulture()
    {
        var vm = new CalculatorViewModel(new CalculatorSettingsViewModel(new FakeRepository()), CultureInfo.GetCultureInfo("de-DE"));
        Type(vm, "0.1+0.2=");
        Assert.Equal("0,3", vm.Display);
        Assert.Equal("0,3", vm.Result);
    }

    [Fact]
    public void AdvancedKeys_ShowOnlyWhenTheSettingIsOn_AndTheWidgetIsWideEnough()
    {
        var vm = Create(new CalculatorSettings(Advanced: true));
        vm.Resize(300, 480);
        Assert.False(vm.ShowAdvanced);
        vm.Resize(700, 420);
        Assert.True(vm.ShowAdvanced);

        var basic = Create();
        basic.Resize(700, 420);
        Assert.False(basic.ShowAdvanced);
    }

    [Fact]
    public void Resizing_BetweenModes_KeepsTheNumber()
    {
        var vm = Create(new CalculatorSettings(Advanced: true));
        vm.Resize(700, 420);
        Type(vm, "2+3*4");
        Assert.Equal("2+3×4", vm.Display);

        vm.Resize(300, 480);
        Assert.Equal("14", vm.Display);
        Type(vm, "+1=");
        Assert.Equal("15", vm.Display);

        vm.Resize(700, 420);
        Assert.Equal("15", vm.Display);
        Type(vm, "*2=");
        Assert.Equal("30", vm.Display);
    }

    [Fact]
    public void TurningTheSettingOff_ReturnsToTheBasicKeypad()
    {
        var repository = new FakeRepository(new CalculatorSettings(Advanced: true));
        var settings = new CalculatorSettingsViewModel(repository);
        var vm = new CalculatorViewModel(settings, CultureInfo.InvariantCulture);
        vm.Resize(700, 420);
        Type(vm, "(2+3)*4");
        Assert.True(vm.ShowAdvanced);

        settings.Advanced = false;
        Assert.False(vm.ShowAdvanced);
        Assert.Equal("20", vm.Display);
    }

    [Fact]
    public void AdvancedKeys_Work_InAdvancedMode()
    {
        var vm = Create(new CalculatorSettings(Advanced: true));
        vm.Resize(700, 420);
        vm.PressCommand.Execute(CalcKey.Sin);
        Type(vm, "30=");
        Assert.Equal("0.5", vm.Display);
    }

    [Fact]
    public void AngleMode_StartsFromTheSetting_AndTheKeyTogglesIt()
    {
        var vm = Create(new CalculatorSettings(Advanced: true, Angle: AngleMode.Radians));
        Assert.Equal(AngleMode.Radians, vm.Angle);
        Assert.Equal("RAD", vm.AngleLabel);
        vm.PressCommand.Execute(CalcKey.ToggleAngle);
        Assert.Equal(AngleMode.Degrees, vm.Angle);
        Assert.Equal("DEG", vm.AngleLabel);
    }

    [Fact]
    public void ChangingTheDefaultAngle_AppliesToTheCalculator()
    {
        var settings = new CalculatorSettingsViewModel(new FakeRepository());
        var vm = new CalculatorViewModel(settings, CultureInfo.InvariantCulture);
        settings.Angle = AngleMode.Radians;
        Assert.Equal(AngleMode.Radians, vm.Angle);
    }

    [Fact]
    public void Memory_ShowsInTheViewModel()
    {
        var vm = Create(new CalculatorSettings(Advanced: true));
        vm.Resize(700, 420);
        Assert.False(vm.HasMemory);
        Type(vm, "5");
        vm.PressCommand.Execute(CalcKey.MemoryAdd);
        Assert.True(vm.HasMemory);
        vm.PressCommand.Execute(CalcKey.MemoryClear);
        Assert.False(vm.HasMemory);
    }

    [Fact]
    public void Layout_FollowsTheWidgetSize()
    {
        var vm = Create();
        vm.Resize(300, 480);
        Assert.Equal(CalculatorLayout.For(300, 480, advanced: false), vm.Layout);
        vm.Resize(400, 600);
        Assert.Equal(CalculatorLayout.For(400, 600, advanced: false), vm.Layout);
    }

    [Fact]
    public void Settings_AreSavedWhenChanged_AndLoadedAgain()
    {
        var repository = new FakeRepository();
        var settings = new CalculatorSettingsViewModel(repository);
        Assert.False(settings.Advanced);
        Assert.Equal(AngleMode.Degrees, settings.Angle);
        Assert.Equal(0, repository.Saves);

        settings.Advanced = true;
        settings.Angle = AngleMode.Radians;
        Assert.Equal(new CalculatorSettings(true, AngleMode.Radians), repository.Saved);

        var again = new CalculatorSettingsViewModel(repository);
        Assert.True(again.Advanced);
        Assert.Equal(AngleMode.Radians, again.Angle);
        Assert.True(again.IsRadians);
        Assert.False(again.IsDegrees);
    }

    [Fact]
    public void AngleChoices_SetTheAngle()
    {
        var settings = new CalculatorSettingsViewModel(new FakeRepository());
        settings.IsRadians = true;
        Assert.Equal(AngleMode.Radians, settings.Angle);
        settings.IsDegrees = true;
        Assert.Equal(AngleMode.Degrees, settings.Angle);
    }

    [Fact]
    public void NoRepository_StillWorks()
    {
        var settings = new CalculatorSettingsViewModel(null);
        settings.Advanced = true;
        Assert.True(settings.Advanced);
    }
}
