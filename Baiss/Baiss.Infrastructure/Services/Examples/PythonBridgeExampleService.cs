using Baiss.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.Examples;

/// <summary>
/// Example service showing how to use the enhanced PythonBridge service with streaming capabilities
/// </summary>
public class PythonBridgeExampleService
{
    private readonly IPythonBridgeService _pythonBridgeService;
    private readonly ILogger<PythonBridgeExampleService> _logger;

    public PythonBridgeExampleService(IPythonBridgeService pythonBridgeService, ILogger<PythonBridgeExampleService> logger)
    {
        _pythonBridgeService = pythonBridgeService;
        _logger = logger;
    }

    /// <summary>
    /// Example of calling a streaming Python function (like chatv2_stream_chunks)
    /// </summary>
    public async Task<string> CallStreamingChatAsync(string userMessage, List<object>? conversationHistory = null)
    {
        try
        {
            var streamingResult = new List<string>();
            
            // Create request object for Python function
            var request = new
            {
                message = new { content = userMessage },
                messages_historical = conversationHistory ?? new List<object>()
            };

            // Call streaming function with callback
            var result = await _pythonBridgeService.CallStreamingFunctionAsync(
                moduleName: "chatv2",  // Your chatv2.py file
                functionName: "chatv2_stream_chunks",
                request: request,
                onChunkReceived: async (chunk) =>
                {
                    // _logger.LogDebug("Received streaming chunk: {Chunk}", chunk);
                    streamingResult.Add(chunk);
                    
                    // Here you could:
                    // - Update UI in real-time
                    // - Send to SignalR clients
                    // - Process chunk for display
                    await ProcessStreamingChunk(chunk);
                }
            );

            if (result.IsSuccess)
            {
                _logger.LogInformation("Streaming completed successfully");
                return string.Join("", streamingResult);
            }
            else
            {
                _logger.LogError("Streaming failed: {Error}", result.ErrorMessage);
                return $"Error: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during streaming chat: {Message}", ex.Message);
            return $"Exception: {ex.Message}";
        }
    }

    /// <summary>
    /// Example of basic Python function calls
    /// </summary>
    public async Task<string> BasicPythonExample()
    {
        try
        {
            // Simple code execution
            var codeResult = await _pythonBridgeService.ExecuteCodeAsync(
                "import datetime; result = f'Current time: {datetime.datetime.now()}'");
            
            if (codeResult.IsSuccess)
            {
                _logger.LogInformation("Python code executed: {Result}", codeResult.Result);
            }

            // Function call with parameters
            var functionResult = await _pythonBridgeService.CallFunctionAsync(
                "json", "dumps", new { message = "Hello from C#", timestamp = DateTime.UtcNow });
            
            if (functionResult.IsSuccess)
            {
                _logger.LogInformation("Function call result: {Result}", functionResult.Result);
                return functionResult.Result ?? "No result";
            }

            return "Basic example completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in basic Python example");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Example of health monitoring
    /// </summary>
    public async Task<string> CheckPythonBridgeHealthAsync()
    {
        try
        {
            var isHealthy = await _pythonBridgeService.ValidateHealthAsync();
            var metrics = _pythonBridgeService.GetMetrics();
            var runtimeInfo = _pythonBridgeService.RuntimeInfo;

            return $@"PythonBridge Health Report:
                Ready: {_pythonBridgeService.IsReady}
                Healthy: {isHealthy}
                Uptime: {metrics.Uptime}
                Total Executions: {metrics.TotalExecutions}
                Success Rate: {metrics.SuccessRate:F1}%
                Average Execution Time: {metrics.AverageExecutionTime.TotalMilliseconds:F0}ms
                Python Version: {runtimeInfo.PythonVersion}
                Python Executable: {runtimeInfo.PythonExecutable}";
        }
        catch (Exception ex)
        {
            return $"Health check failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Example of using the service with file operations (like embeddings)
    /// </summary>
    public async Task<List<string>> GetFilePathsWithoutEmbeddingsAsync()
    {
        try
        {
            var result = await _pythonBridgeService.CallFunctionAsync("files", "get_all_paths_wo_embeddings");
            
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Result))
            {
                // Parse the JSON response (assuming it returns a list of paths)
                var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Result) ?? new List<string>();
                _logger.LogInformation("Found {Count} files without embeddings", paths.Count);
                return paths;
            }
            
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file paths without embeddings");
            return new List<string>();
        }
    }

    private async Task ProcessStreamingChunk(string chunk)
    {
        // Example processing of streaming chunks
        try
        {
            // You could parse JSON chunks, update progress, etc.
            if (chunk.Contains("error"))
            {
                _logger.LogWarning("Error detected in streaming chunk: {Chunk}", chunk);
            }
            
            // Simulate some async processing
            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing streaming chunk: {Chunk}", chunk);
        }
    }
}