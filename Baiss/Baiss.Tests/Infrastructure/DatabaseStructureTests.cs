using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Baiss.Infrastructure.Db;
using Baiss.Infrastructure.Interfaces;
using System.Data.SQLite;
using System.Data;

namespace Baiss.Tests.Infrastructure;

public class DatabaseStructureTests : IDisposable
{
    private readonly SQLiteConnection _connection;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DatabaseValidator _validator;
    private readonly Mock<ILogger<DatabaseValidator>> _loggerMock;

    public DatabaseStructureTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();

        var connectionFactoryMock = new Mock<IDbConnectionFactory>();
        connectionFactoryMock.Setup(x => x.CreateConnection()).Returns(_connection);
        _connectionFactory = connectionFactoryMock.Object;

        _loggerMock = new Mock<ILogger<DatabaseValidator>>();
        _validator = new DatabaseValidator(_connectionFactory, _loggerMock.Object);
    }

    [Fact]
    public async Task ValidateSettingsTableStructure_WithAllColumns_ShouldReturnTrue()
    {
        // Arrange - Create complete Settings table
        await CreateCompleteSettingsTableAsync();

        // Act
        var result = await _validator.ValidateSettingsTableStructureAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateSettingsTableStructure_WithMissingColumns_ShouldReturnFalse()
    {
        // Arrange - Create incomplete Settings table (like the original migration)
        const string createTableSql = @"
            CREATE TABLE Settings (
                Performance INTEGER NOT NULL DEFAULT 0,
                AllowedPaths TEXT NOT NULL DEFAULT '[]',
                AllowedApplications TEXT NOT NULL DEFAULT '[]',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            )";

        using var command = new SQLiteCommand(createTableSql, _connection);
        await command.ExecuteNonQueryAsync();

        // Act
        var result = await _validator.ValidateSettingsTableStructureAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnsureDefaultFileExtensions_WithEmptyExtensions_ShouldUpdateToDefaults()
    {
        // Arrange
        await CreateCompleteSettingsTableAsync();

        // Insert a record with empty file extensions
        const string insertSql = @"
            INSERT INTO Settings (Performance, AllowedPaths, AllowedApplications, CreatedAt, AllowedFileExtensions, AppVersion, EnableAutoUpdate, AllowFileReading, AllowUpdateCreatedFiles, AllowCreateNewFiles, NewFilesSavePath, AIModelType, AIModelId)
            VALUES (0, '[]', '[]', datetime('now'), '[]', '1.0.0', 1, 0, 0, 0, '', 'local', '')";

        using var insertCommand = new SQLiteCommand(insertSql, _connection);
        await insertCommand.ExecuteNonQueryAsync();

        // Act
        var result = await _validator.EnsureDefaultFileExtensionsAsync();

        // Assert
        Assert.True(result);

        // Verify the extensions were updated
        const string selectSql = "SELECT AllowedFileExtensions FROM Settings";
        using var selectCommand = new SQLiteCommand(selectSql, _connection);
        var extensions = await selectCommand.ExecuteScalarAsync() as string;

        Assert.NotNull(extensions);
        Assert.Contains("doc", extensions);
        Assert.Contains("pdf", extensions);
        Assert.NotEqual("[]", extensions);
    }

    private async Task CreateCompleteSettingsTableAsync()
    {
        const string createTableSql = @"
            CREATE TABLE Settings (
                Performance INTEGER NOT NULL DEFAULT 0,
                AllowedPaths TEXT NOT NULL DEFAULT '[]',
                AllowedApplications TEXT NOT NULL DEFAULT '[]',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT,
                AppVersion TEXT NOT NULL DEFAULT '1.0.0',
                EnableAutoUpdate INTEGER NOT NULL DEFAULT 1,
                AllowFileReading INTEGER NOT NULL DEFAULT 0,
                AllowUpdateCreatedFiles INTEGER NOT NULL DEFAULT 0,
                AllowCreateNewFiles INTEGER NOT NULL DEFAULT 0,
                NewFilesSavePath TEXT NOT NULL DEFAULT '',
                AllowedFileExtensions TEXT NOT NULL DEFAULT '[""doc"",""docx"",""xls"",""xlsx"",""pdf"",""txt"",""csv""]',
                AIModelType TEXT NOT NULL DEFAULT 'local',
                AIModelId TEXT NOT NULL DEFAULT ''
            )";

        using var command = new SQLiteCommand(createTableSql, _connection);
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
