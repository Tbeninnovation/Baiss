using Baiss.Application.Models;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Interface for the singleton Python bridge service
/// </summary>
public interface IPythonBridgeService : IDisposable
{
    /// <summary>
    /// Gets whether the Python bridge is initialized and ready
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the Python runtime information
    /// </summary>
    PythonRuntimeInfo RuntimeInfo { get; }

    /// <summary>
    /// Initializes the Python bridge service (called automatically)
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Executes a Python script with parameters
    /// </summary>
    Task<PythonExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Executes Python code directly
    /// </summary>
    Task<PythonExecutionResult> ExecuteCodeAsync(string pythonCode, Dictionary<string, object>? variables = null);

    /// <summary>
    /// Calls a specific function from a Python module
    /// </summary>
    Task<PythonExecutionResult> CallFunctionAsync(string moduleName, string functionName, params object[] arguments);

    /// <summary>
    /// Calls a Python function with streaming support using callbacks
    /// </summary>
    Task<PythonExecutionResult> CallStreamingFunctionAsync(
        string moduleName, 
        string functionName, 
        object request,
        Func<string, Task> onChunkReceived,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the Python environment is healthy
    /// </summary>
    Task<bool> ValidateHealthAsync();

    /// <summary>
    /// Lightweight health check that only verifies service state without Python runtime interaction
    /// </summary>
    bool IsHealthyQuick();

    /// <summary>
    /// Gets performance metrics for the bridge
    /// </summary>
    PythonBridgeMetrics GetMetrics();

    /// <summary>
    /// Resets the Python bridge (re-initializes)
    /// </summary>
    Task<bool> ResetAsync();
}