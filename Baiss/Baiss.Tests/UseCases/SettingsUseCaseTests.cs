using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Application.UseCases;
using Baiss.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Baiss.Tests.UseCases;

public class SettingsUseCaseTests
{
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IModelRepository> _modelRepositoryMock;
    private readonly SettingsUseCase _settingsUseCase;

    public SettingsUseCaseTests()
    {
        _settingsServiceMock = new Mock<ISettingsService>();
        _modelRepositoryMock = new Mock<IModelRepository>();
        _settingsUseCase = new SettingsUseCase(_settingsServiceMock.Object, _modelRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new SettingsUseCase(null!, _modelRepositoryMock.Object));
        exception.ParamName.Should().Be("settingsService");
    }

    #endregion

    #region GetSettingsUseCaseAsync Tests

    [Fact]
    public async Task GetSettingsUseCaseAsync_WithExistingSettings_ReturnsSettingsDto()
    {
        // Arrange
        var settingsDto = new SettingsDto
        {
            Performance = PerformanceLevel.Medium,
            AllowedPaths = new List<string> { "C:\\Test", "D:\\Documents" },
            AllowedApplications = new List<string> { "notepad.exe", "calculator.exe" },
            CreatedAt = new DateTime(2023, 1, 1),
            UpdatedAt = new DateTime(2023, 2, 1)
        };

        _settingsServiceMock.Setup(x => x.GetSettingsAsync())
            .ReturnsAsync(settingsDto);

        // Act
        var result = await _settingsUseCase.GetSettingsUseCaseAsync();

        // Assert
        result.Should().NotBeNull();
        result.Performance.Should().Be(PerformanceLevel.Medium);
        result.AllowedPaths.Should().BeEquivalentTo(new List<string> { "C:\\Test", "D:\\Documents" });
        result.AllowedApplications.Should().BeEquivalentTo(new List<string> { "notepad.exe", "calculator.exe" });
        result.CreatedAt.Should().Be(new DateTime(2023, 1, 1));
        result.UpdatedAt.Should().Be(new DateTime(2023, 2, 1));
    }

    [Fact]
    public async Task GetSettingsUseCaseAsync_WithNoSettings_ThrowsInvalidOperationException()
    {
        // Arrange
        _settingsServiceMock.Setup(x => x.GetSettingsAsync())
            .ReturnsAsync((SettingsDto?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _settingsUseCase.GetSettingsUseCaseAsync());
        exception.Message.Should().Contain("Settings not found. Please initialize settings first.");
    }

    [Fact]
    public async Task GetSettingsUseCaseAsync_WhenServiceThrows_ThrowsException()
    {
        // Arrange
        _settingsServiceMock.Setup(x => x.GetSettingsAsync())
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _settingsUseCase.GetSettingsUseCaseAsync());
        exception.Message.Should().Be("Service error");
    }

    #endregion

    #region UpdateAiPermissionsAsync Tests

    [Fact]
    public async Task UpdateAiPermissionsAsync_WithValidPermissions_ReturnsUpdatedSettings()
    {
        // Arrange
        var permissionsDto = new UpdateAiPermissionsDto
        {
            AllowFileReading = true,
            AllowCreateNewFiles = false,
            AllowUpdateCreatedFiles = true,
            NewFilesSavePath = "C:\\NewFiles"
        };

        var expectedResult = new SettingsDto
        {
            Performance = PerformanceLevel.Medium,
            AllowCreateNewFiles = false,
            AllowUpdateCreatedFiles = true,
            NewFilesSavePath = "C:\\NewFiles"
        };

        _settingsServiceMock.Setup(x => x.UpdateAiPermissionsAsync(permissionsDto))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _settingsUseCase.UpdateAiPermissionsAsync(permissionsDto);

        // Assert
        result.Should().NotBeNull();
        result.AllowCreateNewFiles.Should().Be(false);
        result.AllowUpdateCreatedFiles.Should().Be(true);
        result.NewFilesSavePath.Should().Be("C:\\NewFiles");
        _settingsServiceMock.Verify(x => x.UpdateAiPermissionsAsync(permissionsDto), Times.Once);
    }

    [Fact]
    public async Task UpdateAiPermissionsAsync_WhenServiceReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var permissionsDto = new UpdateAiPermissionsDto();
        _settingsServiceMock.Setup(x => x.UpdateAiPermissionsAsync(permissionsDto))
            .ReturnsAsync((SettingsDto?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _settingsUseCase.UpdateAiPermissionsAsync(permissionsDto));
        exception.Message.Should().Contain("Failed to update AI permissions. Settings not found or operation failed.");
    }

    #endregion

    // TODO: Uncomment when CreateAIPermissionsAsync is implemented in ISettingsService and SettingsUseCase
    /*
    #region CreateAIPermissionsAsync Tests

    [Fact]
    public async Task CreateAIPermissionsAsync_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        var allowFileReading = true;
        var allowedPaths = new List<string> { "C:\\Test" };
        var allowUpdateCreatedFiles = false;
        var allowCreateNewFiles = true;
        var newFilesSavePath = "C:\\NewFiles";
        var allowedFileExtensions = new List<string> { ".txt", ".doc" };

        _settingsServiceMock.Setup(x => x.CreateAIPermissionsAsync(
            allowFileReading, allowedPaths, allowUpdateCreatedFiles,
            allowCreateNewFiles, newFilesSavePath, allowedFileExtensions))
            .ReturnsAsync(true);

        // Act
        var result = await _settingsUseCase.CreateAIPermissionsAsync(
            allowFileReading, allowedPaths, allowUpdateCreatedFiles,
            allowCreateNewFiles, newFilesSavePath, allowedFileExtensions);

        // Assert
        result.Should().BeTrue();
        _settingsServiceMock.Verify(x => x.CreateAIPermissionsAsync(
            allowFileReading, allowedPaths, allowUpdateCreatedFiles,
            allowCreateNewFiles, newFilesSavePath, allowedFileExtensions), Times.Once);
    }

    [Fact]
    public async Task CreateAIPermissionsAsync_WhenServiceReturnsFalse_ReturnsFalse()
    {
        // Arrange
        _settingsServiceMock.Setup(x => x.CreateAIPermissionsAsync(
            It.IsAny<bool>(), It.IsAny<List<string>>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(false);

        // Act
        var result = await _settingsUseCase.CreateAIPermissionsAsync(
            true, new List<string>(), false, true, "", new List<string>());

        // Assert
        result.Should().BeFalse();
    }

    #endregion
    */

    #region GetAvailableModelAsync Tests

    [Fact]
    public async Task GetAvailableModelAsync_ReturnsModelFromService()
    {
        // Arrange
        var expectedModel = "GPT-4";
        _settingsServiceMock.Setup(x => x.GetAvailableModelAsync())
            .ReturnsAsync(expectedModel);

        // Act
        var result = await _settingsUseCase.GetAvailableModelAsync();

        // Assert
        result.Should().Be(expectedModel);
        _settingsServiceMock.Verify(x => x.GetAvailableModelAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAvailableModelAsync_WhenServiceReturnsEmpty_ReturnsEmpty()
    {
        // Arrange
        _settingsServiceMock.Setup(x => x.GetAvailableModelAsync())
            .ReturnsAsync("");

        // Act
        var result = await _settingsUseCase.GetAvailableModelAsync();

        // Assert
        result.Should().Be("");
    }

    #endregion
}
