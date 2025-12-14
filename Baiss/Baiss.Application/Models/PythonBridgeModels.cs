namespace Baiss.Application.Models;

/// <summary>
/// Result of Python script execution
/// </summary>
public record PythonExecutionResult
{
    public bool IsSuccess { get; init; }
    public string? Result { get; init; }
    public string? ErrorMessage { get; init; }

    public static PythonExecutionResult Success(string? result = null) => new()
    {
        IsSuccess = true,
        Result = result
    };

    public static PythonExecutionResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Information about Python runtime
/// </summary>
public class PythonRuntimeInfo
{
    public bool IsInitialized { get; set; }
    public string PythonPath { get; set; } = string.Empty;
    public string ScriptsPath { get; set; } = string.Empty;
    public string PythonVersion { get; set; } = string.Empty;
    public string PythonExecutable { get; set; } = string.Empty;
    public string? Error { get; set; }
}

/// <summary>
/// Performance metrics for the Python bridge
/// </summary>
public class PythonBridgeMetrics
{
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public DateTime LastExecutionTime { get; set; }
    public string LastError { get; set; } = string.Empty;
    public long MemoryUsageBytes { get; set; }
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
}