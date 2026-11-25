using Chisel.Core.Habits;

namespace Chisel.Core.Tests.Habits;

public class HabitValidationTests
{
    private static readonly Habit Read = new(Guid.NewGuid(), "Read");

    private static string? Check(string? name = "Walk", string? emoji = null, HabitDays days = HabitDays.Every, IReadOnlyList<Habit>? habits = null, Guid? editing = null) =>
        HabitValidation.Error(name, emoji, days, habits ?? [Read], editing);

    [Fact]
    public void AGoodHabit_Passes()
    {
        Assert.Null(Check(emoji: "📚"));
        Assert.Null(Check(emoji: "  "));
    }

    [Theory]
    [InlineData(null, "Give the habit a name.")]
    [InlineData("   ", "Give the habit a name.")]
    [InlineData("read", "You already have a habit with that name.")]
    [InlineData(" Read ", "You already have a habit with that name.")]
    public void BadNames_AreRefused(string? name, string error)
    {
        Assert.Equal(error, Check(name));
    }

    [Fact]
    public void ALongName_IsRefused()
    {
        Assert.Equal($"Keep the name under {HabitValidation.MaxNameLength} characters.", Check(new string('a', HabitValidation.MaxNameLength + 1)));
    }

    [Fact]
    public void Renaming_MayKeepItsOwnName()
    {
        Assert.Null(Check("READ", editing: Read.Id));
    }

    [Fact]
    public void MoreThanOneEmoji_OrNoDays_AreRefused()
    {
        Assert.Equal("Use a single emoji, or leave it empty.", Check(emoji: "📚📚"));
        Assert.Equal("Pick at least one day.", Check(days: HabitDays.None));
    }

    [Fact]
    public void AddingPastTheCap_IsRefused_ButEditingIsNot()
    {
        var full = Enumerable.Range(0, HabitValidation.MaxHabits).Select(i => new Habit(Guid.NewGuid(), $"H{i}")).ToList();
        Assert.Equal($"You can track up to {HabitValidation.MaxHabits} habits.", Check(habits: full));
        Assert.Null(Check("H0", habits: full, editing: full[0].Id));
    }
}
