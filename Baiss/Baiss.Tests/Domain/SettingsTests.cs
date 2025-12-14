using Baiss.Domain.Entities;
using Xunit;

namespace Baiss.Tests.Domain;

public class SettingsTests
{
    [Fact]
    public void Settings_ShouldHaveDefaultFileExtensions_WhenCreated()
    {
        // Arrange & Act
        var settings = new Settings();

        // Assert
        Assert.NotNull(settings.AllowedFileExtensions);
        Assert.NotEmpty(settings.AllowedFileExtensions);

        // Verify all expected default extensions are present
        var expectedExtensions = new[] { "docx", "xls", "xlsx", "pdf", "txt", "csv", "md" };
        foreach (var extension in expectedExtensions)
        {
            Assert.Contains(extension, settings.AllowedFileExtensions);
        }
    }

    [Fact]
    public void GetDefaultAllowedFileExtensions_ShouldReturnExpectedExtensions()
    {
        // Act
        var defaultExtensions = Settings.GetDefaultAllowedFileExtensions();

        // Assert
        Assert.NotNull(defaultExtensions);
        Assert.Equal(7, defaultExtensions.Count);

        var expectedExtensions = new[] { "docx", "xls", "xlsx", "pdf", "txt", "csv", "md" };
        foreach (var extension in expectedExtensions)
        {
            Assert.Contains(extension, defaultExtensions);
        }
    }

    [Fact]
    public void Settings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new Settings();

        // Assert
        Assert.Equal("app-settings-global", settings.Id);
        Assert.Equal(PerformanceLevel.Small, settings.Performance);
        Assert.False(settings.AllowFileReading);
        Assert.False(settings.AllowUpdateCreatedFiles);
        Assert.False(settings.AllowCreateNewFiles);
        Assert.True(settings.EnableAutoUpdate);
        Assert.Equal("1.0.0", settings.AppVersion);
        Assert.Equal(ModelTypes.Local, settings.AIModelType);
        Assert.Empty(settings.AIModelId);
        Assert.NotNull(settings.AllowedPaths);
        Assert.Empty(settings.AllowedPaths);
        Assert.NotNull(settings.AllowedApplications);
        Assert.Empty(settings.AllowedApplications);
    }
}
