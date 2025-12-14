using Baiss.Application.Interfaces;
using Baiss.Application.UseCases;
using Baiss.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Baiss.Tests.UseCases;

public class ConversationManagementUseCaseTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<ILogger<ConversationManagementUseCase>> _loggerMock;
    private readonly ConversationManagementUseCase _conversationManagementUseCase;

    public ConversationManagementUseCaseTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _loggerMock = new Mock<ILogger<ConversationManagementUseCase>>();

        _conversationManagementUseCase = new ConversationManagementUseCase(
            _conversationRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region RenameTitleAsync Tests

    [Fact]
    public async Task RenameTitleAsync_WithValidData_ShouldReturnTrue()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var newTitle = "New Conversation Title";
        var existingConversation = new Conversation
        {
            Id = conversationId,
            Title = "Old Title",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync(existingConversation);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, newTitle);

        // Assert
        result.Should().BeTrue();
        existingConversation.Title.Should().Be(newTitle);
        existingConversation.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(conversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(existingConversation), Times.Once);
    }

    [Fact]
    public async Task RenameTitleAsync_WithEmptyTitle_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var emptyTitle = "";

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, emptyTitle);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task RenameTitleAsync_WithNullTitle_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        string nullTitle = null!;

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, nullTitle);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task RenameTitleAsync_WithWhitespaceTitle_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var whitespaceTitle = "   ";

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, whitespaceTitle);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task RenameTitleAsync_WithNonExistentConversation_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var newTitle = "New Title";

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, newTitle);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(conversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task RenameTitleAsync_WithTitleContainingWhitespace_ShouldTrimTitle()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var titleWithWhitespace = "  New Conversation Title  ";
        var expectedTitle = "New Conversation Title";
        var existingConversation = new Conversation
        {
            Id = conversationId,
            Title = "Old Title",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync(existingConversation);

        _conversationRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, titleWithWhitespace);

        // Assert
        result.Should().BeTrue();
        existingConversation.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public async Task RenameTitleAsync_WhenRepositoryThrowsException_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var newTitle = "New Title";

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _conversationManagementUseCase.RenameTitleAsync(conversationId, newTitle);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteConversationAsync Tests

    [Fact]
    public async Task DeleteConversationAsync_WithValidId_ShouldReturnTrue()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var existingConversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync(existingConversation);

        _conversationRepositoryMock
            .Setup(x => x.DeleteAsync(conversationId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _conversationManagementUseCase.DeleteConversationAsync(conversationId);

        // Assert
        result.Should().BeTrue();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(conversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.DeleteAsync(conversationId), Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _conversationManagementUseCase.DeleteConversationAsync(conversationId);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(conversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteConversationAsync_WhenGetByIdThrowsException_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _conversationManagementUseCase.DeleteConversationAsync(conversationId);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteConversationAsync_WhenDeleteThrowsException_ShouldReturnFalse()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var existingConversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId))
            .ReturnsAsync(existingConversation);

        _conversationRepositoryMock
            .Setup(x => x.DeleteAsync(conversationId))
            .ThrowsAsync(new Exception("Delete operation failed"));

        // Act
        var result = await _conversationManagementUseCase.DeleteConversationAsync(conversationId);

        // Assert
        result.Should().BeFalse();
        _conversationRepositoryMock.Verify(x => x.GetByIdAsync(conversationId), Times.Once);
        _conversationRepositoryMock.Verify(x => x.DeleteAsync(conversationId), Times.Once);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new ConversationManagementUseCase(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("conversationRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new ConversationManagementUseCase(_conversationRepositoryMock.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var useCase = new ConversationManagementUseCase(_conversationRepositoryMock.Object, _loggerMock.Object);

        // Assert
        useCase.Should().NotBeNull();
    }

    #endregion
}
