using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services;

/// <summary>
/// Diagnostic service for monitoring and managing the PythonBridge service
/// </summary>
public class PythonBridgeDiagnosticsService
{
    private readonly IPythonBridgeService _pythonBridgeService;
    private readonly ILogger<PythonBridgeDiagnosticsService> _logger;

    public PythonBridgeDiagnosticsService(IPythonBridgeService pythonBridgeService, ILogger<PythonBridgeDiagnosticsService> logger)
    {
        _pythonBridgeService = pythonBridgeService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive diagnostics information about the PythonBridge service
    /// </summary>
    public async Task<PythonBridgeDiagnostics> GetDiagnosticsAsync()
    {
        var diagnostics = new PythonBridgeDiagnostics
        {
            Timestamp = DateTime.UtcNow,
            IsReady = _pythonBridgeService.IsReady,
            RuntimeInfo = _pythonBridgeService.RuntimeInfo,
            Metrics = _pythonBridgeService.GetMetrics()
        };

        // Perform health check
        try
        {
            diagnostics.IsHealthy = await _pythonBridgeService.ValidateHealthAsync();
        }
        catch (Exception ex)
        {
            diagnostics.IsHealthy = false;
            diagnostics.HealthCheckError = ex.Message;
        }

        // Test basic functionality
        try
        {
            var testResult = await _pythonBridgeService.ExecuteCodeAsync("result = 'diagnostics_test_success'");
            diagnostics.BasicFunctionalityTest = testResult.IsSuccess;
            if (!testResult.IsSuccess)
            {
                diagnostics.BasicFunctionalityError = testResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            diagnostics.BasicFunctionalityTest = false;
            diagnostics.BasicFunctionalityError = ex.Message;
        }

        return diagnostics;
    }

    /// <summary>
    /// Performs a comprehensive test of the PythonBridge service
    /// </summary>
    public async Task<PythonBridgeTestResult> RunComprehensiveTestAsync()
    {
        var testResult = new PythonBridgeTestResult
        {
            TestStartTime = DateTime.UtcNow
        };

        var tests = new List<(string Name, Func<Task<bool>> Test, string? Error)>();

        // Test 1: Basic code execution
        tests.Add(("Basic Code Execution", async () =>
        {
            var result = await _pythonBridgeService.ExecuteCodeAsync("result = 2 + 2");
            return result.IsSuccess && result.Result?.Contains("4") != false;
        }, null));

        // Test 2: Module import
        tests.Add(("Module Import", async () =>
        {
            var result = await _pythonBridgeService.ExecuteCodeAsync("import json; result = 'json_imported'");
            return result.IsSuccess;
        }, null));

        // Test 3: Function call
        tests.Add(("Function Call", async () =>
        {
            var result = await _pythonBridgeService.CallFunctionAsync("json", "dumps", new { test = "value" });
            return result.IsSuccess && !string.IsNullOrEmpty(result.Result);
        }, null));

        // Test 4: Exception handling
        tests.Add(("Exception Handling", async () =>
        {
            var result = await _pythonBridgeService.ExecuteCodeAsync("raise ValueError('test_error')");
            return !result.IsSuccess && result.ErrorMessage?.Contains("test_error") == true;
        }, null));

        // Run all tests
        foreach (var (name, test, _) in tests)
        {
            try
            {
                _logger.LogDebug("Running test: {TestName}", name);
                var success = await test();
                testResult.TestResults[name] = success;
                if (success)
                {
                    testResult.PassedTests++;
                }
                else
                {
                    testResult.FailedTests++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Test failed: {TestName}", name);
                testResult.TestResults[name] = false;
                testResult.TestErrors[name] = ex.Message;
                testResult.FailedTests++;
            }
        }

        testResult.TestEndTime = DateTime.UtcNow;
        testResult.TotalTests = tests.Count;
        testResult.Duration = testResult.TestEndTime - testResult.TestStartTime;

        _logger.LogInformation("PythonBridge comprehensive test completed: {Passed}/{Total} tests passed", 
            testResult.PassedTests, testResult.TotalTests);

        return testResult;
    }

    /// <summary>
    /// Resets the PythonBridge service
    /// </summary>
    public async Task<bool> ResetServiceAsync()
    {
        try
        {
            _logger.LogInformation("Resetting PythonBridge service via diagnostics");
            return await _pythonBridgeService.ResetAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset PythonBridge service");
            return false;
        }
    }

    /// <summary>
    /// Gets a formatted diagnostics report
    /// </summary>
    public async Task<string> GetDiagnosticsReportAsync()
    {
        var diagnostics = await GetDiagnosticsAsync();
        return JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// Comprehensive diagnostics information for PythonBridge service
/// </summary>
public class PythonBridgeDiagnostics
{
    public DateTime Timestamp { get; set; }
    public bool IsReady { get; set; }
    public bool IsHealthy { get; set; }
    public string? HealthCheckError { get; set; }
    public PythonRuntimeInfo RuntimeInfo { get; set; } = new();
    public PythonBridgeMetrics Metrics { get; set; } = new();
    public bool BasicFunctionalityTest { get; set; }
    public string? BasicFunctionalityError { get; set; }
}

/// <summary>
/// Results from comprehensive testing of PythonBridge service
/// </summary>
public class PythonBridgeTestResult
{
    public DateTime TestStartTime { get; set; }
    public DateTime TestEndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public Dictionary<string, bool> TestResults { get; set; } = new();
    public Dictionary<string, string> TestErrors { get; set; } = new();
    public bool AllTestsPassed => FailedTests == 0 && TotalTests > 0;
}