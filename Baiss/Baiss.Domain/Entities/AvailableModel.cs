using System.Text.Json;

namespace Baiss.Domain.Entities;

/// <summary>
/// Represents an available model from HuggingFace with full metadata stored as JSONB
/// </summary>
public class AvailableModel
{
    /// <summary>
    /// Unique identifier for the model (model_id from HuggingFace)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public bool IsDownloaded { get; set; } = false;

    public bool IsValid { get; set; } = false;

    /// <summary>
    /// Full JSON metadata from HuggingFace API stored as JSONB (JsonDocument)
    /// This includes: model_id, author, model_name, downloads, likes, description, gguf_files, etc.
    /// </summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>
    /// When this model metadata was fetched/updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this model was first added to the database
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
