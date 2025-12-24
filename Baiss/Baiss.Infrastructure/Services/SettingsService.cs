using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Jobs;
// using Baiss.Infrastructure.Interop;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.IO;


namespace Baiss.Infrastructure.Services;

/// <summary>
/// Service for managing application settings
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IExternalApiService _externalApiService;
    private readonly ILogger<SettingsService> _logger;
    private readonly ITreeStructureService _treeStructureService;
    // private readonly IEmbeddingsService _embeddingsService;
    // private readonly IPythonBridgeService _pythonBridgeService;
    private readonly IModelRepository _modelRepository;
    private readonly ILaunchServerService _launchServerService;
    private readonly IJobSchedulerService _jobSchedulerService;

    private CancellationTokenSource _cancellationTokenSource;

    private Task _thr;
    private string _updating = "";
    private volatile bool _pauseRequested;



    private readonly IAvailableModelRepository _availableModelRepository;

    public SettingsService(ISettingsRepository settingsRepository, IExternalApiService externalApiService, ILogger<SettingsService> logger, ITreeStructureService treeStructureService, IModelRepository modelRepository, ILaunchServerService launchServerService, IAvailableModelRepository availableModelRepository, IJobSchedulerService jobSchedulerService, CancellationTokenSource cancellationTokenSource = null, Task thr = null)
    {
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _externalApiService = externalApiService ?? throw new ArgumentNullException(nameof(externalApiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _treeStructureService = treeStructureService ?? throw new ArgumentNullException(nameof(treeStructureService));
        _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
        _launchServerService = launchServerService ?? throw new ArgumentNullException(nameof(launchServerService));
        _availableModelRepository = availableModelRepository ?? throw new ArgumentNullException(nameof(availableModelRepository));
        _jobSchedulerService = jobSchedulerService ?? throw new ArgumentNullException(nameof(jobSchedulerService));
        _thr = thr;
        _cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        _pauseRequested = false;
    }

    /// <summary>
    /// Maps Settings entity to SettingsDto
    /// </summary>
    /// <param name="settings">The settings entity to map</param>
    /// <returns>Mapped SettingsDto</returns>
    private static SettingsDto MapToDto(Settings settings, List<string>? existingModels = null)
    {
        {
            return new SettingsDto
            {
                Performance = settings.Performance,
                AllowedPaths = settings.AllowedPaths,
                AllowedApplications = settings.AllowedApplications,
                AppVersion = settings.AppVersion,
                AllowedFileExtensions = settings.AllowedFileExtensions,
                EnableAutoUpdate = settings.EnableAutoUpdate,
                AllowFileReading = settings.AllowFileReading,
                AllowUpdateCreatedFiles = settings.AllowUpdateCreatedFiles,
                AllowCreateNewFiles = settings.AllowCreateNewFiles,
                NewFilesSavePath = settings.NewFilesSavePath,
                ExistingModels = existingModels ?? new List<string>(),
                AIModelType = settings.AIModelType,
                AIModelProviderScope = settings.AIModelProviderScope,
                AIChatModelId = settings.AIChatModelId,
                AIEmbeddingModelId = settings.AIEmbeddingModelId,
                HuggingFaceApiKey = settings.HuggingfaceApiKey,
                TreeStructureSchedule = settings.TreeStructureSchedule,
                TreeStructureScheduleEnabled = settings.TreeStructureScheduleEnabled,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            };
        }
    }

    /// <summary>
    /// Retrieves application settings
    /// </summary>
    /// <returns>Application settings or null</returns>
    public async Task<SettingsDto?> GetSettingsAsync()
    {
        try
        {


            var settings = await _settingsRepository.GetAsync();
            if (settings == null) return null;
            //var existingModels = await _externalApiService.GetListModelAsync();
            //var existingModelIds = existingModels.Data.Downloads.Select(m => m.ModelId).ToList() ?? new List<string>();

            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving settings: {ex.Message}", ex);
        }
    }

    public async Task<SettingsGeneralDtos?> UpdateGeneralSettingsAsync(UpdateGeneralSettingsDto generalSettingsDto)
    {

        if (generalSettingsDto == null)
        {
            throw new ArgumentNullException(nameof(generalSettingsDto), "General settings data cannot be null");
        }

        if (generalSettingsDto.NeedUpdate)
        {
            _updating = "";
            var settings = await _settingsRepository.GetAsync();
            bool hasChanges = false;


            _ = Task.Run(async () =>
            {
                try
                {
                    bool updateResult = await _externalApiService.baiss_update();
                    if (!updateResult)
                    {
                        _logger.LogWarning("Baiss update process reported failure.");
                        _updating = "FAILED";
                        throw new InvalidOperationException("Baiss update process failed.");
                    }
                    var updateInfo = await _externalApiService.CheckForUpdatesAsync();
                    if (updateInfo?.CurrentVersion != null && !string.IsNullOrEmpty(updateInfo.CurrentVersion))
                    {
                        settings.AppVersion = updateInfo.CurrentVersion;
                        hasChanges = true;
                    }
                    if (hasChanges)
                    {
                        settings.UpdatedAt = DateTime.UtcNow;
                        await _settingsRepository.SaveAsync(settings);
                    }
                    _updating = "FINISHED";
                    _logger.LogInformation("Update check completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for updates in background thread");
                }
            });
            var generalSetting = new SettingsGeneralDtos
            {
                AppVersion = "Updating...",
            };
            return generalSetting;


        }

        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null) return null;
            var currentVersion = settings.AppVersion ?? "";

            bool hasChanges = false;

            if (settings.Performance != generalSettingsDto.Performance)
            {
                settings.Performance = generalSettingsDto.Performance;
                hasChanges = true;
            }

            if (settings.EnableAutoUpdate != generalSettingsDto.EnableAutoUpdate)
            {
                settings.EnableAutoUpdate = generalSettingsDto.EnableAutoUpdate;
                hasChanges = true;
            }

            if (generalSettingsDto.CheckUpdate)
            {
                _updating = "";
                var updateInfo = await _externalApiService.CheckForUpdatesAsync(currentVersion);

                if (updateInfo?.CurrentVersion != null && !string.IsNullOrEmpty(updateInfo.CurrentVersion))
                {
                    currentVersion = updateInfo.CurrentVersion;
                    if (settings.AppVersion == currentVersion)
                    {
                        // settings.AppVersion = currentVersion;
                        // hasChanges = true;
                        _updating = "FINISHED";
                    }
                    else
                    {
                        var ret = new SettingsGeneralDtos
                        {
                            AppVersion = currentVersion,
                        };
                        return ret;
                    }
                }
            }

            // Only save if something actually changed
            if (hasChanges)
            {
                settings.UpdatedAt = DateTime.UtcNow;
                await _settingsRepository.SaveAsync(settings);
            }

            var generalSetting = new SettingsGeneralDtos
            {
                Performance = settings.Performance,
                EnableAutoUpdate = settings.EnableAutoUpdate,
                AppVersion = settings.AppVersion,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            };
            return generalSetting;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving settings: {ex.Message}", ex);
        }
    }

    public async Task<SettingsDto?> UpdateTreeStructureScheduleAsync(UpdateTreeStructureScheduleDto scheduleDto)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null) return null;

            settings.TreeStructureSchedule = scheduleDto.Schedule;
            settings.TreeStructureScheduleEnabled = scheduleDto.Enabled;
            settings.UpdatedAt = DateTime.UtcNow;

            await _settingsRepository.SaveAsync(settings);

            // Update the running job
            if (scheduleDto.Enabled)
            {
                // Cancel existing job first to be safe
                await _jobSchedulerService.CancelJobAsync("tree-structure-update-job");

                // Schedule with new cron expression
                await _jobSchedulerService.ScheduleRecurringJobAsync<UpdateTreeStructureJob>(
                    "tree-structure-update-job",
                    scheduleDto.Schedule
                );
                _logger.LogInformation("Rescheduled tree structure update job with cron: {Cron}", scheduleDto.Schedule);
            }
            else
            {
                await _jobSchedulerService.CancelJobAsync("tree-structure-update-job");
                _logger.LogInformation("Cancelled tree structure update job");
            }

            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tree structure schedule settings");
            return null;
        }
    }

    /// <summary>
    /// Updates AI permissions settings
    /// </summary>
    /// <param name="permissionsDto">The permissions data to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    public async Task<SettingsDto?> UpdateAiPermissionsAsync(UpdateAiPermissionsDto permissionsDto)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                return null;
            }

            List<string> newPathsAdd = new List<string>();
            List<string> newPathsDell = new List<string>();
            List<string> newExtensionsDell = new List<string>();
            List<string> newExtensionsAdd = new List<string>();



            // Update basic permission settings
            settings.AllowFileReading = permissionsDto.AllowFileReading;
            settings.AllowUpdateCreatedFiles = permissionsDto.AllowUpdateCreatedFiles;
            settings.AllowCreateNewFiles = permissionsDto.AllowCreateNewFiles;

            // Update new files save path if provided
            if (!string.IsNullOrWhiteSpace(permissionsDto.NewFilesSavePath))
            {
                settings.NewFilesSavePath = permissionsDto.NewFilesSavePath;
            }

            // Check if allowedPaths contains data
            if (permissionsDto.AllowedPaths != null && permissionsDto.AllowedPaths.Any())
            {
                // Get existing paths from database
                var existingPaths = settings.AllowedPaths ?? new List<string>();
                // Process all paths in a single loop
                foreach (var pathDto in permissionsDto.AllowedPaths)
                {
                    if (string.IsNullOrWhiteSpace(pathDto.Path)) continue;

                    _logger.LogInformation("Processing path: {Path} -- - -> > {IsValid}", pathDto.Path, pathDto.IsValid);

                    if (!pathDto.IsValid)
                    {
                        // Remove path if it's invalid (case insensitive comparison)
                        newPathsDell.Add(pathDto.Path);
                        existingPaths.RemoveAll(existingPath => string.Equals(existingPath, pathDto.Path, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        // Add path if it's valid and doesn't already exist (case insensitive comparison)
                        if (!existingPaths.Any(existingPath => string.Equals(existingPath, pathDto.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            newPathsAdd.Add(pathDto.Path);
                            existingPaths.Add(pathDto.Path);
                        }
                    }
                }
                // Update the allowed paths with the modified list
                settings.AllowedPaths = existingPaths;
            }

            // Check if allowedFileExtensions contains data
            if (permissionsDto.AllowedFileExtensions != null && permissionsDto.AllowedFileExtensions.Any())
            {
                // Get existing extensions from database
                var existingExtensions = settings.AllowedFileExtensions ?? new List<string>();
                // Process all extensions in a single loop
                foreach (var extensionDto in permissionsDto.AllowedFileExtensions)
                {
                    if (string.IsNullOrWhiteSpace(extensionDto.Extension)) continue;

                    if (!extensionDto.IsValid)
                    {
                        newExtensionsDell.Add(extensionDto.Extension);
                        existingExtensions.RemoveAll(existingExtension => string.Equals(existingExtension, extensionDto.Extension, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        // Add extension if it's valid and doesn't already exist (case insensitive comparison)
                        if (!existingExtensions.Any(existingExtension => string.Equals(existingExtension, extensionDto.Extension, StringComparison.OrdinalIgnoreCase)))
                        {
                            existingExtensions.Add(extensionDto.Extension);
                            newExtensionsAdd.Add(extensionDto.Extension);
                        }
                    }
                }
                // Update the allowed extensions with the modified list
                settings.AllowedFileExtensions = existingExtensions;
            }

            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.SaveAsync(settings);

            ProcessTreeStructureUpdatesAsync(newExtensionsDell, newPathsDell, newPathsAdd, newExtensionsAdd, settings.AllowedPaths ?? new List<string>(), settings.AllowedFileExtensions ?? new List<string>());
            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error updating AI permissions: {ex.Message}", ex);
        }
    }

    public void RefreshTreeStructure()
    {
        _thr = Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsRepository.GetAsync();
                if (settings == null)
                {
                    _logger.LogWarning("Cannot refresh tree structure: Settings not found");
                    return;
                }

                await _treeStructureService.UpdateTreeStructureAsync(settings.AllowedPaths ?? new List<string>(), settings.AllowedFileExtensions ?? new List<string>(), _cancellationTokenSource.Token);
                _logger.LogInformation("Tree structure refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing tree structure");
            }
        });
    }

    /// <summary>
    /// Processes tree structure updates asynchronously in background tasks
    /// </summary>
    /// <param name="extensionsToDelete">List of extensions to delete from tree structure</param>
    /// <param name="pathsToDelete">List of paths to delete from tree structure</param>
    /// <param name="pathsToAdd">List of paths to add to tree structure</param>
    /// <param name="allowedExtensions">List of allowed file extensions</param>
    private void ProcessTreeStructureUpdatesAsync(List<string> extensionsToDelete, List<string> pathsToDelete, List<string> pathsToAdd, List<string> newExtensionsAdd, List<string> allowedPaths, List<string> allowedExtensions)
    {
        // Delete extensions if any
        if (extensionsToDelete.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _treeStructureService.DeleteFromTreeStructureWithExtensionsAsync(extensionsToDelete);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning tree structure after extension deletion");
                }
            });
        }

        // Delete paths if any
        if (pathsToDelete.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _treeStructureService.DeleteFromTreeStructureAsync(pathsToDelete);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning tree structure after path deletion");
                }
            });
        }


        // Add paths if any
        if (newExtensionsAdd.Count > 0)
        {
            if (_pauseRequested)
            {
                _logger.LogInformation("Background operations are paused; deferring tree structure update for {PathCount} paths", newExtensionsAdd.Count);
                return;
            }

            _thr = Task.Run(async () =>
            {
                try
                {
                    // Step 1: Build tree structure first
                    _logger.LogInformation("Starting tree structure generation for {PathCount} paths", newExtensionsAdd.Count);
                    await _treeStructureService.UpdateTreeStructureAsync(newExtensionsAdd, allowedPaths, _cancellationTokenSource.Token);
                    _logger.LogInformation("Tree structure generation completed successfully");

                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Tree structure and embedding generation was cancelled");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Server is offline"))
                {
                    _logger.LogWarning("Tree structure update skipped: Server is offline");
                    // Don't log as error since this is an expected condition
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating tree structure or generating embeddings after path addition");
                }
            });
        }


        if (pathsToAdd.Count > 0)
        {
            if (_pauseRequested)
            {
                _logger.LogInformation("Background operations are paused; deferring tree structure update for {PathCount} paths", pathsToAdd.Count);
                return;
            }

            _thr = Task.Run(async () =>
            {
                try
                {
                    // Step 1: Build tree structure first
                    _logger.LogInformation("Starting tree structure generation for {PathCount} paths", pathsToAdd.Count);
                    await _treeStructureService.UpdateTreeStructureAsync(pathsToAdd, allowedExtensions, _cancellationTokenSource.Token);
                    _logger.LogInformation("Tree structure generation completed successfully");


                    // Step 2: Generate embeddings for new content
                    // _logger.LogInformation("Starting automatic embedding generation for new paths");

                    // Initialiser EmbeddingPipeline avec les services nécessaires
                    // Baiss.Infrastructure.Services.AI.EmbeddingPipeline.Initialize(
                    //     _launchServerService,
                    //     _settingsRepository,
                    //     _modelRepository,
                    //     _embeddingsService);

                    // await Baiss.Infrastructure.Services.AI.EmbeddingPipeline.RunAsync(
                    //     _logger,
                    //     _embeddingsService,
                    //     _pythonBridgeService,
                    //     _cancellationTokenSource.Token);
                    // _logger.LogInformation("Automatic embedding generation completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Tree structure and embedding generation was cancelled");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Server is offline"))
                {
                    _logger.LogWarning("Tree structure update skipped: Server is offline");
                    // Don't log as error since this is an expected condition
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating tree structure or generating embeddings after path addition");
                }
            });
        }
    }


    /// <summary>
    /// Updates AI model settings
    /// </summary>
    /// <param name="aiModelDto">The AI model settings to update</param>
    /// <returns>Updated settings DTO if successful, null if failed</returns>
    public async Task<SettingsDto?> UpdateAIModelSettingsAsync(UpdateAIModelSettingsDto aiModelDto)
    {
        try
        {
            _logger.LogInformation("Updating AI model settings - Type: {Type}, ChatModelId: {ChatModelId}, EmbeddingModelId: {EmbeddingModelId}",
                aiModelDto.AIModelType, aiModelDto.AIChatModelId, aiModelDto.AIEmbeddingModelId);

            // Get existing settings from database
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                _logger.LogInformation("Settings not found, creating new settings with AI model configuration");
                // Create new settings if none exist
                settings = new Settings
                {
                    Id = "app-settings-global",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            // Update AI model properties
            settings.AIModelType = aiModelDto.AIModelType;
            if (!string.IsNullOrWhiteSpace(aiModelDto.AIModelProviderScope))
            {
                settings.AIModelProviderScope = aiModelDto.AIModelProviderScope;
            }


            // Update (or clear) separate chat and embedding model IDs
            bool chatModelChanged = false;
            bool embeddingModelChanged = false;
            if (aiModelDto.AIChatModelId != null)
            {
                var oldChat = settings.AIChatModelId;
                settings.AIChatModelId = string.IsNullOrWhiteSpace(aiModelDto.AIChatModelId) ? string.Empty : aiModelDto.AIChatModelId;

                chatModelChanged = oldChat != settings.AIChatModelId;

                _logger.LogInformation("Chat model ID {Action}: {Value}", string.IsNullOrEmpty(settings.AIChatModelId) ? "cleared" : (oldChat == settings.AIChatModelId ? "unchanged" : "updated"), settings.AIChatModelId);
            }

            if (aiModelDto.AIEmbeddingModelId != null)
            {
                var oldEmb = settings.AIEmbeddingModelId;
                settings.AIEmbeddingModelId = string.IsNullOrWhiteSpace(aiModelDto.AIEmbeddingModelId) ? string.Empty : aiModelDto.AIEmbeddingModelId;
                embeddingModelChanged = oldEmb != settings.AIEmbeddingModelId;
                _logger.LogInformation("Embedding model ID {Action}: {Value}", string.IsNullOrEmpty(settings.AIEmbeddingModelId) ? "cleared" : (oldEmb == settings.AIEmbeddingModelId ? "unchanged" : "updated"), settings.AIEmbeddingModelId);
            }

            if (aiModelDto.HuggingFaceApiKey != null)
            {
                settings.HuggingfaceApiKey = aiModelDto.HuggingFaceApiKey;
            }


            settings.UpdatedAt = DateTime.UtcNow;

            // Save updated settings
            await _settingsRepository.SaveAsync(settings);

            // Restart llama-cpp server if chat model changed and it's a local model
            if (chatModelChanged && settings.AIModelType == ModelTypes.Local && !string.IsNullOrEmpty(settings.AIChatModelId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Restarting llama-cpp server with new chat model: {ModelId}", settings.AIChatModelId);

                        // Stop existing server if running using the centralized method
                        _logger.LogInformation("Stopping existing llama-cpp server process");
                        await _launchServerService.StopServerByTypeAsync("chat");

                        // Get the model path from repository
                        var modelPath = await _modelRepository.GetPathByModelIdAsync(settings.AIChatModelId);
                        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                        {
                            _logger.LogInformation("Launching llama-cpp server with model path: {ModelPath}", modelPath);

                            // Launch new server with the updated model
                            var newProcess = await _launchServerService.LaunchLlamaCppServerAsync("chat", modelPath);
                            if (newProcess != null)
                            {
                                _logger.LogInformation("llama-cpp server restarted successfully with new model");
                            }
                            else
                            {
                                _logger.LogError("Failed to restart llama-cpp server with new model");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Model path not found or file does not exist for model: {ModelId}", settings.AIChatModelId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error restarting llama-cpp server with new model");
                    }
                });
            }
            // Restart llama-cpp server if chat model changed and it's a local model embedding
            if (embeddingModelChanged && settings.AIModelType == ModelTypes.Local && !string.IsNullOrEmpty(settings.AIEmbeddingModelId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Restarting llama-cpp server with new embedding model: {ModelId}", settings.AIEmbeddingModelId);

                        // Stop existing server if running using the centralized method
                        _logger.LogInformation("Stopping existing llama-cpp server process");
                        await _launchServerService.StopServerByTypeAsync("embedding");

                        // Get the model path from repository
                        var modelPath = await _modelRepository.GetPathByModelIdAsync(settings.AIEmbeddingModelId);
                        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                        {
                            _logger.LogInformation("Launching llama-cpp server with model path: {ModelPath}", modelPath);

                            // Launch new server with the updated model
                            var newProcess = await _launchServerService.LaunchLlamaCppServerAsync("embedding", modelPath, "--embeddings");
                            if (newProcess != null)
                            {
                                _logger.LogInformation("llama-cpp server restarted successfully with new model");
                            }
                            else
                            {
                                _logger.LogError("Failed to restart llama-cpp server with new model");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Model path not found or file does not exist for model: {ModelId}", settings.AIChatModelId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error restarting llama-cpp server with new model");
                    }
                });
            }

            _logger.LogInformation("Successfully updated AI model settings");
            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AI model settings");
            return null;
        }
    }

    /// <summary>
    /// Minimal update just for AIModelProviderScope (used when user toggles radio buttons before picking models)
    /// </summary>
    public async Task<bool> UpdateAIModelProviderScopeAsync(string scope)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                settings = new Settings();
            }

            var oldScope = settings.AIModelProviderScope;
            settings.AIModelProviderScope = scope;

            // Keep AIModelType coherent with provider scope so runtime routing/logs are consistent
            // local -> local; hosted/databricks -> hosted
            settings.AIModelType = string.Equals(scope, ModelTypes.Local, StringComparison.OrdinalIgnoreCase)
                ? ModelTypes.Local
                : ModelTypes.Hosted;

            // Clear chat and embedding model selections when changing provider scope
            // User needs to select new models appropriate for the new scope
            settings.AIChatModelId = string.Empty;
            settings.AIEmbeddingModelId = string.Empty;

            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.SaveAsync(settings);
            _logger.LogInformation("AIModelProviderScope persisted: {Scope}; AIModelType set to {Type}; Chat and Embedding models cleared", scope, settings.AIModelType);

            // Handle llama-cpp server based on provider scope change
            bool isLocal = string.Equals(scope, ModelTypes.Local, StringComparison.OrdinalIgnoreCase);
            bool wasLocal = string.Equals(oldScope, ModelTypes.Local, StringComparison.OrdinalIgnoreCase);

            if (wasLocal && !isLocal)
            {
                // Switching from local to hosted - stop llama-cpp server
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Switching to hosted model provider, stopping llama-cpp server");
                        // await _launchServerService.StopCurrentServerAsync();
                        _logger.LogInformation("llama-cpp server stopped successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping llama-cpp server when switching to hosted provider");
                    }
                });
            }
            else if (!wasLocal && isLocal)
            {
                // Switching from hosted to local - start llama-cpp server (if a model is already configured)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Switching to local model provider, checking if llama-cpp server needs to be started");

                        // Check if there's a chat model configured to start the server with
                        var currentSettings = await _settingsRepository.GetAsync();
                        if (currentSettings != null && !string.IsNullOrEmpty(currentSettings.AIChatModelId))
                        {
                            var modelPath = await _modelRepository.GetPathByModelIdAsync(currentSettings.AIChatModelId);
                            if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                            {
                                _logger.LogInformation("Starting llama-cpp server with model: {ModelId}", currentSettings.AIChatModelId);
                                var process = await _launchServerService.LaunchLlamaCppServerAsync("chat", modelPath);

                                if (process != null)
                                {
                                    _logger.LogInformation("llama-cpp server started successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to start llama-cpp server");
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Model path not found or file does not exist, llama-cpp server not started");
                            }
                        }
                        if (currentSettings != null && !string.IsNullOrEmpty(currentSettings.AIEmbeddingModelId))
                        {
                            var modelPath = await _modelRepository.GetPathByModelIdAsync(currentSettings.AIEmbeddingModelId);
                            if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                            {
                                _logger.LogInformation("Starting llama-cpp server with model: {ModelId}", currentSettings.AIEmbeddingModelId);
                                var process = await _launchServerService.LaunchLlamaCppServerAsync("embedding", modelPath, " --embeddings");
                                if (process != null)
                                {
                                    _logger.LogInformation("llama-cpp server started successfully");
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to start llama-cpp server");
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Model path not found or file does not exist, llama-cpp server not started");
                            }
                        }


                        else
                        {
                            _logger.LogInformation("No chat model configured yet, llama-cpp server will be started when model is selected");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error starting llama-cpp server when switching to local provider");
                    }
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist AIModelProviderScope: {Scope}", scope);
            return false;
        }
    }

    /// <summary>
    /// Stops all background operations by cancelling the cancellation token
    /// </summary>
    public void StopBackgroundOperations()
    {
        try
        {

            _externalApiService.CancelTree();

            _pauseRequested = true;

            _cancellationTokenSource.Cancel();

            var currentTask = _thr;
            if (currentTask != null && !currentTask.IsCompleted)
            {
                _ = currentTask.ContinueWith(task =>
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        _logger.LogError(task.Exception, "Tree structure task faulted while stopping background operations");
                    }
                }, TaskScheduler.Default);
            }

            _thr = null;

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling background operations");
        }
    }

    public void ResumeBackgroundOperations()
    {
        _pauseRequested = false;
    }


    public Task<bool> CheckTreeStructureThread()
    {
        try
        {
            return Task.FromResult(!_pauseRequested && _thr != null && !_thr.IsCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tree structure thread status");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Resets the cancellation token source for new operations
    /// </summary>
    public void ResetCancellationToken()
    {
        try
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _logger.LogInformation("Cancellation token reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting cancellation token");
        }
    }

    /// <summary>
    /// Checks if the update check thread is still running
    /// </summary>
    public string CheckUpdateCheckThread()
    {
        return _updating;
    }


    public async Task<List<ModelInfo>> DownloadAvailableModelsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching available models from external API");

            // Fetch models from external API
            var models = await _externalApiService.DownloadAvailableModelsAsync();

            if (models == null || !models.Any())
            {
                _logger.LogWarning("No models retrieved from external API");
            }

            _logger.LogInformation("Retrieved {Count} models, saving to database", models.Count);

            // Get all downloaded models from Models table to check which are already downloaded
            var downloadedModels = await _modelRepository.GetAllModelsAsync();
            var downloadedModelIds = downloadedModels
                .Where(m => m.IsActive && string.Equals(m.Type, ModelTypes.Local, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Found {Count} downloaded models in Models table", downloadedModelIds.Count);

            // Convert ModelInfo DTOs to AvailableModel entities and save to database
            var availableModels = models.Select(m =>
            {
                var jsonString = JsonSerializer.Serialize(m);
                bool isDownloaded = downloadedModelIds.Contains(m.ModelId);

                if (isDownloaded)
                {
                    _logger.LogDebug("Model '{ModelId}' is already downloaded, marking IsDownloaded = true", m.ModelId);
                }

                return new AvailableModel
                {
                    Id = m.ModelId,
                    IsDownloaded = isDownloaded,
                    IsValid = true,
                    Metadata = JsonDocument.Parse(jsonString), // Store as JsonDocument
                    UpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
            }).ToList();

            // Bulk upsert to database
            await _availableModelRepository.BulkUpsertAsync(availableModels);

            _logger.LogInformation("Successfully saved {Count} models to database", availableModels.Count);

            // Remove models that are no longer in the API response AND are not downloaded
            try
            {
                var savedModels = await _availableModelRepository.GetAllAsync();
                var apiModelIds = models.Select(m => m.ModelId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var modelsToDelete = savedModels
                    .Where(dbModel => !apiModelIds.Contains(dbModel.Id) && !dbModel.IsDownloaded && dbModel.IsValid)
                    .ToList();

                if (modelsToDelete.Any())
                {
                    _logger.LogInformation("Removing {Count} models that are no longer in API and not downloaded", modelsToDelete.Count);

                    foreach (var modelToDelete in modelsToDelete)
                    {
                        await _availableModelRepository.DeleteAsync(modelToDelete.Id);
                        _logger.LogDebug("Deleted model '{ModelId}' from AvailableModels (not in API, not downloaded)", modelToDelete.Id);
                    }

                    // Refresh savedModels after deletion
                    savedModels = await _availableModelRepository.GetAllAsync();
                }
                else
                {
                    _logger.LogDebug("No models to delete from AvailableModels table");
                }

                if (savedModels != null && savedModels.Any())
                {
                    _logger.LogInformation("Returning {Count} models from database after save", savedModels.Count());

                    // Create a dictionary for quick lookup of saved models
                    var savedModelsDict = savedModels.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

                    // Start with models from API in the order they were returned
                    var modelInfoList = new List<ModelInfo>();

                    foreach (var apiModel in models)
                    {
                        if (savedModelsDict.TryGetValue(apiModel.ModelId, out var dbModel))
                        {
                            try
                            {
                                var jsonString = dbModel.Metadata.RootElement.GetRawText();
                                var modelInfo = JsonSerializer.Deserialize<ModelInfo>(jsonString);
                                if (modelInfo != null)
                                {
                                    modelInfoList.Add(modelInfo);
                                }
                            }
                            catch (Exception deserializeEx)
                            {
                                _logger.LogWarning(deserializeEx, "Failed to deserialize model metadata for {ModelId}", dbModel.Id);
                            }
                        }
                    }

                    // Add downloaded models that are not in API response at the top
                    // Also include manually added models (IsValid = false) that are not in the API response
                    var modelsNotInApi = savedModels
                        .Where(m => !apiModelIds.Contains(m.Id) && (m.IsDownloaded || !m.IsValid))
                        .OrderBy(m => m.CreatedAt)
                        .Select(m =>
                        {
                            try
                            {
                                var jsonString = m.Metadata.RootElement.GetRawText();
                                return JsonSerializer.Deserialize<ModelInfo>(jsonString);
                            }
                            catch (Exception deserializeEx)
                            {
                                _logger.LogWarning(deserializeEx, "Failed to deserialize model metadata for {ModelId}", m.Id);
                                return null;
                            }
                        })
                        .Where(m => m != null)
                        .Select(m => m!)
                        .ToList();

                    // Combine: downloaded models not in API first, then models from API (in API order)
                    return modelsNotInApi.Concat(modelInfoList).ToList();
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error cleaning up models not in API response");
                // Continue execution - this is not critical
            }

            // Retrieve and return models from database instead of external API response

            _logger.LogWarning("Failed to retrieve saved models from database, returning external API response as fallback");
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching and saving available models: {Message}", ex.Message);

            // Try to return models from database as fallback
            try
            {
                var dbModels = await _availableModelRepository.GetAllAsync();
                if (dbModels != null && dbModels.Any())
                {
                    _logger.LogInformation("Returning {Count} models from database as fallback", dbModels.Count());

                    var modelInfoList = dbModels
                        .OrderBy(m => m.CreatedAt)
                        .Select(m =>
                        {
                            try
                            {
                                var jsonString = m.Metadata.RootElement.GetRawText();
                                return JsonSerializer.Deserialize<ModelInfo>(jsonString);
                            }
                            catch
                            {
                                return null;
                            }
                        })
                        .Where(m => m != null)
                        .Select(m => m!)
                        .ToList();

                    return modelInfoList;
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Error retrieving models from database as fallback");
            }

            return new List<ModelInfo>();
        }
    }

    public Task<StartModelsDownloadResponse> StartModelDownloadAsync(string modelId, string? downloadUrl = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id cannot be empty", nameof(modelId));
        }
        return _externalApiService.StartModelDownloadAsync(modelId, downloadUrl);
    }

    public Task<ModelDownloadListResponse> GetModelDownloadListAsync()
    {
        return _externalApiService.GetModelDownloadListAsync();
    }

    public Task<StopModelDownloadResponse> StopModelDownloadAsync(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process id cannot be empty", nameof(processId));
        }
        return _externalApiService.StopModelDownloadAsync(processId);
    }

    public Task<ModelDownloadProgressResponse> GetModelDownloadProgressAsync(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process id cannot be empty", nameof(processId));
        }
        return _externalApiService.GetModelDownloadProgressAsync(processId);
    }

    public Task<ModelsListResponse> GetModelsListExistsAsync()
    {
        // ! herr
        return _externalApiService.GetModelsListExistsAsync();


    }
    public async Task<ModelsListResponse> GetModelsListExistWIthCheackDbAsync()
    {

        // ! change heeer
        var externalResponse = await _externalApiService.GetModelsListExistsAsync();

        if (!externalResponse.Success || externalResponse.Data == null)
        {
            _logger.LogWarning("External API returned unsuccessful response or no data");
            return externalResponse;
        }

        try
        {
            // Get all existing models from database
            var dbModels = await _modelRepository.GetAllModelsAsync();
            var dbModelIds = dbModels.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Get model IDs from external API
            var externalModelIds = externalResponse.Data.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Step 1: Add or update models from external API
            foreach (var kvp in externalResponse.Data)
            {
                var modelId = kvp.Key;
                var modelInfo = kvp.Value;

                // Check if model exists in database
                var existingModel = await _modelRepository.GetModelByIdAsync(modelId);

                if (existingModel == null)
                {
                    // Model doesn't exist, create new one
                    _logger.LogInformation("Adding new model '{ModelId}' to database", modelId);

                    // check if windows and set provider accordingly\
                    var localPath = modelInfo.Entypoint;
                    if (OperatingSystem.IsWindows())
                    {
                        localPath = modelInfo.Entypoint.Replace("/", "\\");
                    }

                    var newModel = new Model
                    {
                        Id = modelId,
                        Name = modelId, // Use modelId as name, can be updated later if needed
                        Type = ModelTypes.Local, // Models from external API are local
                        Provider = ModelProviders.Python, // Assuming Python provider for local models
                        Purpose = modelId.ToLower().Contains("embedding") ? "embedding" : "chat",
                        LocalPath = localPath,
                        IsActive = string.Equals(modelInfo.Status, "completed", StringComparison.OrdinalIgnoreCase),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _modelRepository.AddModelAsync(newModel);
                    _logger.LogInformation("Successfully added model '{ModelId}' to database", modelId);
                }
                else
                {
                    // Model exists, update it if needed
                    bool needsUpdate = false;

                    if (existingModel.LocalPath != modelInfo.Entypoint)
                    {
                        existingModel.LocalPath = modelInfo.Entypoint;
                        needsUpdate = true;
                    }

                    bool isActive = string.Equals(modelInfo.Status, "completed", StringComparison.OrdinalIgnoreCase);
                    if (existingModel.IsActive != isActive)
                    {
                        existingModel.IsActive = isActive;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        existingModel.UpdatedAt = DateTime.UtcNow;
                        await _modelRepository.UpdateModelAsync(existingModel);
                        _logger.LogInformation("Updated existing model '{ModelId}' in database", modelId);
                    }
                    else
                    {
                        _logger.LogDebug("Model '{ModelId}' already exists in database and is up to date", modelId);
                    }
                }
            }

            // Step 2: Remove models from DB that don't exist in external API (only local models)
            // DISABLED: Do not automatically delete models from DB just because they're not in API response
            // Models should only be deleted via explicit user action through DeleteModelAsync
            // foreach (var dbModel in dbModels)
            // {
            //     // Only check local models managed by external API
            //     if (string.Equals(dbModel.Type, ModelTypes.Local, StringComparison.OrdinalIgnoreCase) &&
            //         !externalModelIds.Contains(dbModel.Id))
            //     {
            //         _logger.LogInformation("Removing model '{ModelId}' from database as it no longer exists in external API", dbModel.Id);
            //         await _modelRepository.DeleteModelAsync(dbModel.Id);
            //         _logger.LogInformation("Successfully removed model '{ModelId}' from database", dbModel.Id);
            //     }
            // }

            // Step 3: Check settings and remove models if they don't exist in external API
            var settings = await _settingsRepository.GetAsync();
            if (settings != null && settings.AIModelType == ModelTypes.Local)
            {
                bool settingsChanged = false;

                // Check AIChatModelId
                if (!string.IsNullOrEmpty(settings.AIChatModelId) && !externalModelIds.Contains(settings.AIChatModelId))
                {
                    _logger.LogInformation("Removing AIChatModelId '{ModelId}' from settings as it no longer exists in external API", settings.AIChatModelId);
                    settings.AIChatModelId = string.Empty;
                    settingsChanged = true;
                }

                // Check AIEmbeddingModelId
                if (!string.IsNullOrEmpty(settings.AIEmbeddingModelId) && !externalModelIds.Contains(settings.AIEmbeddingModelId))
                {
                    _logger.LogInformation("Removing AIEmbeddingModelId '{ModelId}' from settings as it no longer exists in external API", settings.AIEmbeddingModelId);
                    settings.AIEmbeddingModelId = string.Empty;
                    settingsChanged = true;
                }

                if (settingsChanged)
                {
                    settings.UpdatedAt = DateTime.UtcNow;
                    await _settingsRepository.SaveAsync(settings);
                    _logger.LogInformation("Settings updated after model synchronization");
                }
            }

            _logger.LogInformation("Database synchronization with external API completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing models between external API and database");
        }

        return externalResponse;
    }



    public async Task<bool> DeleteModelAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id cannot be empty", nameof(modelId));
        }

        try
        {
            // Attempt to delete the model via external API (file system)
            bool deleteResult = false;
            try
            {
                deleteResult = await _externalApiService.DeleteModelAsync(modelId);
                _logger.LogInformation("Delete model API call result for modelId '{ModelId}': {Result}", modelId, deleteResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete model '{ModelId}' from file system via external API", modelId);
            }

            // Clean up: Remove from database entities and settings configuration
            // This method handles both database entity deletion and settings cleanup
            await RemoveModelFromSettingsIfConfiguredAsync(modelId);

            return deleteResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelId}: {Message}", modelId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Removes the model from Settings and Database if it was configured as AIChatModelId or AIEmbeddingModelId
    /// </summary>
    /// <param name="modelId">The model ID that was deleted</param>
    private async Task RemoveModelFromSettingsIfConfiguredAsync(string modelId)
    {
        try
        {
            // Step 1: Delete from database if it exists
            try
            {
                var modelExists = await _modelRepository.ModelExistsAsync(modelId);
                if (modelExists)
                {
                    await _modelRepository.DeleteModelAsync(modelId);
                    _logger.LogInformation("Successfully deleted model '{ModelId}' from database entities", modelId);
                }
                else
                {
                    _logger.LogDebug("Model '{ModelId}' does not exist in database entities, skipping entity deletion", modelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model '{ModelId}' from database entities", modelId);
                // Continue to settings cleanup even if database deletion fails
            }

            // Step 2: Clean up settings
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                _logger.LogDebug("No settings found, nothing to update after model deletion");
                return;
            }

            bool settingsChanged = false;

            // Check if the deleted model was configured as the chat model
            if (!string.IsNullOrEmpty(settings.AIChatModelId) &&
                string.Equals(settings.AIChatModelId, modelId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Clearing AIChatModelId '{ModelId}' from settings as model was deleted", settings.AIChatModelId);
                settings.AIChatModelId = string.Empty;
                settingsChanged = true;
            }

            // Check if the deleted model was configured as the embedding model
            if (!string.IsNullOrEmpty(settings.AIEmbeddingModelId) &&
                string.Equals(settings.AIEmbeddingModelId, modelId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Clearing AIEmbeddingModelId '{ModelId}' from settings as model was deleted", settings.AIEmbeddingModelId);
                settings.AIEmbeddingModelId = string.Empty;
                settingsChanged = true;
            }

            // Save settings only if something was changed
            if (settingsChanged)
            {
                settings.UpdatedAt = DateTime.UtcNow;
                await _settingsRepository.SaveAsync(settings);
                _logger.LogInformation("Settings updated after model deletion: AIChatModelId='{ChatModelId}', AIEmbeddingModelId='{EmbeddingModelId}'",
                    settings.AIChatModelId, settings.AIEmbeddingModelId);
            }
            else
            {
                _logger.LogDebug("No settings changes needed after deleting model '{ModelId}'", modelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings after model deletion for modelId '{ModelId}': {Message}", modelId, ex.Message);
            // Don't throw - the model deletion itself was successful, this is just cleanup
        }
    }

    public Task<ModelInfoResponse> GetModelInfoAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id cannot be empty", nameof(modelId));
        }
        return _externalApiService.GetModelInfoAsync(modelId);
    }

    public async Task RestartServerAsync(string modelType)
    {
        var settings = await _settingsRepository.GetAsync();
        if (settings == null) return;

        string? modelId = modelType == "chat" ? settings.AIChatModelId : settings.AIEmbeddingModelId;
        if (string.IsNullOrEmpty(modelId)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Restarting llama-cpp server for {ModelType} with model: {ModelId}", modelType, modelId);

                await _launchServerService.StopServerByTypeAsync(modelType);

                var modelPath = await _modelRepository.GetPathByModelIdAsync(modelId);
                if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                {
                    await _launchServerService.LaunchLlamaCppServerAsync(modelType, modelPath);
                }
                else
                {
                    _logger.LogWarning("Cannot restart server: Model path not found or file does not exist for model: {ModelId}", modelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting llama-cpp server for {ModelType}", modelType);
            }
        });
    }

    private async Task<string?> GetHuggingFaceApiKeyAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        return settings?.HuggingfaceApiKey ?? null;
    }

    public async Task<ModelDetailsResponseDto> SearchAndSaveExternalModelAsync(string modelId)
    {
        try
        {
            var token = await GetHuggingFaceApiKeyAsync();

            var response = await _externalApiService.GetExternalModelDetailsAsync(modelId, token);
            if (!response.Success || response.Data == null) return response;

            JsonElement rootElement;
            if (response.Data is JsonElement element)
            {
                rootElement = element;
            }
            else
            {
                try
                {
                    var json = JsonSerializer.Serialize(response.Data);
                    using var doc = JsonDocument.Parse(json);
                    rootElement = doc.RootElement.Clone();
                }
                catch
                {
                    return new ModelDetailsResponseDto { Success = false, Error = "Failed to parse model data" };
                }
            }

            bool savedAny = false;

            // Handle Array of models
            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in rootElement.EnumerateArray())
                {
                    if (await SaveModelFromElement(item))
                    {
                        savedAny = true;
                    }
                }
            }
            // Handle Single Object
            else if (rootElement.ValueKind == JsonValueKind.Object)
            {
                if (await SaveModelFromElement(rootElement))
                {
                    savedAny = true;
                }
            }

            if (savedAny)
            {
                return new ModelDetailsResponseDto { Success = true, Message = "Model saved successfully" };
            }
            else
            {
                return new ModelDetailsResponseDto { Success = false, Error = "Model found but failed to save (might already exist)" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching and saving external model {ModelId}", modelId);
            return new ModelDetailsResponseDto { Success = false, Error = ex.Message };
        }
    }

    private async Task<bool> SaveModelFromElement(JsonElement element)
    {
        try
        {
            if (!element.TryGetProperty("model_id", out var idProp)) return false;
            var id = idProp.GetString();
            if (string.IsNullOrEmpty(id)) return false;

            var existingModel = await _availableModelRepository.GetByIdAsync(id);

            var availableModel = new AvailableModel
            {
                Id = id,
                IsDownloaded = existingModel?.IsDownloaded ?? false,
                IsValid = existingModel?.IsValid ?? false,
                Metadata = JsonDocument.Parse(element.GetRawText()),
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = existingModel?.CreatedAt ?? DateTime.UtcNow
            };

            if (existingModel != null)
            {
                await _availableModelRepository.UpdateAsync(availableModel);
            }
            else
            {
                await _availableModelRepository.AddAsync(availableModel);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving model element");
            return false;
        }
    }
}
