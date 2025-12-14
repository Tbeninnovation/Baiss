using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Interface for the application settings repository
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Retrieves the application settings
    /// </summary>
    /// <returns>The application settings or null</returns>
    Task<Settings?> GetAsync();

    /// <summary>
    /// Updates or creates the application settings
    /// </summary>
    /// <param name="settings">The settings to save</param>
    Task SaveAsync(Settings settings);

    Task<string> GetModelIdByTypeAsync(string modelType);
}

