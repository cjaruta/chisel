using System.Globalization;
using Chisel.Core.Calculator;

namespace Chisel.Core.Tests.Calculator;

public class ExpressionEvaluatorTests
{
    private static string Eval(string expression, AngleMode angle = AngleMode.Degrees) =>
        ExpressionEvaluator.Evaluate(expression, angle, CultureInfo.InvariantCulture);

    private static void Fails(string expression, AngleMode angle = AngleMode.Degrees) =>
        Assert.Throws<CalculatorException>(() => Eval(expression, angle));

    [Theory]
    [InlineData("2+3*4", "14")]
    [InlineData("2*3+4", "10")]
    [InlineData("10-4-3", "3")]
    [InlineData("100/10/5", "2")]
    [InlineData("0.1+0.2", "0.3")]
    [InlineData("1/3", "0.333333333333")]
    [InlineData("2*3^2", "18")]
    public void Operators_FollowPrecedenceAndAssociativity(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("(2+3)*4", "20")]
    [InlineData("2*(3+4)", "14")]
    [InlineData("((1+2)*(3+4))", "21")]
    [InlineData("(2+3", "5")]
    [InlineData("sin(30", "0.5")]
    [InlineData("2(3+1)", "8")]
    [InlineData("(1+1)(2+2)", "8")]
    public void Parentheses_GroupAndAreClosedAtTheEnd(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("-3+5", "2")]
    [InlineData("2*-3", "-6")]
    [InlineData("2--3", "5")]
    [InlineData("-2^2", "-4")]
    [InlineData("(-2)^2", "4")]
    [InlineData("2^-1", "0.5")]
    [InlineData("2^3^2", "512")]
    [InlineData("2^10", "1024")]
    public void Signs_AndPowers_BindCorrectly(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("200+10%", "220")]
    [InlineData("200-10%", "180")]
    [InlineData("200*10%", "20")]
    [InlineData("50%", "0.5")]
    public void Percent_WorksLikeThePhone(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("sin(30)", "0.5")]
    [InlineData("cos(60)", "0.5")]
    [InlineData("tan(45)", "1")]
    [InlineData("sin(0)", "0")]
    [InlineData("sin(180)", "0")]
    [InlineData("cos(90)", "0")]
    [InlineData("asin(0.5)", "30")]
    [InlineData("acos(0.5)", "60")]
    [InlineData("atan(1)", "45")]
    public void Trig_InDegrees(string expression, string expected) => Assert.Equal(expected, Eval(expression, AngleMode.Degrees));

    [Theory]
    [InlineData("sin(pi/2)", "1")]
    [InlineData("cos(pi)", "-1")]
    [InlineData("sin(pi)", "0")]
    [InlineData("tan(pi/4)", "1")]
    [InlineData("sin(30)", "-0.988031624093")]
    [InlineData("asin(1)", "1.57079632679")]
    [InlineData("atan(1)", "0.785398163397")]
    public void Trig_InRadians(string expression, string expected) => Assert.Equal(expected, Eval(expression, AngleMode.Radians));

    [Theory]
    [InlineData("ln(e)", "1")]
    [InlineData("ln(1)", "0")]
    [InlineData("log(1000)", "3")]
    [InlineData("log(0.01)", "-2")]
    [InlineData("sqrt(16)", "4")]
    [InlineData("sqrt(2)", "1.41421356237")]
    [InlineData("pi", "3.14159265359")]
    [InlineData("e", "2.71828182846")]
    [InlineData("2pi", "6.28318530718")]
    [InlineData("5^2", "25")]
    [InlineData("4^(-1)", "0.25")]
    [InlineData("2^0.5", "1.41421356237")]
    [InlineData("10^15", "1E+15")]
    [InlineData("10^30", "1E+30")]
    public void Functions_AndConstants(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("0!", "1")]
    [InlineData("1!", "1")]
    [InlineData("5!", "120")]
    [InlineData("10!", "3628800")]
    [InlineData("20!", "2.43290200818E+18")]
    [InlineData("170!", "7.25741561531E+306")]
    [InlineData("3!^2", "36")]
    [InlineData("(2+1)!", "6")]
    public void Factorial_GrowsToTheLimit(string expression, string expected) => Assert.Equal(expected, Eval(expression));

    [Theory]
    [InlineData("171!")]
    [InlineData("3.5!")]
    [InlineData("(-1)!")]
    [InlineData("1/0")]
    [InlineData("0^(-1)")]
    [InlineData("sqrt(-1)")]
    [InlineData("ln(0)")]
    [InlineData("ln(-5)")]
    [InlineData("log(0)")]
    [InlineData("asin(2)")]
    [InlineData("acos(-1.5)")]
    [InlineData("tan(90)")]
    [InlineData("(-8)^0.5")]
    [InlineData("10^400")]
    [InlineData("(1+2))")]
    [InlineData("()")]
    [InlineData("2**3")]
    [InlineData("")]
    [InlineData("abc(3)")]
    public void ImpossibleExpressions_AreErrors(string expression) => Fails(expression);

    [Fact]
    public void Tan_OfARightAngleInRadians_IsAnError() => Fails("tan(pi/2)", AngleMode.Radians);

    [Fact]
    public void ATrailingOperator_IsIgnored() => Assert.Equal("5", Eval("2+3+"));

    [Fact]
    public void TheUnicodeSymbols_AreUnderstood() => Assert.Equal("9", Eval("2+3×12÷4−2"));

    [Fact]
    public void ScientificNumbers_AreUnderstood() => Assert.Equal("3E+20", Eval("1.5E+20*2"));
}
