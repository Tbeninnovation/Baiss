using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Repository interface for managing AI models
/// </summary>
public interface IModelRepository
{
    /// <summary>
    /// Get all active models
    /// </summary>
    Task<IEnumerable<Model>> GetAllModelsAsync();

    /// <summary>
    /// Get models by type (local/hosted)
    /// </summary>
    Task<IEnumerable<Model>> GetModelsByTypeAsync(string type);

    /// <summary>
    /// Get models by provider
    /// </summary>
    Task<IEnumerable<Model>> GetModelsByProviderAsync(string provider);

    /// <summary>
    /// Get models by purpose (chat/embedding)
    /// </summary>
    Task<IEnumerable<Model>> GetModelsByPurposeAsync(string purpose);

    /// <summary>
    /// Get models by provider and purpose
    /// </summary>
    Task<IEnumerable<Model>> GetModelsByProviderAndPurposeAsync(string provider, string purpose);

    /// <summary>
    /// Get a specific model by ID
    /// </summary>
    Task<Model?> GetModelByIdAsync(string id);

    /// <summary>
    /// Add a new model
    /// </summary>
    Task<Model> AddModelAsync(Model model);

    /// <summary>
    /// Update an existing model
    /// </summary>
    Task<Model> UpdateModelAsync(Model model);

    /// <summary>
    /// Delete a model by ID
    /// </summary>
    Task DeleteModelAsync(string id);

    /// <summary>
    /// Check if a model exists
    /// </summary>
    Task<bool> ModelExistsAsync(string id);

    /// <summary>
    /// Get the local path of a model by its ID
    /// </summary>
    Task<string> GetPathByModelIdAsync(string modelId);
}
