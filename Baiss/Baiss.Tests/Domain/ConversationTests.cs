using Baiss.Domain.Entities;
using FluentAssertions;

namespace Baiss.Tests.Domain;

public class ConversationTests
{
    [Fact]
    public void CreateFromFirstMessage_WithValidContent_ShouldCreateConversation()
    {
        // Arrange
        var messageContent = "Hello, how are you?";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Id.Should().NotBe(Guid.Empty);
        conversation.Title.Should().Be("Hello, how are you?");
        conversation.CreatedByUserId.Should().Be(string.Empty);
        conversation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        conversation.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        conversation.Messages.Should().NotBeNull();
        conversation.Messages.Should().BeEmpty();
    }

    [Fact]
    public void CreateFromFirstMessage_WithDefaultUserId_ShouldUseDefaultUser()
    {
        // Arrange
        var messageContent = "Test message";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.CreatedByUserId.Should().Be(string.Empty);
    }

    [Fact]
    public void CreateFromFirstMessage_WithShortContent_ShouldNotTruncateTitle()
    {
        // Arrange
        var messageContent = "Short message";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("Short message");
    }

    [Fact]
    public void CreateFromFirstMessage_WithLongContent_ShouldTruncateTitle()
    {
        // Arrange
        var messageContent = "This is a very long message that greatly exceeds the 20 character limit";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("This is a very long...");
        conversation.Title.Length.Should().Be(22); // 19 characters + "..." (because "This is a very long" is 19 chars)
    }

    [Fact]
    public void CreateFromFirstMessage_WithEmptyContent_ShouldUseDefaultTitle()
    {
        // Arrange
        var messageContent = "";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("New conversation");
    }

    [Fact]
    public void CreateFromFirstMessage_WithWhitespaceContent_ShouldUseDefaultTitle()
    {
        // Arrange
        var messageContent = "   ";

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("New conversation");
    }

    [Fact]
    public void CreateFromFirstMessage_WithNullContent_ShouldUseDefaultTitle()
    {
        // Arrange
        string? messageContent = null;

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent!);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("New conversation");
    }

    [Fact]
    public void CreateFromFirstMessage_WithExactly20Characters_ShouldNotTruncate()
    {
        // Arrange
        var messageContent = "Exactement20Charact1"; // Exactly 20 characters

        // Act
        var conversation = Conversation.CreateFromFirstMessage(messageContent);

        // Assert
        conversation.Should().NotBeNull();
        conversation.Title.Should().Be("Exactement20Charact1");
        conversation.Title.Length.Should().Be(20);
    }

    [Fact]
    public void UpdateTimestamp_ShouldUpdateUpdatedAtProperty()
    {
        // Arrange
        var conversation = Conversation.CreateFromFirstMessage("Test");
        var originalTimestamp = conversation.UpdatedAt;

        // Wait a bit to ensure the timestamp changes
        Thread.Sleep(10);

        // Act
        conversation.UpdateTimestamp();

        // Assert
        conversation.UpdatedAt.Should().BeAfter(originalTimestamp);
        conversation.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("Simple message", "Simple message")]
    [InlineData("  Message with spaces  ", "Message with space...")]
    [InlineData("Test message exactly twenty characters !", "Test message exactly...")]
    [InlineData("A", "A")]
    public void CreateFromFirstMessage_WithVariousInputs_ShouldGenerateCorrectTitles(
        string input, string expectedTitle)
    {
        // Act
        var conversation = Conversation.CreateFromFirstMessage(input);

        // Assert
        conversation.Title.Should().Be(expectedTitle);
    }
}
