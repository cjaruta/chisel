using System.Globalization;
using Chisel.Core.Calculator;
using static Chisel.Core.Tests.Calculator.CalcPad;

namespace Chisel.Core.Tests.Calculator;

public class AdvancedCalculatorTests
{
    private static CalculatorEngine Adv(string keys = "") => Run(keys, advanced: true);

    private static void Keys(CalculatorEngine engine, params CalcKey[] keys)
    {
        foreach (var key in keys)
        {
            engine.Press(key);
        }
    }

    [Theory]
    [InlineData("2+3*4=", "14")]
    [InlineData("(2+3)*4=", "20")]
    [InlineData("0.1+0.2=", "0.3")]
    [InlineData("200+10%=", "220")]
    [InlineData("(2+3=", "5")]
    [InlineData("2+3+=", "5")]
    [InlineData("5N+2=", "-3")]
    [InlineData("1/0=", "Error")]
    public void Expressions_AreEvaluatedWithPrecedence(string keys, string shown) => Assert.Equal(shown, Adv(keys).Display);

    [Fact]
    public void WhileTyping_TheDisplayShowsTheExpression()
    {
        Assert.Equal("2+3×4", Adv("2+3*4").Display);
        Assert.Equal("(2+3)÷", Adv("(2+3)/").Display);
        Assert.Equal("1.5", Adv("1.5").Display);
        Assert.Equal("0", Adv().Display);
    }

    [Fact]
    public void WhileTyping_ThePreviewShowsTheValueSoFar()
    {
        Assert.Equal("= 14", Adv("2+3*4").Secondary);
        Assert.Equal("", Adv("7").Secondary);
        Assert.Equal("", Adv("2+").Secondary);
    }

    [Fact]
    public void AfterEquals_TheSecondaryShowsWhatWasCalculated()
    {
        var engine = Adv("2+3*4=");
        Assert.Equal("2+3×4 =", engine.Secondary);
    }

    [Fact]
    public void AfterEquals_ADigitStartsOver_AndAnOperatorContinues()
    {
        var engine = Adv("2+3=");
        Press(engine, "*2=");
        Assert.Equal("10", engine.Display);
        Press(engine, "7");
        Assert.Equal("7", engine.Display);
    }

    [Fact]
    public void ChangingTheOperator_ReplacesIt() => Assert.Equal("15", Adv("5+*3=").Display);

    [Fact]
    public void Backspace_RemovesTheLastCharacter_ThenTheLastToken()
    {
        var engine = Adv("12+34");
        Press(engine, "B");
        Assert.Equal("12+3", engine.Display);
        Press(engine, "BB");
        Assert.Equal("12", engine.Display);
        Press(engine, "BBB");
        Assert.Equal("0", engine.Display);
    }

    [Fact]
    public void Clear_EmptiesTheExpression() => Assert.Equal("0", Adv("2+3C").Display);

    [Fact]
    public void Negate_FlipsTheNumberBeingTyped_AndTheResult()
    {
        Assert.Equal("2+-5", Adv("2+5N").Display);
        Assert.Equal("2+5", Adv("2+5NN").Display);
        Assert.Equal("-5", Adv("2+3=N").Display);
        Assert.Equal("-5", Adv("5N").Display);
    }

    [Fact]
    public void Negate_ThenSquare_SquaresTheNegativeNumber()
    {
        var engine = Adv("5N");
        engine.Press(CalcKey.Square);
        engine.Press(CalcKey.Equals);
        Assert.Equal("25", engine.Display);
    }

    [Fact]
    public void FunctionKeys_OpenAFunction_AndEqualsClosesIt()
    {
        var engine = Adv();
        Keys(engine, CalcKey.Sin);
        Press(engine, "30");
        Assert.Equal("sin(30", engine.Display);
        Press(engine, "=");
        Assert.Equal("0.5", engine.Display);
    }

    [Fact]
    public void InverseTrig_ShowsTheInverseNotation()
    {
        var engine = Adv();
        Keys(engine, CalcKey.Asin);
        Press(engine, "0.5");
        Assert.Equal("sin⁻¹(0.5", engine.Display);
        Press(engine, "=");
        Assert.Equal("30", engine.Display);
    }

    [Fact]
    public void FunctionOnAResult_WrapsTheResult()
    {
        var engine = Adv("16=");
        engine.Press(CalcKey.Sqrt);
        engine.Press(CalcKey.Equals);
        Assert.Equal("4", engine.Display);
    }

    [Fact]
    public void AngleMode_CanBeToggled_AndChangesTrig()
    {
        var engine = Adv();
        Assert.Equal(AngleMode.Degrees, engine.Angle);
        engine.Press(CalcKey.ToggleAngle);
        Assert.Equal(AngleMode.Radians, engine.Angle);
        Keys(engine, CalcKey.Sin);
        Press(engine, "30=");
        Assert.Equal("-0.988031624093", engine.Display);
    }

    [Fact]
    public void ConstantsAndPostfixKeys()
    {
        var engine = Adv();
        Keys(engine, CalcKey.Pi, CalcKey.Multiply, CalcKey.Digit2, CalcKey.Equals);
        Assert.Equal("6.28318530718", engine.Display);

        engine = Adv("5");
        Keys(engine, CalcKey.Square, CalcKey.Equals);
        Assert.Equal("25", engine.Display);

        engine = Adv("4");
        Keys(engine, CalcKey.Reciprocal, CalcKey.Equals);
        Assert.Equal("0.25", engine.Display);

        engine = Adv("5");
        Keys(engine, CalcKey.Factorial, CalcKey.Equals);
        Assert.Equal("120", engine.Display);

        engine = Adv("2");
        Keys(engine, CalcKey.Power, CalcKey.Digit8, CalcKey.Equals);
        Assert.Equal("256", engine.Display);
    }

    [Fact]
    public void Factorial_PastTheLimit_IsAnErrorThatRecovers()
    {
        var engine = Adv("171");
        Keys(engine, CalcKey.Factorial, CalcKey.Equals);
        Assert.True(engine.IsError);
        Press(engine, "3");
        Assert.False(engine.IsError);
        Assert.Equal("3", engine.Display);
    }

    [Fact]
    public void Parentheses_AreBalanced()
    {
        var engine = Adv("2");
        Keys(engine, CalcKey.CloseParen);
        Assert.Equal("2", engine.Display);
        Keys(engine, CalcKey.Multiply, CalcKey.OpenParen, CalcKey.Digit3, CalcKey.CloseParen, CalcKey.CloseParen, CalcKey.Equals);
        Assert.Equal("6", engine.Display);
    }

    [Fact]
    public void Memory_AddsSubtractsRecallsAndClears()
    {
        var engine = Adv("5");
        Assert.False(engine.HasMemory);
        engine.Press(CalcKey.MemoryAdd);
        Assert.True(engine.HasMemory);
        Press(engine, "C3");
        engine.Press(CalcKey.MemoryAdd);
        Press(engine, "C1");
        engine.Press(CalcKey.MemorySubtract);
        Press(engine, "C");
        engine.Press(CalcKey.MemoryRecall);
        Assert.Equal("7", engine.Display);

        engine.Press(CalcKey.MemoryClear);
        Assert.False(engine.HasMemory);
        Press(engine, "C");
        engine.Press(CalcKey.MemoryRecall);
        Assert.Equal("0", engine.Display);
    }

    [Fact]
    public void Memory_AddsTheValueOfTheWholeExpression()
    {
        var engine = Adv("2+3*4");
        engine.Press(CalcKey.MemoryAdd);
        Press(engine, "C");
        engine.Press(CalcKey.MemoryRecall);
        Assert.Equal("14", engine.Display);
    }

    [Fact]
    public void Memory_RecallsIntoTheExpression()
    {
        var engine = Adv("5");
        engine.Press(CalcKey.MemoryAdd);
        Press(engine, "C2*");
        engine.Press(CalcKey.MemoryRecall);
        Press(engine, "=");
        Assert.Equal("10", engine.Display);
    }

    [Fact]
    public void Memory_IsSharedWithTheBasicKeypad()
    {
        var engine = Adv("5");
        engine.Press(CalcKey.MemoryAdd);
        engine.Advanced = false;
        Press(engine, "C2+");
        engine.Press(CalcKey.MemoryRecall);
        Press(engine, "=");
        Assert.Equal("7", engine.Display);
    }

    [Fact]
    public void BasicMode_IgnoresTheAdvancedKeys()
    {
        var engine = Run("5");
        Keys(engine, CalcKey.Sin, CalcKey.OpenParen, CalcKey.Pi, CalcKey.Factorial);
        Assert.Equal("5", engine.Display);
    }

    [Fact]
    public void SwitchingMode_KeepsTheNumber()
    {
        var engine = Run("12.5");
        engine.Advanced = true;
        Assert.Equal("12.5", engine.Display);
        Press(engine, "+3=");
        Assert.Equal("15.5", engine.Display);

        engine.Advanced = false;
        Assert.Equal("15.5", engine.Display);
        Press(engine, "*2=");
        Assert.Equal("31", engine.Display);
    }

    [Fact]
    public void SwitchingToBasicMidExpression_KeepsTheValueSoFar()
    {
        var engine = Adv("2+3*4");
        engine.Advanced = false;
        Assert.Equal("14", engine.Display);
    }

    [Fact]
    public void SwitchingFromAnError_StartsAtZero()
    {
        var engine = Adv("1/0=");
        engine.Advanced = false;
        Assert.False(engine.IsError);
        Assert.Equal("0", engine.Display);
    }

    [Fact]
    public void TheDecimalSeparator_FollowsTheCulture()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("0,3", Run("0.1+0.2=", german, advanced: true).Display);
        Assert.Equal("1,5+2", Run("1.5+2", german, advanced: true).Display);
    }

    [Fact]
    public void ACopyableResult_IsTheDisplayedNumber()
    {
        Assert.Equal("14", Adv("2+3*4=").Result);
        Assert.Equal("14", Adv("2+3*4").Result);
        Assert.Equal("8", Run("5+3=").Result);
        Assert.Equal("", Run("6/0=").Result);
    }
}
