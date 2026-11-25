using System.Globalization;
using Chisel.Core.Countdown;

namespace Chisel.Core.Tests.Countdown;

public class CountdownValidationTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static CountdownValidation.Result Check(string? name = "Trip", DateOnly? date = null, string? time = null, string? emoji = null, int count = 0, bool adding = true) =>
        CountdownValidation.Validate(name, date ?? new DateOnly(2000, 6, 1), time, emoji, count, adding, Culture);

    [Fact]
    public void AGoodEvent_Passes_WithItsTimeParsed()
    {
        var result = Check(time: "16:30", emoji: "🎉");
        Assert.Null(result.Error);
        Assert.Equal(new TimeOnly(16, 30), result.Time);
        Assert.Equal(new TimeOnly(16, 30), Check(time: "4:30 PM").Time);
        Assert.Null(Check(time: "  ").Time);
    }

    [Theory]
    [InlineData(null, "Give the event a name.")]
    [InlineData("   ", "Give the event a name.")]
    public void TheName_IsRequired(string? name, string error) => Assert.Equal(error, Check(name: name).Error);

    [Fact]
    public void TheName_HasAMaximumLength() =>
        Assert.Equal($"Keep the name under {CountdownValidation.MaxNameLength} characters.", Check(name: new string('a', CountdownValidation.MaxNameLength + 1)).Error);

    [Fact]
    public void TheDate_IsRequiredAndInRange()
    {
        Assert.Equal("Pick a date.", CountdownValidation.Validate("Trip", null, null, null, 0, true, Culture).Error);
        Assert.Equal("Pick a date between 1900 and 2199.", Check(date: new DateOnly(1899, 12, 31)).Error);
        Assert.Equal("Pick a date between 1900 and 2199.", Check(date: new DateOnly(2200, 1, 1)).Error);
    }

    [Fact]
    public void ABadTime_OrMoreThanOneEmoji_IsRejected()
    {
        Assert.Equal("Enter the time like 14:30, or leave it empty.", Check(time: "25:00").Error);
        Assert.Equal("Use a single emoji, or leave it empty.", Check(emoji: "🎉🎂").Error);
    }

    [Fact]
    public void TheNumberOfEvents_IsCapped_ButEditingAtTheCapIsFine()
    {
        Assert.Equal($"You can track up to {CountdownValidation.MaxEvents} events.", Check(count: CountdownValidation.MaxEvents).Error);
        Assert.Null(Check(count: CountdownValidation.MaxEvents, adding: false).Error);
    }
}
