using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;

namespace Baiss.Application.UseCases;

/// <summary>
/// Use case for retrieving application settings
/// </summary>
public class SettingsUseCase
{
	private readonly ISettingsService _settingsService;
	private readonly IModelRepository _modelRepository;

	public SettingsUseCase(ISettingsService settingsService, IModelRepository modelRepository)
	{
		_settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
		_modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
	}

	/// <summary>
	/// Retrieves application settings
	/// </summary>
	/// <returns>Application settings</returns>
	/// <exception cref="InvalidOperationException">Thrown when settings are not found</exception>
	public async Task<SettingsDto> GetSettingsUseCaseAsync()
	{
		var result = await _settingsService.GetSettingsAsync();
		if (result == null)
		{
			throw new InvalidOperationException("Settings not found. Please initialize settings first.");
		}
		return result;
	}


	public async Task<SettingsGeneralDtos> UpdateGeneralSettingsAsync(UpdateGeneralSettingsDto generalSettingsDto)
	{
		var result = await _settingsService.UpdateGeneralSettingsAsync(generalSettingsDto);
		if (result == null)
		{
			throw new InvalidOperationException("Failed to update general settings. Settings not found or operation failed.");
		}
		return result;
	}

	//


	/// <summary>
	/// Updates AI permissions settings
	/// </summary>
	/// <param name="permissionsDto">The permissions data to update</param>
	/// <returns>Updated settings DTO</returns>
	/// <exception cref="InvalidOperationException">Thrown when settings are not found or update fails</exception>
	public async Task<SettingsDto> UpdateAiPermissionsAsync(UpdateAiPermissionsDto permissionsDto)
	{
		var result = await _settingsService.UpdateAiPermissionsAsync(permissionsDto);
		if (result == null)
		{
			throw new InvalidOperationException("Failed to update AI permissions. Settings not found or operation failed.");
		}
		return result;
	}



	/// <summary>
	/// Updates AI model settings
	/// </summary>
	/// <param name="aiModelDto">The AI model settings to update</param>
	/// <returns>Updated settings DTO</returns>
	public async Task<SettingsDto> UpdateAIModelSettingsAsync(UpdateAIModelSettingsDto aiModelDto)
	{
		var result = await _settingsService.UpdateAIModelSettingsAsync(aiModelDto);
		if (result == null)
		{
			throw new InvalidOperationException("Failed to update AI model settings. Settings not found or operation failed.");
		}
		return result;
	}

	public async Task<bool> UpdateAIModelProviderScopeAsync(string scope)
	{
		return await _settingsService.UpdateAIModelProviderScopeAsync(scope);
	}

	public async Task<SettingsDto> UpdateTreeStructureScheduleAsync(UpdateTreeStructureScheduleDto scheduleDto)
	{
		var result = await _settingsService.UpdateTreeStructureScheduleAsync(scheduleDto);
		if (result == null)
		{
			throw new InvalidOperationException("Failed to update tree structure schedule settings. Settings not found or operation failed.");
		}
		return result;
	}

	/// <summary>
	/// Get all available AI models
	/// </summary>
	/// <returns>List of available AI models</returns>
	public async Task<IEnumerable<AIModelDto>> GetAvailableAIModelsAsync()
	{
		var models = await _modelRepository.GetAllModelsAsync();
		// IMPORTANT: Include Purpose so UI (Databricks chat/embedding dropdowns) can split models correctly
		var projected = models.Select(m => new AIModelDto
		{
			Id = m.Id,
			Name = m.Name,
			Type = m.Type,
			Provider = m.Provider,
			Description = m.Description,
			Purpose = m.Purpose, // previously omitted -> caused Databricks lists to appear empty
			IsActive = m.IsActive
		});
		return projected;
	}

	/// <summary>
	/// Get AI models by type
	/// </summary>
	/// <param name="type">Model type: "local" or "hosted"</param>
	/// <returns>List of AI models of the specified type</returns>
	public async Task<IEnumerable<AIModelDto>> GetAIModelsByTypeAsync(string type)
	{
		var models = await _modelRepository.GetModelsByTypeAsync(type);
		return models.Select(m => new AIModelDto
		{
			Id = m.Id,
			Name = m.Name,
			Type = m.Type,
			Provider = m.Provider,
			Description = m.Description,
			Purpose = m.Purpose,
			IsActive = m.IsActive
		});
	}

	public async Task<AIModelDto> AddAIModelAsync(CreateAIModelDto aiModelDto)
	{
		if (aiModelDto == null)
		{
			throw new ArgumentNullException(nameof(aiModelDto));
		}

		var model = new Model
		{
			Id = aiModelDto.Id,
			Name = aiModelDto.Name,
			Type = aiModelDto.Type,
			Provider = aiModelDto.Provider,
			Description = aiModelDto.Description,
			Purpose = aiModelDto.Purpose,
			IsActive = aiModelDto.IsActive
		};

		var result = await _modelRepository.AddModelAsync(model);

		return new AIModelDto
		{
			Id = result.Id,
			Name = result.Name,
			Type = result.Type,
			Provider = result.Provider,
			Description = result.Description,
			Purpose = result.Purpose,
			IsActive = result.IsActive
		};
	}

	public async Task<AIModelDto> UpdateModelAsync(string modelId)
	{
		if (string.IsNullOrWhiteSpace(modelId))
		{
			throw new ArgumentException("Model id cannot be empty", nameof(modelId));
		}

		Console.WriteLine($"Updating model with id: {modelId}");

		var modelInfo = await _settingsService.GetModelInfoAsync(modelId);
		if (modelInfo == null)
		{
			throw new InvalidOperationException($"Model with id {modelId} not found");
		}


		string containsEmbedding = modelId.ToLower().Contains("embedding") ? "embedding" : "chat";

		string localPath = modelInfo.Data.Entypoint;
		if (OperatingSystem.IsWindows())
		{
			localPath = modelInfo.Data.Entypoint.Replace("/", "\\");
		}

		// Check if model exists
		var existingModel = await _modelRepository.GetModelByIdAsync(modelId);
		if (existingModel != null)
		{
			existingModel.LocalPath = localPath;
			existingModel.IsActive = true;
			existingModel.UpdatedAt = DateTime.UtcNow;

			await _modelRepository.UpdateModelAsync(existingModel);

			return new AIModelDto
			{
				Id = existingModel.Id,
				Name = existingModel.Name,
				Type = existingModel.Type,
				Provider = existingModel.Provider,
				Description = existingModel.Description,
				Purpose = existingModel.Purpose,
				IsActive = existingModel.IsActive,
				LocalPath = existingModel.LocalPath
			};
		}

		var model = new Model
		{
			Id = modelInfo.Data.ModelId,
			Name = modelInfo.Data.ModelId,
			Type = "local",
			Provider = "python", // Assuming local models are of provider "python"
			LocalPath = localPath,
			Description = $"Local model downloaded from Baiss",
			Purpose = containsEmbedding,
			IsActive = true
		};
		var result = await _modelRepository.AddModelAsync(model);

		return new AIModelDto
		{
			Id = result.Id,
			Name = result.Name,
			Type = result.Type,
			Provider = result.Provider,
			Description = result.Description,
			Purpose = result.Purpose,
			IsActive = result.IsActive,
			LocalPath = result.LocalPath
		};
	}


	public Task DeleteAIModelAsync(string modelId)
	{
		if (string.IsNullOrWhiteSpace(modelId))
		{
			throw new ArgumentException("Model id cannot be empty", nameof(modelId));
		}

		return _modelRepository.DeleteModelAsync(modelId);
	}

	public void StopBackgroundOperations()
	{
		_settingsService.StopBackgroundOperations();
	}

	public void ResumeBackgroundOperations()
	{
		_settingsService.ResumeBackgroundOperations();
	}

	/// <summary>
	/// Checks the status of the tree structure update thread.
	/// </summary>
	/// <returns>True if the thread is running, false otherwise.</returns>
	public Task<bool> CheckTreeStructureThread()
	{
		return _settingsService.CheckTreeStructureThread();
	}

	public Task<List<ModelInfo>> DownloadAvailableModelsAsync()
	{
		return _settingsService.DownloadAvailableModelsAsync();
	}

    public Task<StartModelsDownloadResponse> StartModelDownloadAsync(string modelId, string? downloadUrl = null)
    {
        return _settingsService.StartModelDownloadAsync(modelId, downloadUrl);
    }

	public Task<ModelDownloadListResponse> GetModelDownloadListAsync()
	{
		return _settingsService.GetModelDownloadListAsync();
	}

	public Task<StopModelDownloadResponse> StopModelDownloadAsync(string processId)
	{
		return _settingsService.StopModelDownloadAsync(processId);
	}

	public Task<ModelDownloadProgressResponse> GetModelDownloadProgressAsync(string processId)
	{
		return _settingsService.GetModelDownloadProgressAsync(processId);
	}

	public Task<ModelsListResponse> GetModelsListExistsAsync()
	{
		return _settingsService.GetModelsListExistsAsync();
	}

	public Task<bool> DeleteModelAsync(string modelId)
	{
		return _settingsService.DeleteModelAsync(modelId);
	}


	public Task<ModelsListResponse> GetModelsListExistWIthCheackDbAsync()
	{
		return _settingsService.GetModelsListExistWIthCheackDbAsync();
	}

	public string CheckUpdateCheckThread()
	{
		return _settingsService.CheckUpdateCheckThread();
	}


	public void RefreshTreeStructure()
	{
		_settingsService.RefreshTreeStructure();
	}

    public Task<ModelDetailsResponseDto> SearchAndSaveExternalModelAsync(string modelId)
    {
        return _settingsService.SearchAndSaveExternalModelAsync(modelId);
    }

	public Task RestartServerAsync(string modelType)
	{
		return _settingsService.RestartServerAsync(modelType);
	}

}
