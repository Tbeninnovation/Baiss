// using System.Collections.Concurrent;
// using System.Diagnostics;
// using System.Text.Json;
// using Baiss.Application.Interfaces;
// using Baiss.Application.Models;
// using Baiss.Infrastructure.Interop;
// using Microsoft.Extensions.Logging;
// using Python.Runtime;

// namespace Baiss.Infrastructure.Services;

// /// <summary>
// /// Enhanced singleton Python bridge service with improved performance, error handling, and resource management
// /// </summary>
// public sealed class PythonBridgeService : IPythonBridgeService
// {
//     private static PythonBridgeService? _instance;
//     private static readonly object _lock = new object();

//     private readonly ILogger<PythonBridgeService> _logger;
//     private readonly string _pythonPath;
//     private readonly string _scriptsPath;
//     private readonly PythonBridgeMetrics _metrics;

//     private PythonBridge? _bridge;
//     private bool _isInitialized;
//     private bool _disposed;
//     private readonly SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);
//     // Removed _executionSemaphore - Python GIL handles concurrency, semaphore was causing deadlocks
//     private readonly ConcurrentDictionary<string, PyObject> _moduleCache = new();
//     private readonly Timer _healthCheckTimer;
//     private DateTime _lastHealthCheck = DateTime.MinValue;

//     private PythonBridgeService(string pythonPath, string scriptsPath, ILogger<PythonBridgeService> logger)
//     {
//         _pythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
//         _scriptsPath = scriptsPath ?? throw new ArgumentNullException(nameof(scriptsPath));
//         _logger = logger ?? throw new ArgumentNullException(nameof(logger));

//         _metrics = new PythonBridgeMetrics
//         {
//             StartTime = DateTime.UtcNow
//         };

//         // Set up periodic health checks (every 5 minutes)
//         _healthCheckTimer = new Timer(async _ => await PerformHealthCheckAsync(), null,
//             TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

//         _logger.LogInformation("PythonBridge service instance created");
//     }

//     /// <summary>
//     /// Gets or creates the singleton instance
//     /// </summary>
//     public static PythonBridgeService GetInstance(string pythonPath, string scriptsPath, ILogger<PythonBridgeService> logger)
//     {
//         if (_instance == null)
//         {
//             lock (_lock)
//             {
//                 if (_instance == null)
//                 {
//                     _instance = new PythonBridgeService(pythonPath, scriptsPath, logger);

//                     // Start initialization in background, but don't wait for it
//                     _ = Task.Run(async () =>
//                     {
//                         try
//                         {
//                             await _instance.InitializeAsync();
//                         }
//                         catch (Exception ex)
//                         {
//                             logger.LogError(ex, "Failed to initialize PythonBridge service in background");
//                         }
//                     });
//                 }
//             }
//         }
//         return _instance;
//     }

//     public bool IsReady => _isInitialized && _bridge != null && !_disposed;

//     public PythonRuntimeInfo RuntimeInfo => _bridge?.GetRuntimeInfoAsync().Result ?? new PythonRuntimeInfo
//     {
//         IsInitialized = false,
//         Error = "Bridge not initialized"
//     };

//     public async Task<bool> InitializeAsync()
//     {
//         if (_isInitialized || _disposed)
//             return _isInitialized;

//         await _initializationSemaphore.WaitAsync();
//         try
//         {
//             if (_isInitialized) // Double-check after acquiring lock
//                 return true;

//             _logger.LogInformation("Initializing PythonBridge service...");

//             // Add timeout to the underlying bridge initialization
//             using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 second timeout

//             _bridge = new PythonBridge(_pythonPath, _scriptsPath, _logger as ILogger<PythonBridge>);

//             // Wrap the initialization with timeout
//             var initTask = _bridge.InitializeAsync();
//             await initTask.WaitAsync(cts.Token);
//             var success = initTask.Result;

//             if (success)
//             {
//                 _isInitialized = true;
//                 _logger.LogInformation("PythonBridge service initialized successfully");

//                 // Warm up commonly used modules (with timeout)
//                 try
//                 {
//                     using var warmupCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
//                     await WarmupCommonModulesAsync();
//                 }
//                 catch (Exception warmupEx)
//                 {
//                     _logger.LogWarning(warmupEx, "Module warmup failed, but initialization succeeded");
//                 }
//             }
//             else
//             {
//                 _logger.LogError("Failed to initialize PythonBridge service");
//                 _bridge?.Dispose();
//                 _bridge = null;
//             }

//             return success;
//         }
//         catch (OperationCanceledException)
//         {
//             _logger.LogError("PythonBridge initialization timed out after 60 seconds");
//             _bridge?.Dispose();
//             _bridge = null;
//             return false;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Exception during PythonBridge initialization");
//             _bridge?.Dispose();
//             _bridge = null;
//             return false;
//         }
//         finally
//         {
//             _initializationSemaphore.Release();
//         }
//     }

//     public async Task<PythonExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, object>? parameters = null)
//     {
//         return await ExecuteWithMetricsAsync(async () =>
//         {
//             if (!await EnsureInitializedAsync())
//                 return PythonExecutionResult.Failure("Bridge not initialized");

//             return await _bridge!.ExecuteScriptAsync(scriptName, parameters);
//         });
//     }

//     public async Task<PythonExecutionResult> ExecuteCodeAsync(string pythonCode, Dictionary<string, object>? variables = null)
//     {
//         return await ExecuteWithMetricsAsync(async () =>
//         {
//             if (!await EnsureInitializedAsync())
//                 return PythonExecutionResult.Failure("Bridge not initialized");

//             return await _bridge!.ExecuteCodeAsync(pythonCode, variables);
//         });
//     }

//     public async Task<PythonExecutionResult> CallFunctionAsync(string moduleName, string functionName, params object[] arguments)
//     {
//         return await ExecuteWithMetricsAsync(async () =>
//         {
//             _logger.LogDebug("CallFunctionAsync: {Module}.{Function} with {ArgCount} arguments", moduleName, functionName, arguments?.Length ?? 0);

//             if (!await EnsureInitializedAsync())
//             {
//                 _logger.LogError("CallFunctionAsync: Bridge not initialized for {Module}.{Function}", moduleName, functionName);
//                 return PythonExecutionResult.Failure("Bridge not initialized");
//             }

//             try
//             {
//                 _logger.LogDebug("CallFunctionAsync: About to call underlying bridge for {Module}.{Function}", moduleName, functionName);
//                 var result = await _bridge!.CallFunctionAsync(moduleName, functionName, arguments);
//                 _logger.LogDebug("CallFunctionAsync: Call completed for {Module}.{Function}, Success: {Success}", moduleName, functionName, result.IsSuccess);
//                 return result;
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "CallFunctionAsync: Exception in {Module}.{Function}: {Message}", moduleName, functionName, ex.Message);
//                 return PythonExecutionResult.Failure($"Exception in {moduleName}.{functionName}: {ex.Message}");
//             }
//         });
//     }

//     public async Task<PythonExecutionResult> CallStreamingFunctionAsync(
//         string moduleName,
//         string functionName,
//         object request,
//         Func<string, Task> onChunkReceived,
//         CancellationToken cancellationToken = default)
//     {
//         if (!await EnsureInitializedAsync())
//             return PythonExecutionResult.Failure("Bridge not initialized");

//         var stopwatch = Stopwatch.StartNew();

//         try
//         {
//             _metrics.TotalExecutions++;
//             _metrics.LastExecutionTime = DateTime.UtcNow;

//             using (Py.GIL())
//             {
//                 try
//                 {
//                     // Get or cache the module
//                     PyObject module;
//                     if (!_moduleCache.TryGetValue(moduleName, out module))
//                     {
//                         module = Py.Import(moduleName);
//                         _moduleCache.TryAdd(moduleName, module);
//                     }

//                     // Check if the function exists
//                     if (!module.HasAttr(functionName))
//                     {
//                         var error = $"Function '{functionName}' not found in module '{moduleName}'";
//                         _metrics.FailedExecutions++;
//                         _metrics.LastError = error;
//                         return PythonExecutionResult.Failure(error);
//                     }

//                     // Create C# callback wrapper that Python can call
//                     Action<string> callback = chunk =>
//                     {
//                         try
//                         {
//                             // Fire and forget the async callback
//                             _ = Task.Run(async () => await onChunkReceived(chunk), cancellationToken);
//                         }
//                         catch (Exception ex)
//                         {
//                             _logger.LogWarning(ex, "Error in streaming callback: {Message}", ex.Message);
//                         }
//                     };

//                     // Convert callback to Python callable
//                     dynamic pythonCallback = callback.ToPython();

//                     // Call the streaming function with request and callback
//                     dynamic function = module.GetAttr(functionName);
//                     var result = function(request.ToPython(), pythonCallback);

//                     _metrics.SuccessfulExecutions++;

//                     // Convert result to string if it's not null
//                     string resultString = result?.ToString() ?? "Streaming completed";
//                     return PythonExecutionResult.Success(resultString);
//                 }
//                 catch (PythonException pyEx)
//                 {
//                     var error = $"Python error in streaming function: {pyEx.Message}";
//                     _logger.LogError(pyEx, error);
//                     _metrics.FailedExecutions++;
//                     _metrics.LastError = error;
//                     return PythonExecutionResult.Failure(error);
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             var error = $"Streaming execution error: {ex.Message}";
//             _logger.LogError(ex, error);
//             _metrics.FailedExecutions++;
//             _metrics.LastError = error;
//             return PythonExecutionResult.Failure(error);
//         }
//         finally
//         {
//             stopwatch.Stop();
//             UpdateAverageExecutionTime(stopwatch.Elapsed);
//         }
//     }

//     public async Task<bool> ValidateHealthAsync()
//     {
//         try
//         {
//             if (!IsReady)
//                 return false;

//             // Fast, non-blocking health check
//             if (_bridge == null)
//                 return false;

//             // Check if Python runtime is available without executing code
//             bool isHealthy = false;

//             // Use a very short timeout to avoid blocking
//             using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
//             {
//                 try
//                 {
//                     // Quick check: verify Python runtime is responsive
//                     await Task.Run(() =>
//                     {
//                         using (Py.GIL())
//                         {
//                             // Just check if we can acquire GIL and access basic Python state
//                             var sysModule = Py.Import("sys");
//                             isHealthy = sysModule != null;
//                         }
//                     }, cts.Token);
//                 }
//                 catch (OperationCanceledException)
//                 {
//                     _logger.LogDebug("Health check timed out - Python runtime may be busy");
//                     isHealthy = false;
//                 }
//             }

//             _lastHealthCheck = DateTime.UtcNow;
//             return isHealthy;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogDebug(ex, "Health check failed: {Message}", ex.Message);
//             return false;
//         }
//     }

//     /// <summary>
//     /// Lightweight health check that only verifies service state without Python runtime interaction
//     /// </summary>
//     public bool IsHealthyQuick()
//     {
//         return IsReady &&
//                !_disposed &&
//                _bridge != null &&
//                _isInitialized &&
//                (DateTime.UtcNow - _lastHealthCheck).TotalMinutes < 10; // Consider healthy if checked within last 10 minutes
//     }

//     public PythonBridgeMetrics GetMetrics()
//     {
//         // Update memory usage
//         _metrics.MemoryUsageBytes = GC.GetTotalMemory(false);
//         return _metrics;
//     }

//     public async Task<bool> ResetAsync()
//     {
//         _logger.LogInformation("Resetting PythonBridge service...");

//         await _initializationSemaphore.WaitAsync();
//         try
//         {
//             // Clean up existing bridge
//             if (_bridge != null)
//             {
//                 _bridge.Dispose();
//                 _bridge = null;
//             }

//             // Clear module cache
//             _moduleCache.Clear();

//             _isInitialized = false;

//             // Re-initialize
//             return await InitializeAsync();
//         }
//         finally
//         {
//             _initializationSemaphore.Release();
//         }
//     }

//     private async Task<bool> EnsureInitializedAsync()
//     {
//         if (!_isInitialized)
//         {
//             try
//             {
//                 // Add timeout to prevent indefinite blocking
//                 using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
//                 var initTask = InitializeAsync();

//                 await initTask.WaitAsync(cts.Token);
//                 return initTask.Result;
//             }
//             catch (OperationCanceledException)
//             {
//                 _logger.LogError("PythonBridge initialization timed out after 30 seconds");
//                 return false;
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error during PythonBridge initialization");
//                 return false;
//             }
//         }
//         return true;
//     }

//     private async Task<PythonExecutionResult> ExecuteWithMetricsAsync(Func<Task<PythonExecutionResult>> execution)
//     {
//         var stopwatch = Stopwatch.StartNew();

//         try
//         {
//             _metrics.TotalExecutions++;
//             _metrics.LastExecutionTime = DateTime.UtcNow;

//             var result = await execution();

//             if (result.IsSuccess)
//             {
//                 _metrics.SuccessfulExecutions++;
//             }
//             else
//             {
//                 _metrics.FailedExecutions++;
//                 _metrics.LastError = result.ErrorMessage ?? "Unknown error";
//             }

//             return result;
//         }
//         finally
//         {
//             stopwatch.Stop();
//             UpdateAverageExecutionTime(stopwatch.Elapsed);
//         }
//     }

//     private void UpdateAverageExecutionTime(TimeSpan executionTime)
//     {
//         var currentAverage = _metrics.AverageExecutionTime.TotalMilliseconds;
//         var count = _metrics.TotalExecutions;
//         var newExecutionMs = executionTime.TotalMilliseconds;

//         // Calculate rolling average
//         var newAverage = ((currentAverage * (count - 1)) + newExecutionMs) / count;
//         _metrics.AverageExecutionTime = TimeSpan.FromMilliseconds(newAverage);
//     }

//     private async Task WarmupCommonModulesAsync()
//     {
//         try
//         {
//             _logger.LogDebug("Warming up common Python modules...");

//             // Warm up commonly used modules
//             var commonModules = new[] { "json", "sys", "os", "time" };

//             foreach (var moduleName in commonModules)
//             {
//                 try
//                 {
//                     using (Py.GIL())
//                     {
//                         var module = Py.Import(moduleName);
//                         _moduleCache.TryAdd(moduleName, module);
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogDebug(ex, "Failed to warm up module {Module}", moduleName);
//                 }
//             }

//             _logger.LogDebug("Module warmup completed");
//         }
//         catch (Exception ex)
//         {
//             _logger.LogWarning(ex, "Error during module warmup");
//         }
//     }

//     private async Task PerformHealthCheckAsync()
//     {
//         try
//         {
//             // First try the quick check
//             if (IsHealthyQuick())
//             {
//                 _logger.LogDebug("Quick health check passed");
//                 return;
//             }

//             // Only do full health check if quick check fails
//             _logger.LogDebug("Quick health check failed, performing full health validation...");
//             var isHealthy = await ValidateHealthAsync();
//             if (!isHealthy)
//             {
//                 _logger.LogWarning("Full health check failed, attempting to reset PythonBridge");
//                 await ResetAsync();
//             }
//             else
//             {
//                 _logger.LogDebug("Full health check passed after quick check failure");
//             }
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error during periodic health check");
//         }
//     }

//     public void Dispose()
//     {
//         if (_disposed)
//             return;

//         _logger.LogInformation("Disposing PythonBridge service...");

//         _disposed = true;

//         _healthCheckTimer?.Dispose();

//         // Clear module cache safely
//         try
//         {
//             if (_isInitialized && _bridge != null && PythonEngine.IsInitialized)
//             {
//                 using (Py.GIL())
//                 {
//                     foreach (var module in _moduleCache.Values)
//                     {
//                         try
//                         {
//                             module?.Dispose();
//                         }
//                         catch (Exception ex)
//                         {
//                             _logger.LogWarning(ex, "Error disposing cached Python module");
//                         }
//                     }
//                 }
//             }
//             _moduleCache.Clear();
//         }
//         catch (Exception ex)
//         {
//             _logger.LogWarning(ex, "Error clearing module cache during disposal");
//             _moduleCache.Clear(); // Clear anyway
//         }

//         // Dispose bridge last
//         try
//         {
//             _bridge?.Dispose();
//         }
//         catch (Exception ex)
//         {
//             _logger.LogWarning(ex, "Error disposing Python bridge");
//         }

//         _initializationSemaphore?.Dispose();

//         // Clear singleton instance
//         lock (_lock)
//         {
//             _instance = null;
//         }

//         _logger.LogInformation("PythonBridge service disposed");
//     }
// }
