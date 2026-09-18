using Chisel.Core.Notes;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.Core.Tests.Notes;

public class NoteTests
{
    [Fact]
    public void WordCount_MultiWordBody_CountsWords()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var note = new Note(time, "Groceries", "Buy milk and bread");

        // Act
        var wordCount = note.WordCount;

        // Assert
        Assert.Equal(4, wordCount);
    }

    [Fact]
    public void WordCount_EmptyBody_ReturnsZero()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var note = new Note(time, "Empty note", "");

        // Act
        var wordCount = note.WordCount;

        // Assert
        Assert.Equal(0, wordCount);
    }

    [Fact]
    public void EditBody_ChangesText_UpdatesTimestamp()
    {
        // Arrange
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero));
        var note = new Note(time, "Groceries", "Buy milk");

        // Act
        time.SetUtcNow(new DateTimeOffset(2026, 9, 17, 9, 5, 0, TimeSpan.Zero));
        note.EditBody("Buy milk and bread");

        // Assert
        Assert.Equal(new DateTimeOffset(2026, 9, 17, 9, 5, 0, TimeSpan.Zero), note.UpdatedAt);
    }
}