using System.Globalization;
using Chisel.App.ViewModels;
using Chisel.Core.Converter;

namespace Chisel.App.Tests.ViewModels;

public class ConverterViewModelTests
{
    private sealed class MemoryRepo : IConverterSettingsRepository
    {
        public ConverterSettings Saved = ConverterSettings.Default;
        public int Saves;
        public ConverterSettings Get() => Saved;
        public void Save(ConverterSettings settings) { Saved = settings; Saves++; }
    }

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Typing_ConvertsAtOnce()
    {
        var vm = new ConverterViewModel(new MemoryRepo(), Us);
        vm.Input = "1000";
        Assert.Equal("3,280.839895", vm.ResultText);
        Assert.Equal("3280.839895", vm.CopyText);
        Assert.False(vm.IsError);
    }

    [Fact]
    public void UsesTheCulture_ForInputAndResult()
    {
        var vm = new ConverterViewModel(new MemoryRepo { Saved = new ConverterSettings("length", "km", "m") }, De);
        vm.Input = "1,5";
        Assert.Equal("1.500", vm.ResultText);
        vm.Input = "1234,5";
        Assert.Equal("1.234.500", vm.ResultText);
        Assert.Equal("1234500", vm.CopyText);
    }

    [Fact]
    public void InvalidInput_ShowsAnError_AndNothingToCopy()
    {
        var vm = new ConverterViewModel(new MemoryRepo { Saved = new ConverterSettings("temperature", "K", "C") }, Us);
        vm.Input = "abc";
        Assert.True(vm.IsError);
        Assert.Equal(ConverterNumber.NotANumber, vm.ResultText);
        Assert.Null(vm.CopyText);
        vm.Input = "-5";
        Assert.Equal(UnitConverter.BelowAbsoluteZero, vm.ResultText);
        vm.Input = "";
        Assert.False(vm.IsError);
        Assert.Equal("", vm.ResultText);
        Assert.Empty(vm.Common);
    }

    [Fact]
    public void Swap_FlipsTheUnits_AndSaves()
    {
        var repo = new MemoryRepo();
        var vm = new ConverterViewModel(repo, Us) { Input = "1" };
        vm.SwapCommand.Execute(null);
        Assert.Equal("ft", vm.From.Id);
        Assert.Equal("m", vm.To.Id);
        Assert.Equal("0.3048", vm.ResultText);
        Assert.Equal(new ConverterSettings("length", "ft", "m"), repo.Saved);
    }

    [Fact]
    public void Category_Change_UsesItsDefaults_AndComesBackToItsLastUnits()
    {
        var repo = new MemoryRepo();
        var vm = new ConverterViewModel(repo, Us) { Input = "1" };
        vm.To = vm.Category.Unit("in")!;
        vm.SelectCategory("temperature");
        Assert.Equal(("C", "F"), (vm.From.Id, vm.To.Id));
        Assert.Equal("33.8", vm.ResultText);
        Assert.Equal(new ConverterSettings("temperature", "C", "F"), repo.Saved);
        vm.SelectCategory("length");
        Assert.Equal(("m", "in"), (vm.From.Id, vm.To.Id));
    }

    [Fact]
    public void RemembersTheSavedChoice()
    {
        var repo = new MemoryRepo();
        var first = new ConverterViewModel(repo, Us);
        first.SelectCategory("data");
        first.From = first.Category.Unit("GiB")!;
        var second = new ConverterViewModel(repo, Us);
        Assert.Equal(("data", "GiB", "MiB"), (second.Category.Id, second.From.Id, second.To.Id));
    }

    [Fact]
    public void Common_ListsOtherUnits_ButNotFromOrTo()
    {
        var vm = new ConverterViewModel(new MemoryRepo(), Us) { Input = "2" };
        var ids = vm.Common.Select(c => c.Unit.Id).ToList();
        Assert.Equal(new[] { "cm", "km", "in", "mi" }, ids);
        Assert.Equal("200", vm.Common[0].Text);
    }

    [Fact]
    public void TypingDoesNotSave()
    {
        var repo = new MemoryRepo();
        var vm = new ConverterViewModel(repo, Us);
        vm.Input = "12";
        vm.Input = "123";
        Assert.Equal(0, repo.Saves);
    }
}
