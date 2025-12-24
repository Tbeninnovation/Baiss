using Baiss.Application.DTOs;
using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Service interface for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Retrieves application settings
    /// </summary>
    /// <returns>Application settings or null</returns>
    Task<SettingsDto?> GetSettingsAsync();


    /// <summary>
    /// Updates AI permissions settings
    /// </summary>
    /// <param name="permissionsDto">The permissions data to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    Task<SettingsDto?> UpdateAiPermissionsAsync(UpdateAiPermissionsDto permissionsDto);

    /// <summary>
    /// Updates general application settings
    /// </summary>
    /// <param name="generalSettingsDto">The general settings data to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    Task<SettingsGeneralDtos?> UpdateGeneralSettingsAsync(UpdateGeneralSettingsDto generalSettingsDto);

    /// <summary>
    /// Updates AI model settings
    /// </summary>
    /// <param name="aiModelDto">The AI model settings to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    Task<SettingsDto?> UpdateAIModelSettingsAsync(UpdateAIModelSettingsDto aiModelDto);
    Task<bool> UpdateAIModelProviderScopeAsync(string scope);

    /// <summary>
    /// Updates tree structure schedule settings
    /// </summary>
    /// <param name="scheduleDto">The schedule settings to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    Task<SettingsDto?> UpdateTreeStructureScheduleAsync(UpdateTreeStructureScheduleDto scheduleDto);

    /// <summary>
    /// Stops any ongoing background operations, such as tree structure updates.
    /// </summary>
    void StopBackgroundOperations();

    /// <summary>
    /// Searches for an external model and saves it to available models if found
    /// </summary>
    /// <param name="modelId">The model ID to search for</param>
    /// <param name="token">Optional API token</param>
    /// <returns>The result of the operation</returns>
    Task<ModelDetailsResponseDto> SearchAndSaveExternalModelAsync(string modelId);

    /// <summary>
    /// Allows paused background operations to run again.
    /// </summary>
    void ResumeBackgroundOperations();

    Task<bool> CheckTreeStructureThread();

    Task<List<ModelInfo>> DownloadAvailableModelsAsync();

    Task<StartModelsDownloadResponse> StartModelDownloadAsync(string modelId, string? downloadUrl = null);
    Task<ModelDownloadListResponse> GetModelDownloadListAsync();
    Task<StopModelDownloadResponse> StopModelDownloadAsync(string processId);
    Task<ModelDownloadProgressResponse> GetModelDownloadProgressAsync(string processId);

    Task<ModelsListResponse> GetModelsListExistsAsync();
    Task<bool> DeleteModelAsync(string modelId);

    Task<ModelInfoResponse> GetModelInfoAsync(string modelId);
    // Task<string> GetUpdateInfoAsync();
    Task<ModelsListResponse> GetModelsListExistWIthCheackDbAsync();

    string CheckUpdateCheckThread();

    public void RefreshTreeStructure();

    Task RestartServerAsync(string modelType);

}
