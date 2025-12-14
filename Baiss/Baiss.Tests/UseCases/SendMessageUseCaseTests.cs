using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Application.UseCases;
using Baiss.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Baiss.Tests.UseCases;

public class SendMessageUseCaseTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IAssistantService> _assistantServiceMock;
    private readonly Mock<ICaptureScreenService> _captureScreenServiceMock;
    private readonly SendMessageUseCase _sendMessageUseCase;

    public SendMessageUseCaseTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _assistantServiceMock = new Mock<IAssistantService>();
        _captureScreenServiceMock = new Mock<ICaptureScreenService>();

        // Setup default behavior for message repository
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Message>());

        // Setup default behavior for assistant service
        _assistantServiceMock.Setup(x => x.IsReady).Returns(true);
        _assistantServiceMock.Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<List<MessageItem>?>()))
            .ReturnsAsync(new ContentResponse
            {
                Status = 200,
                Success = true,
                Error = null,
                Response = new ResponseData
                {
                    Messages = new List<MessageItem>
                    {
                        new MessageItem
                        {
                            Role = "assistant",
                            Content = new List<ContentItem>
                            {
                                new ContentItem { Text = "Test assistant response" }
                            }
                        }
                    }
                },
                Usage = new UsageData { InputTokens = 10, OutputTokens = 10, TotalTokens = 20 },
                Sources = new List<SourceItem>(), // Empty sources list for tests
                // Dashboard = new List<object>(),
                StopReason = "end_turn"
            });

        // Setup streaming response for the streaming use case
        _assistantServiceMock.Setup(x => x.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<List<MessageItem>?>()))
            .Returns(GenerateTestStreamingResponse());

        _sendMessageUseCase = new SendMessageUseCase(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _assistantServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyContent_ShouldReturnFailure()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "",
            ConversationId = null
        };

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Message cannot be empty");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContent_ShouldReturnFailure()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = null!,
            ConversationId = null
        };

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Message cannot be empty");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceContent_ShouldReturnFailure()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "   ",
            ConversationId = null
        };

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Message cannot be empty");
    }

    [Fact]
    public async Task ExecuteAsync_WithNewConversation_ShouldCreateConversationAndMessage()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "Hello, how are you?",
            ConversationId = null
        };

        _conversationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => c);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Conversation { Id = id, Title = "Hello, how are you?", CreatedByUserId = "user", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.IsNewConversation.Should().BeTrue();
        result.ConversationId.Should().NotBe(Guid.Empty);
        result.MessageId.Should().NotBe(Guid.Empty);
        result.ConversationTitle.Should().Be("Hello, how are you?");
        result.ErrorMessage.Should().BeNull();

        // Verify that the methods have been called
        _conversationRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Conversation>()), Times.Once);
        _messageRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Message>()), Times.Exactly(2)); // User message + Assistant message
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyGuidConversationId_ShouldCreateNewConversation()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "Test message",
            ConversationId = Guid.Empty
        };

        _conversationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => c);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Conversation { Id = id, Title = "Test message", CreatedByUserId = "user", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.IsNewConversation.Should().BeTrue();
        result.ConversationTitle.Should().Be("Test message");

        _conversationRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Conversation>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingConversation_ShouldAddMessageToConversation()
    {
        // Arrange
        var existingConversationId = Guid.NewGuid();
        var existingConversation = new Conversation
        {
            Id = existingConversationId,
            Title = "Existing conversation",
            CreatedByUserId = "user",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var dto = new SendMessageDto
        {
            Content = "New message in the conversation",
            ConversationId = existingConversationId
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(existingConversationId))
            .ReturnsAsync(existingConversation);

        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.IsNewConversation.Should().BeFalse();
        result.ConversationId.Should().Be(existingConversationId);
        result.ConversationTitle.Should().Be("Existing conversation");
        result.MessageId.Should().NotBe(Guid.Empty);

        // Verify that the correct methods have been called
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(existingConversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Conversation>()), Times.Never);
        _messageRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Message>()), Times.Exactly(2)); // User message + Assistant message
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentConversation_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentConversationId = Guid.NewGuid();
        var dto = new SendMessageDto
        {
            Content = "Message for non-existent conversation",
            ConversationId = nonExistentConversationId
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(nonExistentConversationId))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Conversation not found");

        // Verify that no message was created
        _messageRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Message>()), Times.Never);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "Test message",
            ConversationId = null
        };

        _conversationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Conversation>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Error");
    }

    [Fact]
    public async Task ExecuteAsync_WithLongMessage_ShouldTruncateTitle()
    {
        // Arrange
        var longMessage = "This is a very long message that far exceeds the 20 character limit to test title truncation";
        var dto = new SendMessageDto
        {
            Content = longMessage,
            ConversationId = null
        };

        _conversationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => c);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Conversation { Id = id, Title = "This is a very long...", CreatedByUserId = "user", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ConversationTitle.Should().Be("This is a very long...");
        result.ConversationTitle.Length.Should().BeLessThanOrEqualTo(23); // 20 characters + "..."
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateUserMessageWithCorrectProperties()
    {
        // Arrange
        var dto = new SendMessageDto
        {
            Content = "Test message content",
            ConversationId = null
        };

        Message? capturedUserMessage = null;
        var messageCreationCount = 0;
        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m)
            .Callback<Message>(msg =>
            {
                messageCreationCount++;
                if (messageCreationCount == 1) // First message is the user message
                {
                    capturedUserMessage = msg;
                }
            });

        _conversationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => c);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Conversation { Id = id, Title = "Test message content", CreatedByUserId = "user", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        capturedUserMessage.Should().NotBeNull();
        capturedUserMessage!.Content.Should().Be("Test message content");
        capturedUserMessage.SenderType.Should().Be(SenderType.USER);
        capturedUserMessage.Id.Should().NotBe(Guid.Empty);
        capturedUserMessage.ConversationId.Should().NotBe(Guid.Empty);
        capturedUserMessage.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateConversationTimestamp()
    {
        // Arrange
        var existingConversationId = Guid.NewGuid();
        var oldTimestamp = DateTime.UtcNow.AddHours(-2);
        var existingConversation = new Conversation
        {
            Id = existingConversationId,
            Title = "Test Conversation",
            CreatedByUserId = "user",
            CreatedAt = oldTimestamp,
            UpdatedAt = oldTimestamp
        };

        var dto = new SendMessageDto
        {
            Content = "New message",
            ConversationId = existingConversationId
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(existingConversationId))
            .ReturnsAsync(existingConversation);

        Conversation? capturedConversation = null;
        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Callback<Conversation>(conv => capturedConversation = conv)
            .Returns(Task.CompletedTask);

        _messageRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        // Act
        var result = await GetFinalResultFromStreamingAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();

        capturedConversation.Should().NotBeNull();
        capturedConversation!.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        capturedConversation.UpdatedAt.Should().BeAfter(oldTimestamp);
    }

    /// <summary>
    /// Generates a test streaming response
    /// </summary>
    private async IAsyncEnumerable<string> GenerateTestStreamingResponse()
    {
        yield return "Test";
        yield return " assistant";
        yield return " response";
        await Task.CompletedTask; // Required for async enumerable
    }

    /// <summary>
    /// Helper method to get the final result from streaming response for testing
    /// </summary>
    private async Task<SendMessageResponseDto> GetFinalResultFromStreamingAsync(SendMessageDto dto)
    {
        await foreach (var (textChunk, finalResult) in _sendMessageUseCase.ExecuteStreamingAsync(dto))
        {
            if (finalResult != null)
            {
                return finalResult;
            }
        }
        throw new InvalidOperationException("No final result received from streaming response");
    }
}
