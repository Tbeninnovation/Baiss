using System.Data;
using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Baiss.Application.Interfaces;
using System.Text.Json;

namespace Baiss.Infrastructure.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private const string AppSettingsId = "app-settings-global";

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Settings?> GetAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Performance, AllowedPaths, AllowedApplications, CreatedAt, UpdatedAt, AppVersion , EnableAutoUpdate, AllowFileReading, AllowUpdateCreatedFiles, AllowCreateNewFiles, NewFilesSavePath, AllowedFileExtensions, AIModelType, AIModelProviderScope, AIChatModelId, AIEmbeddingModelId, TreeStructureSchedule, TreeStructureScheduleEnabled, HuggingfaceApiKey
            FROM Settings";
        // WHERE Id = @Id";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = AppSettingsId });

        if (result == null) return null;

        return new Settings
        {
            Performance = (PerformanceLevel)result.Performance,
            AllowedPaths = JsonSerializer.Deserialize<List<string>>(result.AllowedPaths) ?? new List<string>(),
            AllowedApplications = JsonSerializer.Deserialize<List<string>>(result.AllowedApplications) ?? new List<string>(),
            AppVersion = result.AppVersion,
            EnableAutoUpdate = Convert.ToBoolean(result.EnableAutoUpdate),
            AllowFileReading = Convert.ToBoolean(result.AllowFileReading),
            AllowUpdateCreatedFiles = Convert.ToBoolean(result.AllowUpdateCreatedFiles),
            AllowCreateNewFiles = Convert.ToBoolean(result.AllowCreateNewFiles),
            NewFilesSavePath = result.NewFilesSavePath,
            AllowedFileExtensions = JsonSerializer.Deserialize<List<string>>(result.AllowedFileExtensions) ?? new List<string>(),
            AIModelType = result.AIModelType,
            AIModelProviderScope = (result.AIModelProviderScope is string scope && !string.IsNullOrWhiteSpace(scope)) ? scope : "local",
            AIChatModelId = result.AIChatModelId ?? string.Empty,
            AIEmbeddingModelId = result.AIEmbeddingModelId ?? string.Empty,
            TreeStructureSchedule = result.TreeStructureSchedule ?? "0 0 0 * * ?",
            TreeStructureScheduleEnabled = Convert.ToBoolean(result.TreeStructureScheduleEnabled),
            HuggingfaceApiKey = result.HuggingfaceApiKey ?? string.Empty,
            CreatedAt = DateTime.Parse(result.CreatedAt),
            UpdatedAt = result.UpdatedAt != null ? DateTime.Parse(result.UpdatedAt) : null
        };
    }

    public async Task SaveAsync(Settings settings)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Check if settings already exist
        var existingSettings = await GetAsync();

        var allowedPathsJson = JsonSerializer.Serialize(settings.AllowedPaths);
        var allowedAppsJson = JsonSerializer.Serialize(settings.AllowedApplications);
        var allowedFileExtensionsJson = JsonSerializer.Serialize(settings.AllowedFileExtensions);

        if (existingSettings != null)
        {
            // Update existing settings
            const string updateSql = @"
                UPDATE Settings
                SET Performance = @Performance,
                    AllowedPaths = @AllowedPaths,
                    AllowedApplications = @AllowedApplications,
                    UpdatedAt = @UpdatedAt,
                    AppVersion = @AppVersion,
                    EnableAutoUpdate = @EnableAutoUpdate,
                    AllowFileReading = @AllowFileReading,
                    AllowUpdateCreatedFiles = @AllowUpdateCreatedFiles,
                    AllowCreateNewFiles = @AllowCreateNewFiles,
                    NewFilesSavePath = @NewFilesSavePath,
                    AllowedFileExtensions = @AllowedFileExtensions,
                    AIModelType = @AIModelType,
                    AIModelProviderScope = @AIModelProviderScope,
                    AIChatModelId = @AIChatModelId,
                    AIEmbeddingModelId = @AIEmbeddingModelId,
                    TreeStructureSchedule = @TreeStructureSchedule,
                    TreeStructureScheduleEnabled = @TreeStructureScheduleEnabled,
                    HuggingfaceApiKey = @HuggingfaceApiKey";

            await connection.ExecuteAsync(updateSql, new
            {
                Performance = (int)settings.Performance,
                AllowedPaths = allowedPathsJson,
                AllowedApplications = allowedAppsJson,
                AppVersion = settings.AppVersion,
                EnableAutoUpdate = settings.EnableAutoUpdate,
                AllowFileReading = settings.AllowFileReading,
                AllowUpdateCreatedFiles = settings.AllowUpdateCreatedFiles,
                AllowCreateNewFiles = settings.AllowCreateNewFiles,
                NewFilesSavePath = settings.NewFilesSavePath,
                AllowedFileExtensions = allowedFileExtensionsJson,
                AIModelType = settings.AIModelType,
                AIModelProviderScope = settings.AIModelProviderScope,
                AIChatModelId = settings.AIChatModelId,
                AIEmbeddingModelId = settings.AIEmbeddingModelId,
                TreeStructureSchedule = settings.TreeStructureSchedule,
                TreeStructureScheduleEnabled = settings.TreeStructureScheduleEnabled,
                HuggingfaceApiKey = settings.HuggingfaceApiKey,
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        else
        {
            // Insert new settings
            const string insertSql = @"
                INSERT INTO Settings (Performance, AllowedPaths, AllowedApplications, AppVersion, EnableAutoUpdate, AllowFileReading, AllowUpdateCreatedFiles, AllowCreateNewFiles, NewFilesSavePath, AllowedFileExtensions, AIModelType, AIModelProviderScope, AIChatModelId, AIEmbeddingModelId, TreeStructureSchedule, TreeStructureScheduleEnabled, HuggingfaceApiKey, CreatedAt, UpdatedAt)
                VALUES (@Performance, @AllowedPaths, @AllowedApplications, @AppVersion, @EnableAutoUpdate, @AllowFileReading, @AllowUpdateCreatedFiles, @AllowCreateNewFiles, @NewFilesSavePath, @AllowedFileExtensions, @AIModelType, @AIModelProviderScope, @AIChatModelId, @AIEmbeddingModelId, @TreeStructureSchedule, @TreeStructureScheduleEnabled, @HuggingfaceApiKey, @CreatedAt, @UpdatedAt)";

            await connection.ExecuteAsync(insertSql, new
            {
                Performance = (int)settings.Performance,
                AllowedPaths = allowedPathsJson,
                AllowedApplications = allowedAppsJson,
                AppVersion = settings.AppVersion,
                EnableAutoUpdate = settings.EnableAutoUpdate,
                AllowFileReading = settings.AllowFileReading,
                AllowUpdateCreatedFiles = settings.AllowUpdateCreatedFiles,
                AllowCreateNewFiles = settings.AllowCreateNewFiles,
                NewFilesSavePath = settings.NewFilesSavePath,
                AllowedFileExtensions = allowedFileExtensionsJson,
                AIModelType = settings.AIModelType,
                AIModelProviderScope = settings.AIModelProviderScope,
                AIChatModelId = settings.AIChatModelId,
                AIEmbeddingModelId = settings.AIEmbeddingModelId,
                TreeStructureSchedule = settings.TreeStructureSchedule,
                TreeStructureScheduleEnabled = settings.TreeStructureScheduleEnabled,
                HuggingfaceApiKey = settings.HuggingfaceApiKey,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }

    public async Task<string> GetModelIdByTypeAsync(string modelType)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT AIChatModelId, AIEmbeddingModelId
            FROM Settings";

        var result = await connection.QueryFirstOrDefaultAsync(sql);

        if (result == null) return string.Empty;

        return modelType.ToLower() switch
        {
            "chat" => result.AIChatModelId ?? string.Empty,
            "embedding" => result.AIEmbeddingModelId ?? string.Empty,
            _ => string.Empty
        };
    }

}
