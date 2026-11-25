using System.Collections.Immutable;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class HabitRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private HabitData Load()
    {
        using var context = _fixture.CreateContext();
        return new HabitRepository(context).Get();
    }

    private void Store(string json, int version = 1)
    {
        using var context = _fixture.CreateContext();
        context.Habits.Add(new HabitsEntity { Version = version, Json = json });
        context.SaveChanges();
    }

    private static void Same(Habit expected, Habit actual)
    {
        Assert.Equal(expected with { Done = [] }, actual with { Done = [] });
        Assert.Equal(expected.Done, actual.Done);
    }

    [Fact]
    public void Empty_ReturnsNoHabits()
    {
        Assert.Empty(Load().Habits);
    }

    [Fact]
    public void Save_RoundTripsEveryField_KeepsTheOrder_AndReplacesThePreviousRecord()
    {
        var read = new Habit(Guid.NewGuid(), "Read", "📚", CountdownColor.None, HabitDays.Every,
            ImmutableSortedSet.Create(new DateOnly(1999, 12, 31), new DateOnly(2000, 1, 1)), new DateOnly(1999, 12, 1));
        var gym = new Habit(Guid.NewGuid(), "Gym", null, CountdownColor.Blue, HabitDays.Monday | HabitDays.Thursday, Created: new DateOnly(2000, 1, 1));
        using (var context = _fixture.CreateContext())
        {
            var repository = new HabitRepository(context);
            repository.Save(new HabitData([gym]));
            repository.Save(new HabitData([read, gym]));
        }

        var loaded = Load().Habits;
        Assert.Equal(2, loaded.Count);
        Same(read, loaded[0]);
        Same(gym, loaded[1]);
        using var check = _fixture.CreateContext();
        var row = Assert.Single(check.Habits);
        Assert.Equal(HabitRepository.CurrentVersion, row.Version);
        Assert.Contains("[19991231,20000101]", row.Json); // dates are stored compactly
    }

    [Fact]
    public void YearsOfHistory_AreKept_WithNothingPruned()
    {
        var start = new DateOnly(1997, 1, 1);
        var done = Enumerable.Range(0, 3 * 366).Select(start.AddDays).ToImmutableSortedSet();
        using (var context = _fixture.CreateContext())
        {
            new HabitRepository(context).Save(new HabitData([new Habit(Guid.NewGuid(), "Walk", Done: done)]));
        }

        Assert.Equal(done, Load().Habits.Single().Done);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"habits\":5}")]
    public void UnreadableData_GivesNoHabits(string json)
    {
        Store(json);
        Assert.Empty(Load().Habits);
    }

    [Fact]
    public void BadEntriesAndValues_AreSkippedOrDefaulted_AndTheRestKept()
    {
        var id = Guid.NewGuid();
        Store($$"""
            {"habits":[
              5,
              {"name":"  "},
              {"id":"{{id}}","name":"Read","emoji":"📚📚","color":"Pink","days":0,"created":"x","done":[20000101,"y",20001301,99999999,20000102,20000101]},
              {"id":"{{id}}","name":"Copy of the same id"},
              {"name":"read"},
              {"name":"Walk","days":200}
            ]}
            """);

        var habits = Load().Habits;
        Assert.Equal(new[] { "Read", "Walk" }, habits.Select(h => h.Name));
        var read = habits[0];
        Assert.Equal(id, read.Id);
        Assert.Null(read.Emoji);
        Assert.Equal(CountdownColor.None, read.Color);
        Assert.Equal(HabitDays.Every, read.Days);
        Assert.Equal(default, read.Created);
        Assert.Equal(new[] { new DateOnly(2000, 1, 1), new DateOnly(2000, 1, 2) }, read.Done);
        Assert.Equal(HabitDays.Every, habits[1].Days);
        Assert.NotEqual(Guid.Empty, habits[1].Id);
    }

    [Fact]
    public void MoreThanTheCap_KeepsTheFirstOnes()
    {
        var entries = string.Join(",", Enumerable.Range(0, HabitValidation.MaxHabits + 5).Select(i => $"{{\"name\":\"H{i}\"}}"));
        Store($"{{\"habits\":[{entries}]}}");

        Assert.Equal(HabitValidation.MaxHabits, Load().Habits.Count);
    }

    public void Dispose() => _fixture.Dispose();
}
