using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Repository interface for managing available models from HuggingFace
/// </summary>
public interface IAvailableModelRepository
{
    /// <summary>
    /// Get all available models
    /// </summary>
    Task<IEnumerable<AvailableModel>> GetAllAsync();

    /// <summary>
    /// Get a specific available model by ID
    /// </summary>
    Task<AvailableModel?> GetByIdAsync(string id);

    /// <summary>
    /// Add a new available model
    /// </summary>
    Task<AvailableModel> AddAsync(AvailableModel model);

    /// <summary>
    /// Update an existing available model
    /// </summary>
    Task<AvailableModel> UpdateAsync(AvailableModel model);

    /// <summary>
    /// Add or update multiple available models
    /// </summary>
    Task BulkUpsertAsync(IEnumerable<AvailableModel> models);

    /// <summary>
    /// Delete an available model by ID
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Check if an available model exists
    /// </summary>
    Task<bool> ExistsAsync(string id);

    /// <summary>
    /// Delete all available models (useful for refresh)
    /// </summary>
    Task DeleteAllAsync();
}
