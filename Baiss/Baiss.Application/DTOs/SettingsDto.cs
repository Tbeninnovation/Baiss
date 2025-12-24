
using Baiss.Domain.Entities;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Baiss.Application.DTOs;

/// <summary>
/// DTO pour les paramètres de l'application
/// </summary>
public class SettingsDto
{
    public PerformanceLevel Performance { get; set; } = PerformanceLevel.Small;
    public List<string> AllowedPaths { get; set; } = new List<string>();
    public List<string> AllowedApplications { get; set; } = new List<string>();

    public string AppVersion { get; set; } = "";

    public List<string> AllowedFileExtensions { get; set; } = new List<string>();

    public bool EnableAutoUpdate { get; set; } = true;
    public bool AllowFileReading { get; set; } = false;

    public bool AllowUpdateCreatedFiles { get; set; } = false;
    public bool AllowCreateNewFiles { get; set; } = false;

    //  Path to save new files created by ai
    public string NewFilesSavePath { get; set; } = string.Empty;

    public List<string> ExistingModels { get; set; } = new List<string>();

    // AI Model Configuration
    public string AIModelType { get; set; } = string.Empty;
    // Radio button provider scope (local | hosted | databricks)
    public string AIModelProviderScope { get; set; } = string.Empty;
    // Legacy AIModelId removed. Use AIChatModelId / AIEmbeddingModelId.

    // Separate fields for chat and embedding models
    public string? AIChatModelId { get; set; }
    public string? AIEmbeddingModelId { get; set; }

    public string HuggingFaceApiKey { get; set; } = string.Empty;

    public string TreeStructureSchedule { get; set; } = "0 0 0 * * ?";
    public bool TreeStructureScheduleEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


public class AllowedPathsDtos
{
    public bool IsValid { get; set; }
    public string Path { get; set; } = string.Empty;

}

public class AllowedFileExtensionsDtos
{
    public bool IsValid { get; set; }
    public string Extension { get; set; } = string.Empty;
}

public class UpdateAiPermissionsDto
{
    public bool AllowFileReading { get; set; }
    public bool AllowUpdateCreatedFiles { get; set; }
    public bool AllowCreateNewFiles { get; set; }

    //  Path to save new files created by ai
    public string NewFilesSavePath { get; set; } = string.Empty;
    public List<AllowedPathsDtos>? AllowedPaths { get; set; } = null;
    public List<AllowedFileExtensionsDtos>? AllowedFileExtensions { get; set; } = null;
}

public class SettingsGeneralDtos
{
    public PerformanceLevel Performance { get; set; } = PerformanceLevel.Small;
    public bool EnableAutoUpdate { get; set; } = true;

    public string AppVersion { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}



public class UpdateGeneralSettingsDto
{
    public PerformanceLevel Performance { get; set; }
    public bool EnableAutoUpdate { get; set; }

    public bool CheckUpdate { get; set; } = false;

    public bool NeedUpdate { get; set; } = false;
}

public class AIModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Purpose { get; set; } = string.Empty; // chat | embedding
    public bool IsActive { get; set; }
    public string? LocalPath { get; set; }
    public string? Author { get; set; }
    public int Downloads { get; set; }
    public int Likes { get; set; }
}

public class UpdateAIModelSettingsDto
{
    public string AIModelType { get; set; } = string.Empty;
    // Separate fields for chat and embedding models
    public string? AIChatModelId { get; set; }
    public string? AIEmbeddingModelId { get; set; }
    public string? AIModelProviderScope { get; set; }
    public string? HuggingFaceApiKey { get; set; }
}


public class CreateAIModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Purpose { get; set; } = string.Empty; // chat | embedding
    public bool IsActive { get; set; } = true;
}

public class ReleaseInfoResponse
{
    [JsonPropertyName("currentStable")]
    public Dictionary<string, PlatformReleaseInfo> CurrentStable { get; set; } = new Dictionary<string, PlatformReleaseInfo>();

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class PlatformReleaseInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
}


public class ServerLaunchResult
{
    public Process? Process { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
}

