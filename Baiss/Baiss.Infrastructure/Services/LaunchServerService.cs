using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Baiss.Application.Interfaces;
using Baiss.Application.DTOs;
using System.Text.Json;



namespace Baiss.Infrastructure.Services;

/// <summary>
/// Service for launching and managing external server processes like llama-cpp server
/// </summary>
public class LaunchServerService : ILaunchServerService
{
    private readonly ILogger<LaunchServerService> _logger;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IModelRepository _modelRepository;
    private readonly Dictionary<string, Process> _serverProcesses = new();
    private readonly Dictionary<string, (string host, int port)> _serverEndpoints = new();

    public LaunchServerService(
        ILogger<LaunchServerService> logger,
        ISettingsRepository settingsRepository,
        IModelRepository modelRepository)
    {
        _logger = logger;
        _settingsRepository = settingsRepository;
        _modelRepository = modelRepository;
    }

    /// <summary>
    /// Launches the llama-cpp server with the specified model type
    /// </summary>
    public async Task<ServerLaunchResult> LaunchLlamaCppServerAsync(string modelType = "chat", string? customModelPath = null, string? additionalArgs = null)
    {
        try
        {
            // Get model path
            string modelPath = await GetModelPathAsync(modelType, customModelPath);

            if (string.IsNullOrEmpty(modelPath))
            {
                _logger.LogError("No model path found for model type: {ModelType}", modelType);
                return new ServerLaunchResult { Process = null, Host = "", Port = -1 };
            }

            // Verify model file exists
            if (!File.Exists(modelPath))
            {
                _logger.LogError("Model file not found at path: {ModelPath}", modelPath);
                return new ServerLaunchResult { Process = null, Host = "", Port = -1 };
            }

            // Get llama-server executable path
            string serverPath = GetLlamaServerPath();

            if (string.IsNullOrEmpty(serverPath) || !File.Exists(serverPath))
            {
                _logger.LogError("llama-server executable not found at path: {ServerPath}", serverPath);
                return new ServerLaunchResult { Process = null, Host = "", Port = -1 };
            }

            // Build command arguments and get server endpoint info
            var (arguments, host, port) = BuildServerArguments(modelPath, additionalArgs);

            _logger.LogInformation("Starting llama-cpp server with model: {ModelPath}", modelPath);
            _logger.LogInformation("Using server executable: {ServerPath}", serverPath);
            _logger.LogInformation("Server arguments: {Arguments}", arguments);
            _logger.LogInformation("Server will be available at: http://{Host}:{Port}", host, port);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(serverPath)
            };

            var process = Process.Start(processStartInfo);

            if (process != null)
            {
                // Attach event handlers for output and error data
                AttachProcessEventHandlers(process);

                // Start asynchronous reading
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Store the process reference and endpoint info by model type
                _serverProcesses[modelType] = process;
                _serverEndpoints[modelType] = (host, port);

                _logger.LogInformation("llama-cpp server started successfully with PID: {ProcessId} for model type: {ModelType}", process.Id, modelType);
                _logger.LogInformation("Server is available at: http://{Host}:{Port}", host, port);

                return new ServerLaunchResult { Process = process, Host = host, Port = port };
            }
            else
            {
                _logger.LogError("Failed to start llama-cpp server process");
                return new ServerLaunchResult { Process = null, Host = "", Port = -1 };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error launching llama-cpp server: {Message}", ex.Message);
            return new ServerLaunchResult { Process = null, Host = "", Port = -1 };
        }
    }

    /// <summary>
    /// Stops the running llama-cpp server process
    /// </summary>
    public async Task<bool> StopServerAsync(Process process, int gracefulTimeoutMs = 3000)
    {
        if (process == null || process.HasExited)
        {
            return true;
        }

		gracefulTimeoutMs = 10;

        try
        {
            _logger.LogInformation("Stopping llama-cpp server process with PID: {ProcessId}", process.Id);

            // First try graceful termination
            // process.CloseMainWindow();

            // Wait for graceful shutdown
            // if (await WaitForExitAsync(process, gracefulTimeoutMs))
            // {
            //     _logger.LogInformation("llama-cpp server stopped gracefully");
            //     return true;
            // }

            // If it doesn't exit gracefully, force kill it
            _logger.LogWarning("llama-cpp server did not exit gracefully, forcing termination");
            process.Kill(true); // true kills entire process tree

            if (await WaitForExitAsync(process, 2000))
            {
                _logger.LogInformation("llama-cpp server force-stopped successfully");
                return true;
            }

            _logger.LogError("Failed to stop llama-cpp server process");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping llama-cpp server process: {Message}", ex.Message);
            return false;
        }
        finally
        {
            process?.Dispose();
            // Clear the stored reference if this process exists in our dictionary
            var keysToRemove = new List<string>();
            foreach (var kvp in _serverProcesses)
            {
                if (kvp.Value == process)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _serverProcesses.Remove(key);
                _serverEndpoints.Remove(key);
            }
        }
    }

    /// <summary>
    /// Checks if a server process is still running and healthy
    /// </summary>
    public bool IsServerRunning(Process? process)
    {
        return process != null && !process.HasExited;
    }

    #region Private Helper Methods

    private async Task<string> GetModelPathAsync(string modelType, string? customModelPath)
    {
        // Use custom model path if provided
        if (!string.IsNullOrEmpty(customModelPath))
        {
            return customModelPath;
        }

        // Get model ID from settings and retrieve actual local path from database
        try
        {
            var modelId = await _settingsRepository.GetModelIdByTypeAsync(modelType);
            if (!string.IsNullOrEmpty(modelId))
            {
                var modelPath = await _modelRepository.GetPathByModelIdAsync(modelId);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    return modelPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve model from settings/database for model type: {ModelType}", modelType);
        }

        return string.Empty;
    }

    private string GetLlamaServerPath()
    {
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string configFilePath = Path.Combine(appDirectory, "baiss_config.json");

        string jsonString = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<ConfigeDto>(jsonString);

        return config?.LlamaCppServerPath ?? string.Empty;
    }


    private (string arguments, string host, int port) BuildServerArguments(string modelPath, string? additionalArgs)
    {
        var args = $"-m \"{modelPath}\"";
        string host = "127.0.0.1";
        int preferredPort = 8080;
        int actualPort;


        // Find an available port starting from preferred port
        actualPort = FindNextAvailablePort(preferredPort);

        if (actualPort == -1)
        {
            throw new InvalidOperationException("No available port found to launch llama-cpp server.");
        }

        // Build arguments with the actual available port
        if (string.IsNullOrEmpty(additionalArgs))
        {
            args += $" --host {host} --port {actualPort} --ctx-size 30000";
        }
        else
        {
            args += $" --host {host} --port {actualPort} {additionalArgs}";
        }

        return (args, host, actualPort);
    }

    private void AttachProcessEventHandlers(Process process)
    {
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.LogInformation("llama-cpp Output: {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Check if it's actually an error or just server info logs
                if (e.Data.Contains("ERROR") || e.Data.Contains("error") ||
                    e.Data.Contains("Traceback") || e.Data.Contains("Exception"))
                {
                    _logger.LogError("llama-cpp Error: {Error}", e.Data);
                }
                else
                {
                    _logger.LogInformation("llama-cpp Info: {Info}", e.Data);
                }
            }
        };
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs)
    {
        return await Task.Run(() => process.WaitForExit(timeoutMs));
    }

    /// <summary>
    /// Checks if a port is available on the local machine
    /// </summary>
    /// <param name="port">The port number to check</param>
    /// <returns>True if the port is available, false if it's in use</returns>
    private static bool IsPortAvailable(int port)
    {
        try
        {
            // Check TCP port
            var tcpListener = new TcpListener(System.Net.IPAddress.Loopback, port);
            tcpListener.Start();
            tcpListener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the next available port starting from the specified port
    /// </summary>
    /// <param name="startPort">The starting port to check</param>
    /// <param name="maxAttempts">Maximum number of ports to try</param>
    /// <returns>The next available port, or -1 if no available port found</returns>
    private int FindNextAvailablePort(int startPort, int maxAttempts = 100)
    {
        for (int port = startPort; port < startPort + maxAttempts; port++)
        {
            // Skip well-known system ports if we're in that range
            if (port < 1024)
                continue;

            if (IsPortAvailable(port))
            {
                _logger.LogDebug("Found available port: {Port}", port);
                return port;
            }
            else
            {
                _logger.LogDebug("Port {Port} is in use, trying next port", port);
            }
        }

        _logger.LogWarning("No available port found after {MaxAttempts} attempts starting from port {StartPort}", maxAttempts, startPort);
        return -1;
    }

    #endregion

    /// <summary>
    /// Gets the current running llama-cpp server process (returns chat server by default)
    /// </summary>
    public Process? GetCurrentServerProcess()
    {
        // Return chat server for backward compatibility
        return GetServerProcessByType("chat");
    }

    /// <summary>
    /// Gets a server process by model type
    /// </summary>
    public Process? GetServerProcessByType(string modelType)
    {
        if (_serverProcesses.TryGetValue(modelType, out var process))
        {
            return process;
        }
        return null;
    }

    // /// <summary>
    // /// Stops the current running llama-cpp server (chat server by default)
    // /// </summary>
    // public async Task<bool> StopCurrentServerAsync()
    // {
    //     return await StopServerByTypeAsync("chat");
    // }

    /// <summary>
    /// Stops a server by model type
    /// </summary>
    public async Task<bool> StopServerByTypeAsync(string modelType)
    {
        if (!_serverProcesses.TryGetValue(modelType, out var process) || process.HasExited)
        {
            _logger.LogInformation("No llama-cpp server is currently running for model type: {ModelType}", modelType);
            return true;
        }

        _logger.LogInformation("Stopping llama-cpp server for model type: {ModelType}", modelType);
        return await StopServerAsync(process);
    }

    /// <summary>
    /// Gets the server URL and port for a specific model type
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "chat", "embedding")</param>
    /// <returns>A tuple containing (host, port) or null if server is not running</returns>
    public (string host, int port)? GetServerEndpoint(string modelType)
    {
        if (_serverEndpoints.TryGetValue(modelType, out var endpoint))
        {
            // Verify the server process is still running
            if (_serverProcesses.TryGetValue(modelType, out var process) && !process.HasExited)
            {
                _logger.LogDebug("Retrieved server endpoint for model type '{ModelType}': {Host}:{Port}",
                    modelType, endpoint.host, endpoint.port);
                return endpoint;
            }
            else
            {
                // Clean up stale endpoint info
                _serverEndpoints.Remove(modelType);
                _serverProcesses.Remove(modelType);
                _logger.LogWarning("Server process for model type '{ModelType}' has exited, removing endpoint", modelType);
            }
        }

        _logger.LogDebug("No active server endpoint found for model type: {ModelType}", modelType);
        return null;
    }

    /// <summary>
    /// Gets the complete server URL for a specific model type
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "chat", "embedding")</param>
    /// <returns>The complete server URL (e.g., "http://localhost:8080") or null if server is not running</returns>
    public string? GetServerUrl(string modelType)
    {
        var endpoint = GetServerEndpoint(modelType);
        if (endpoint.HasValue)
        {
            var url = $"http://{endpoint.Value.host}:{endpoint.Value.port}";
            _logger.LogDebug("Generated server URL for model type '{ModelType}': {Url}", modelType, url);
            return url;
        }

        _logger.LogDebug("Cannot generate server URL for model type '{ModelType}' - no active server found", modelType);
        return null;
    }

}
