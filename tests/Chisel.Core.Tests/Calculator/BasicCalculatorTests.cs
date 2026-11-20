using System.Globalization;
using Chisel.Core.Calculator;
using static Chisel.Core.Tests.Calculator.CalcPad;

namespace Chisel.Core.Tests.Calculator;

public class BasicCalculatorTests
{
    [Theory]
    [InlineData("2+3=", "5")]
    [InlineData("0.1+0.2=", "0.3")]
    [InlineData("9-12=", "-3")]
    [InlineData("6*7=", "42")]
    [InlineData("1/4=", "0.25")]
    [InlineData("1/3=", "0.333333333333")]
    public void Arithmetic_ShowsTheExactResult(string keys, string shown) => Assert.Equal(shown, Run(keys).Display);

    [Fact]
    public void Starts_AtZero() => Assert.Equal("0", new CalculatorEngine(CultureInfo.InvariantCulture).Display);

    [Fact]
    public void Operations_ChainLeftToRight_LikeAPhone() => Assert.Equal("20", Run("2+3*4=").Display);

    [Fact]
    public void ChainedOperator_ShowsTheRunningResult() => Assert.Equal("5", Run("2+3-").Display);

    [Fact]
    public void ChangingTheOperator_ReplacesIt() => Assert.Equal("15", Run("5+*3=").Display);

    [Fact]
    public void Equals_RepeatsTheLastOperation()
    {
        var engine = Run("5+3=");
        Assert.Equal("8", engine.Display);
        Press(engine, "=");
        Assert.Equal("11", engine.Display);
        Press(engine, "=");
        Assert.Equal("14", engine.Display);
    }

    [Fact]
    public void EqualsWithNoSecondNumber_UsesTheDisplayedOne() => Assert.Equal("25", Run("5*=").Display);

    [Fact]
    public void ADigitAfterEquals_StartsANewCalculation()
    {
        var engine = Run("2+3=4");
        Assert.Equal("4", engine.Display);
        Press(engine, "=");
        Assert.Equal("4", engine.Display);
    }

    [Fact]
    public void AnOperatorAfterEquals_ContinuesFromTheResult() => Assert.Equal("10", Run("2+3=*2=").Display);

    [Theory]
    [InlineData("200+10%=", "220")]
    [InlineData("200-10%=", "180")]
    [InlineData("200*10%=", "20")]
    [InlineData("200/10%=", "2000")]
    [InlineData("50%", "0.5")]
    public void Percent_WorksLikeThePhone(string keys, string shown) => Assert.Equal(shown, Run(keys).Display);

    [Fact]
    public void Negate_FlipsTheSignOfTheEntryAndOfAResult()
    {
        Assert.Equal("-5", Run("5N").Display);
        Assert.Equal("5", Run("5NN").Display);
        Assert.Equal("-5", Run("2+3=N").Display);
        Assert.Equal("0", Run("N").Display);
    }

    [Fact]
    public void Negate_ThenContinuingToType_KeepsTheSign() => Assert.Equal("-52", Run("5N2").Display);

    [Theory]
    [InlineData("123B", "12")]
    [InlineData("5B", "0")]
    [InlineData("5NB", "0")]
    [InlineData("1.5B", "1.")]
    [InlineData("2+3=B", "5")]
    public void Backspace_RemovesTheLastTypedCharacter(string keys, string shown) => Assert.Equal(shown, Run(keys).Display);

    [Fact]
    public void Clear_ResetsEverything() => Assert.Equal("4", Run("12+3C4=").Display);

    [Theory]
    [InlineData(".5", "0.5")]
    [InlineData("1..2", "1.2")]
    [InlineData("0007", "7")]
    [InlineData("1.50", "1.50")]
    public void TypedNumbers_AreShownAsTyped(string keys, string shown) => Assert.Equal(shown, Run(keys).Display);

    [Fact]
    public void TypedNumbers_StopAtFifteenDigits() => Assert.Equal("123456789012345", Run("12345678901234567890").Display);

    [Fact]
    public void Secondary_ShowsThePendingOperation() => Assert.Equal("12 +", Run("12+").Secondary);

    [Fact]
    public void Secondary_IsEmptyWithNothingPending() => Assert.Equal("", Run("12+3=").Secondary);

    [Fact]
    public void DivideByZero_ShowsAnErrorAndRecoversOnTheNextKey()
    {
        var engine = Run("6/0=");
        Assert.True(engine.IsError);
        Assert.Equal("Error", engine.Display);

        Press(engine, "5");
        Assert.False(engine.IsError);
        Assert.Equal("5", engine.Display);
    }

    [Fact]
    public void AfterAnError_AnyKeyStartsFresh_AndDoesNotRepeatTheOldOperation()
    {
        var engine = Run("6/0=");
        Press(engine, "=");
        Assert.False(engine.IsError);
        Assert.Equal("0", engine.Display);
    }

    [Fact]
    public void Overflow_ShowsAnError() => Assert.True(Run("99999999999999*99999999999999=*99999999999999=").IsError);

    [Fact]
    public void TheDecimalSeparator_FollowsTheCulture()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("0,3", Run("0.1+0.2=", german).Display);
        Assert.Equal("1,", Run("1.", german).Display);
        Assert.Equal("-2,5", Run("2.5N", german).Display);
    }
}
