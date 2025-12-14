using System.Diagnostics;
using System.Threading.Tasks;
using Baiss.Application.DTOs;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Interface for launching and managing external server processes like llama-cpp server
/// </summary>
public interface ILaunchServerService
{
    /// <summary>
    /// Launches the llama-cpp server with the specified model type
    /// </summary>
    /// <param name="modelType">The type of model to use (e.g., "chat", "embedding", etc.)</param>
    /// <param name="customModelPath">Optional custom model path, if not provided will use settings/database</param>
    /// <param name="additionalArgs">Additional command line arguments for the server</param>
    /// <returns>The launched process, or null if launch failed</returns>
    Task<ServerLaunchResult> LaunchLlamaCppServerAsync(string modelType = "chat", string? customModelPath = null, string? additionalArgs = null);

    /// <summary>
    /// Stops the running llama-cpp server process
    /// </summary>
    /// <param name="process">The process to stop</param>
    /// <param name="gracefulTimeoutMs">Timeout in milliseconds for graceful shutdown before force kill</param>
    /// <returns>True if stopped successfully</returns>
    Task<bool> StopServerAsync(Process process, int gracefulTimeoutMs = 3000);

    /// <summary>
    /// Checks if a server process is still running and healthy
    /// </summary>
    /// <param name="process">The process to check</param>
    /// <returns>True if the process is running and healthy</returns>
    bool IsServerRunning(Process? process);

    /// <summary>
    /// Gets the current running llama-cpp server process
    /// </summary>
    /// <returns>The current process or null if not running</returns>
    Process? GetCurrentServerProcess();

    /// <summary>
    // /// Stops the current running llama-cpp server
    // /// </summary>
    // /// <returns>True if stopped successfully</returns>
    // Task<bool> StopCurrentServerAsync();

    /// <summary>
    /// Gets a server process by model type
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "chat", "embedding")</param>
    /// <returns>The server process or null if not found</returns>
    Process? GetServerProcessByType(string modelType);

    /// <summary>
    /// Stops a server by model type
    /// </summary>
    /// <param name="modelType">The type of model to stop (e.g., "chat", "embedding")</param>
    /// <returns>True if stopped successfully</returns>
    Task<bool> StopServerByTypeAsync(string modelType);

    /// <summary>
    /// Gets the server URL and port for a specific model type
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "chat", "embedding")</param>
    /// <returns>A tuple containing (host, port) or null if server is not running</returns>
    (string host, int port)? GetServerEndpoint(string modelType);

    /// <summary>
    /// Gets the complete server URL for a specific model type
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "chat", "embedding")</param>
    /// <returns>The complete server URL (e.g., "http://localhost:8080") or null if server is not running</returns>
    string? GetServerUrl(string modelType);
}
