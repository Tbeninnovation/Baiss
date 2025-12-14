namespace Baiss.Domain.Entities;

/// <summary>
/// Represents an AI model that can be used in the application
/// </summary>
public class Model
{
    /// <summary>
    /// Unique identifier for the model
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the model
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of model: "local" or "hosted"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Provider of the model: "python", "openai", "anthropic", "databricks", "azure", etc.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Description of the model
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Purpose of the model: "chat" or "embedding"
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Whether this model is currently active/available
    /// </summary>
    public bool IsActive { get; set; } = true;



    // path of local models (if Type is "local")
    public string? LocalPath { get; set; }


    /// <summary>
    /// When the model was added to the system
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the model was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Model types enumeration for better type safety
/// </summary>
public static class ModelTypes
{
    public const string Local = "local";
    public const string Hosted = "hosted";
}

/// <summary>
/// Model purposes enumeration
/// </summary>
public static class ModelPurposes
{
    public const string Chat = "chat";
    public const string Embedding = "embedding";
}

/// <summary>
/// Model providers enumeration
/// </summary>
public static class ModelProviders
{
    public const string Python = "python";
    public const string Ollama = "ollama";
    public const string OpenAI = "openai";
    public const string Anthropic = "anthropic";
    public const string Databricks = "databricks";
    public const string Azure = "azure";
    public const string RunPod = "runpod";
}
