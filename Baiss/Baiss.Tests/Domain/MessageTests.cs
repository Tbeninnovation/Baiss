using Baiss.Domain.Entities;
using FluentAssertions;

namespace Baiss.Tests.Domain;

public class MessageTests
{
    [Fact]
    public void CreateUserMessage_WithValidParameters_ShouldCreateUserMessage()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Hello, how are you?";

        // Act
        var message = Message.CreateUserMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.Id.Should().NotBe(Guid.Empty);
        message.ConversationId.Should().Be(conversationId);
        message.Content.Should().Be(content);
        message.SenderType.Should().Be(SenderType.USER);
        message.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.Conversation.Should().BeNull(); // Navigation property not initialized
    }

    [Fact]
    public void CreateUserMessage_WithDefaultSenderType_ShouldUseUserType()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Test message";

        // Act
        var message = Message.CreateUserMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.SenderType.Should().Be(SenderType.USER);
    }

    [Fact]
    public void CreateAssistantMessage_WithValidParameters_ShouldCreateAssistantMessage()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "I'm doing well, thank you!";

        // Act
        var message = Message.CreateAssistantMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.Id.Should().NotBe(Guid.Empty);
        message.ConversationId.Should().Be(conversationId);
        message.Content.Should().Be(content);
        message.SenderType.Should().Be(SenderType.ASSISTANT);
        message.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.Conversation.Should().BeNull();
    }

    [Fact]
    public void CreateAssistantMessage_WithDefaultSenderType_ShouldUseAssistantType()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Assistant response";

        // Act
        var message = Message.CreateAssistantMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.SenderType.Should().Be(SenderType.ASSISTANT);
    }

    [Fact]
    public void CreateUserMessage_WithEmptyContent_ShouldCreateMessageWithEmptyContent()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "";

        // Act
        var message = Message.CreateUserMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.Content.Should().Be("");
    }

    [Fact]
    public void CreateUserMessage_WithEmptyGuidConversationId_ShouldCreateMessageWithEmptyGuid()
    {
        // Arrange
        var conversationId = Guid.Empty;
        var content = "Test message";

        // Act
        var message = Message.CreateUserMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.ConversationId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void CreateUserMessage_ShouldGenerateUniqueIds()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Test message";

        // Act
        var message1 = Message.CreateUserMessage(conversationId, content);
        var message2 = Message.CreateUserMessage(conversationId, content);

        // Assert
        message1.Id.Should().NotBe(message2.Id);
        message1.Id.Should().NotBe(Guid.Empty);
        message2.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateAssistantMessage_ShouldGenerateUniqueIds()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Assistant response";

        // Act
        var message1 = Message.CreateAssistantMessage(conversationId, content);
        var message2 = Message.CreateAssistantMessage(conversationId, content);

        // Assert
        message1.Id.Should().NotBe(message2.Id);
        message1.Id.Should().NotBe(Guid.Empty);
        message2.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("Short message")]
    [InlineData("Very long message with lots of content to test that length doesn't affect creation")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Message with special characters: àéèç!@#$%^&*()")]
    public void CreateUserMessage_WithVariousContent_ShouldCreateValidMessages(string content)
    {
        // Arrange
        var conversationId = Guid.NewGuid();

        // Act
        var message = Message.CreateUserMessage(conversationId, content);

        // Assert
        message.Should().NotBeNull();
        message.Content.Should().Be(content);
        message.Id.Should().NotBe(Guid.Empty);
        message.ConversationId.Should().Be(conversationId);
        message.SenderType.Should().Be(SenderType.USER);
    }

    [Fact]
    public void CreateUserMessage_AndCreateAssistantMessage_ShouldHaveDifferentSenderTypes()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Test message";

        // Act
        var userMessage = Message.CreateUserMessage(conversationId, content);
        var assistantMessage = Message.CreateAssistantMessage(conversationId, content);

        // Assert
        userMessage.SenderType.Should().Be(SenderType.USER);
        assistantMessage.SenderType.Should().Be(SenderType.ASSISTANT);
        userMessage.SenderType.Should().NotBe(assistantMessage.SenderType);
    }

    [Fact]
    public void CreateUserMessage_TimestampsShouldBeConsistent()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Test timing";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = Message.CreateUserMessage(conversationId, content);
        var afterCreation = DateTime.UtcNow;

        // Assert
        message.SentAt.Should().BeOnOrAfter(beforeCreation);
        message.SentAt.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void CreateAssistantMessage_TimestampsShouldBeConsistent()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var content = "Assistant response";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = Message.CreateAssistantMessage(conversationId, content);
        var afterCreation = DateTime.UtcNow;

        // Assert
        message.SentAt.Should().BeOnOrAfter(beforeCreation);
        message.SentAt.Should().BeOnOrBefore(afterCreation);
    }
}
