// using Python.Runtime;
// using System.Diagnostics;
// using Microsoft.Extensions.Logging;
// using Baiss.Application.Models;

// namespace Baiss.Infrastructure.Interop;

// /// <summary>
// /// Bridge for interacting with Python runtime and scripts
// /// </summary>
// public class PythonBridge : IDisposable
// {
//     private static bool _engineShutdownInitiated = false;
//     private static readonly object _shutdownLock = new object();

//     private readonly string _pythonPath;
//     private readonly string _scriptsPath;
//     private readonly ILogger<PythonBridge> _logger;
//     private bool _isInitialized;
//     private bool _disposed;
//     private IntPtr _mainThreadState = IntPtr.Zero;
//     private int _initializationThreadId = -1;

//     public PythonBridge(string pythonPath, string scriptsPath, ILogger<PythonBridge>? logger = null)
//     {
//         _pythonPath = pythonPath ?? throw new ArgumentNullException(nameof(pythonPath));
//         _scriptsPath = scriptsPath ?? throw new ArgumentNullException(nameof(scriptsPath));
//         _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PythonBridge>.Instance;
//     }

//     ~PythonBridge()
//     {
//         // Finalizer to ensure cleanup happens even if Dispose is not called
//         Dispose();
//     }

//     /// <summary>
//     /// Initializes the Python runtime
//     /// </summary>
//     public async Task<bool> InitializeAsync()
//     {
//         try
//         {
//             if (_isInitialized)
//                 return true;

//             // Validate Python installation
//             if (!await ValidatePythonInstallationAsync())
//             {
//                 throw new InvalidOperationException("Python installation not found or invalid");
//             }

//             // Set Python DLL path if needed
//             if (!string.IsNullOrEmpty(_pythonPath))
//             {
//                 Runtime.PythonDLL = Path.Combine(_pythonPath, GetPythonDllName());
//             }

//             // Configure .NET Core runtime before initializing Python engine
//             if (!PythonEngine.IsInitialized)
//             {
//                 // Set the runtime to use .NET Core instead of Mono on macOS
//                 if (Environment.OSVersion.Platform == PlatformID.Unix)
//                 {
//                     // This tells Python.NET to use .NET Core on macOS/Linux
//                     Environment.SetEnvironmentVariable("PYTHONNET_RUNTIME", "coreclr");
//                 }

//                 // Initialize Python engine
//                 PythonEngine.Initialize();
//             }

//             // Add scripts path and baiss modules to Python path
//             using (Py.GIL())
//             {
//                 dynamic sys = Py.Import("sys");
//                 dynamic os = Py.Import("os");

//                 // Add the main scripts path (endpoints directory)
//                 sys.path.insert(0, _scriptsPath);

//                 // Determine if we're in a packaged application
//                 string baseDir = AppContext.BaseDirectory;
//                 string packagedPythonSrc = Path.Combine(baseDir, "python", "python_minimal", "python_src");

//                 if (Directory.Exists(packagedPythonSrc))
//                 {
//                     // Packaged application - add the python_src directory and its subdirectories
//                     sys.path.insert(0, packagedPythonSrc);

//                     // Add baiss module paths from packaged location
//                     string[] baissModules = { "baiss_sdk", "baiss_agents", "baiss_chatproxy", "baiss_sandbox" };
//                     foreach (string module in baissModules)
//                     {
//                         string modulePath = Path.Combine(packagedPythonSrc, module);
//                         if (Directory.Exists(modulePath))
//                         {
//                             sys.path.insert(0, modulePath);
//                         }
//                     }
//                 }
//                 else
//                 {
//                     // Development environment - use AppContext.BaseDirectory to avoid single-file issues
//                     // Get the solution root directory by going up from the base directory
//                     var currentAssemblyPath = AppContext.BaseDirectory;
//                     // Navigate up to find the solution root (where Baiss.sln is located)
//                     var solutionRoot = currentAssemblyPath;
//                     while (solutionRoot != null && !File.Exists(Path.Combine(solutionRoot, "Baiss.sln")))
//                     {
//                         solutionRoot = Path.GetDirectoryName(solutionRoot);
//                     }

//                     // The desktop-app folder should be at the same level as the Baiss folder
//                     var desktopAppPath = solutionRoot != null
//                         ? Path.Combine(Path.GetDirectoryName(solutionRoot) ?? "", "desktop-app")
//                         : Path.GetDirectoryName(_scriptsPath) ?? _scriptsPath;

//                     // Add the desktop-app path first (this contains baisstools.py)
//                     sys.path.insert(0, desktopAppPath);

//                     var baissSDKPath = Path.Combine(desktopAppPath, "baiss_sdk");
//                     var baissAgentsPath = Path.Combine(desktopAppPath, "baiss_agents");
//                     var baissProxyPath = Path.Combine(desktopAppPath, "baiss_chatproxy");
//                     var baissSandboxPath = Path.Combine(desktopAppPath, "baiss_sandbox");

//                     // Add paths if they exist
//                     string[] devPaths = { baissSDKPath, baissAgentsPath, baissProxyPath, baissSandboxPath };
//                     foreach (string path in devPaths)
//                     {
//                         if (Directory.Exists(path))
//                         {
//                             sys.path.insert(0, path);
//                         }
//                     }
//                 }

//                 if (OperatingSystem.IsWindows())
//                 {
//                     try
//                     {
//                         dynamic asyncio = Py.Import("asyncio");
//                         if (asyncio.HasAttr("WindowsSelectorEventLoopPolicy"))
//                         {
//                             dynamic policy = asyncio.WindowsSelectorEventLoopPolicy();
//                             asyncio.set_event_loop_policy(policy);
//                             _logger.LogDebug("Configured asyncio WindowsSelectorEventLoopPolicy for background threads");
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         _logger.LogWarning(ex, "Failed to configure asyncio selector policy on Windows");
//                     }
//                 }
//             }

//             // Release the GIL held by the initialization thread so that other threads can acquire it
//             if (_mainThreadState == IntPtr.Zero && PythonEngine.IsInitialized)
//             {
//                 _initializationThreadId = Environment.CurrentManagedThreadId;
//                 _mainThreadState = PythonEngine.BeginAllowThreads();
//                 _logger.LogDebug("Python GIL released by initialization thread {ThreadId}", _initializationThreadId);
//             }

//             _isInitialized = true;
//             return true;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Failed to initialize Python: {Message}", ex.Message);
//             return false;
//         }
//     }

//     /// <summary>
//     /// Executes a Python script and returns the result
//     /// </summary>
//     public async Task<PythonExecutionResult> ExecuteScriptAsync(string scriptName, Dictionary<string, object>? parameters = null)
//     {
//         if (!_isInitialized)
//         {
//             var initialized = await InitializeAsync();
//             if (!initialized)
//             {
//                 return PythonExecutionResult.Failure("Python runtime not initialized");
//             }
//         }

//         try
//         {
//             return await Task.Run(() =>
//             {
//                 using (Py.GIL())
//                 {
//                     try
//                     {
//                         // Import the script module
//                         dynamic module = Py.Import(Path.GetFileNameWithoutExtension(scriptName));

//                         // Convert parameters to Python objects
//                         var pythonParams = new Dictionary<string, PyObject>();
//                         if (parameters != null)
//                         {
//                             foreach (var param in parameters)
//                             {
//                                 pythonParams[param.Key] = param.Value.ToPython();
//                             }
//                         }

//                         // Execute the main function if it exists
//                         if (module.HasAttr("main"))
//                         {
//                             var result = pythonParams.Count > 0
//                                 ? module.main(pythonParams.ToPython())
//                                 : module.main();

//                             return PythonExecutionResult.Success(result?.ToString());
//                         }
//                         else
//                         {
//                             // Just import the module (execute at module level)
//                             return PythonExecutionResult.Success("Module executed successfully");
//                         }
//                     }
//                     catch (PythonException pyEx)
//                     {
//                         return PythonExecutionResult.Failure($"Python error: {pyEx.Message}");
//                     }
//                 }
//             });
//         }
//         catch (Exception ex)
//         {
//             return PythonExecutionResult.Failure($"Execution error: {ex.Message}");
//         }
//     }

//     /// <summary>
//     /// Executes Python code directly
//     /// </summary>
//     public async Task<PythonExecutionResult> ExecuteCodeAsync(string pythonCode, Dictionary<string, object>? variables = null)
//     {
//         if (!_isInitialized)
//         {
//             var initialized = await InitializeAsync();
//             if (!initialized)
//             {
//                 return PythonExecutionResult.Failure("Python runtime not initialized");
//             }
//         }

//         try
//         {
//             return await Task.Run(() =>
//             {
//                 using (Py.GIL())
//                 {
//                     try
//                     {
//                         using var scope = Py.CreateScope();

//                         // Set variables in scope
//                         if (variables != null)
//                         {
//                             foreach (var variable in variables)
//                             {
//                                 scope.Set(variable.Key, variable.Value.ToPython());
//                             }
//                         }

//                         // Execute the code
//                         var result = scope.Exec(pythonCode);

//                         // Try to get a return value if there's a 'result' variable
//                         try
//                         {
//                             var resultVar = scope.Get("result");
//                             return PythonExecutionResult.Success(resultVar?.ToString());
//                         }
//                         catch
//                         {
//                             return PythonExecutionResult.Success("Code executed successfully");
//                         }
//                     }
//                     catch (PythonException pyEx)
//                     {
//                         return PythonExecutionResult.Failure($"Python error: {pyEx.Message}");
//                     }
//                 }
//             });
//         }
//         catch (Exception ex)
//         {
//             return PythonExecutionResult.Failure($"Execution error: {ex.Message}");
//         }
//     }

//     /// <summary>
//     /// Extracts content from Python objects, handling special cases like JSONResponse
//     /// </summary>
//     private string? ExtractResponseContent(PyObject? pyObj)
//     {
//         if (pyObj == null)
//             return null;

//         try
//         {
//             using (Py.GIL())
//             {
//                 // Check if it's a JSONResponse (Starlette response)
//                 if (pyObj.HasAttr("body") && pyObj.HasAttr("status_code"))
//                 {
//                     // It's likely a JSONResponse object
//                     var body = pyObj.GetAttr("body");
//                     if (body != null)
//                     {
//                         // Convert bytes to string
//                         var bodyBytes = body.As<byte[]>();
//                         if (bodyBytes != null)
//                         {
//                             return System.Text.Encoding.UTF8.GetString(bodyBytes);
//                         }
//                         // Fallback to string conversion
//                         return body.ToString();
//                     }
//                 }

//                 // Check if it has a 'content' attribute (FastAPI Response)
//                 if (pyObj.HasAttr("content"))
//                 {
//                     var content = pyObj.GetAttr("content");
//                     if (content != null)
//                     {
//                         return content.ToString();
//                     }
//                 }

//                 // Handle Python dictionaries, lists, and other JSON-serializable objects
//                 try
//                 {
//                     dynamic json = Py.Import("json");
//                     var jsonStr = json.dumps(pyObj).ToString();
//                     return jsonStr;
//                 }
//                 catch (Exception jsonEx)
//                 {
//                     _logger.LogWarning(jsonEx, "Failed to serialize Python object to JSON, falling back to string conversion");
//                     // Default to string conversion as fallback
//                     return pyObj.ToString();
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             _logger.LogWarning(ex, "Failed to extract response content, falling back to string conversion");
//             return pyObj.ToString();
//         }
//     }

//     /// <summary>
//     /// Calls a specific function from a Python module
//     /// </summary>
//     public async Task<PythonExecutionResult> CallFunctionAsync(string moduleName, string functionName, params object[] arguments)
//     {
//         if (!_isInitialized)
//         {
//             var initialized = await InitializeAsync();
//             if (!initialized)
//             {
//                 return PythonExecutionResult.Failure("Python runtime not initialized");
//             }
//         }

//         using (Py.GIL())
//         {
//             try
//             {
//                 dynamic module = Py.Import(moduleName);

//                 // Check if the function exists in the module
//                 if (!module.HasAttr(functionName))
//                 {
//                     return PythonExecutionResult.Failure($"Function '{functionName}' not found in module '{moduleName}'");
//                 }

//                 dynamic function = module.GetAttr(functionName);

//                 // Convert arguments to Python objects and call the function
//                 PyObject result;
//                 if (arguments.Length > 0)
//                 {
//                     var pythonArgs = arguments.Select(arg => arg.ToPython()).ToArray();
//                     result = function.Invoke(pythonArgs);
//                 }
//                 else
//                 {
//                     result = function();
//                 }

//                 // Check if the result is a coroutine (async function)
//                 if (IsCoroutine(result))
//                 {
//                     // Import asyncio and run the coroutine
//                     dynamic asyncio = Py.Import("asyncio");

//                     // Try to get the current event loop, if it fails create a new one
//                     dynamic loop;
//                     try
//                     {
//                         loop = asyncio.get_event_loop();
//                     }
//                     catch
//                     {
//                         // Create a new event loop if none exists or current one is closed
//                         loop = asyncio.new_event_loop();
//                         asyncio.set_event_loop(loop);
//                     }

//                     // Run the coroutine
//                     var awaitedResult = loop.run_until_complete(result);
//                     return PythonExecutionResult.Success(ExtractResponseContent(awaitedResult));
//                 }
//                 else
//                 {
//                     // Regular synchronous function result
//                     return PythonExecutionResult.Success(ExtractResponseContent(result));
//                 }
//             }
//             catch (PythonException pyEx)
//             {
//                 return PythonExecutionResult.Failure($"Python error: {pyEx.Message}");
//             }
//             catch (Exception ex)
//             {
//                 return PythonExecutionResult.Failure($"Execution error: {ex.Message}");
//             }
//         }
//     }

//     /// <summary>
//     /// Checks if a Python object is a coroutine
//     /// </summary>
//     private bool IsCoroutine(PyObject obj)
//     {
//         try
//         {
//             using (Py.GIL())
//             {
//                 dynamic inspect = Py.Import("inspect");
//                 return inspect.iscoroutine(obj);
//             }
//         }
//         catch
//         {
//             return false;
//         }
//     }

//     /// <summary>
//     /// Validates Python installation
//     /// </summary>
//     private async Task<bool> ValidatePythonInstallationAsync()
//     {
//         try
//         {
//             // Check if python folder exists
//             if (!Directory.Exists(_pythonPath))
//             {
//                 _logger.LogWarning("Python path does not exist: {PythonPath}", _pythonPath);
//                 return false;
//             }

//             // Try to run python --version
//             var pythonExecutable = OperatingSystem.IsWindows() ? "python.exe" : "python3";
//             var pythonPath = Path.Combine(_pythonPath, "python.exe");

//             // On macOS, Python is in /bin/ subdirectory
//             if (!File.Exists(pythonPath) && OperatingSystem.IsMacOS())
//             {
//                 pythonPath = Path.Combine(_pythonPath , "python3");
//             }

//             var startInfo = new ProcessStartInfo
//             {
//                 FileName = pythonPath,
//                 Arguments = "--version",
//                 UseShellExecute = false,
//                 RedirectStandardOutput = true,
//                 RedirectStandardError = true,
//                 CreateNoWindow = true
//             };

//             using var process = Process.Start(startInfo);
//             if (process != null)
//             {
//                 await process.WaitForExitAsync();
//                 var output = await process.StandardOutput.ReadToEndAsync();
//                 _logger.LogInformation("Python validation result: {Output}", output);
//                 return process.ExitCode == 0;
//             }

//             return false;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Python validation failed");
//             return false;
//         }
//     }

//     /// <summary>
//     /// Gets the appropriate Python DLL name based on the platform
//     /// </summary>
//     private string GetPythonDllName()
//     {
//         if (OperatingSystem.IsWindows())
//         {
//             // Try to find python3*.dll in the Python directory
//             try
//             {
//                 var dllFiles = Directory.GetFiles(_pythonPath, "python3*.dll");
//                 // Prefer the specific version DLL over the generic python3.dll
//                 var specificDll = dllFiles.FirstOrDefault(f => f.Contains("313.dll"));
//                 if (specificDll != null)
//                 {
//                     return Path.GetFileName(specificDll);
//                 }

//                 // If no specific version found, use any available but prefer non-generic
//                 var nonGenericDll = dllFiles.FirstOrDefault(f => !Path.GetFileName(f).Equals("python3.dll"));
//                 if (nonGenericDll != null)
//                 {
//                     return Path.GetFileName(nonGenericDll);
//                 }

//                 // Fall back to any available
//                 if (dllFiles.Length > 0)
//                 {
//                     return Path.GetFileName(dllFiles[0]);
//                 }
//             }
//             catch
//             {
//                 // Log or handle the error if needed
//                 _logger.LogWarning("Failed to find specific Python DLL in path: {Path}", _pythonPath);
//                 // Fall back to default
//             }
//             return "python313.dll"; // Updated default for Python 3.13
//         }
//         else if (OperatingSystem.IsLinux())
//         {
//             return "libpython3.13.so";
//         }
//         else if (OperatingSystem.IsMacOS())
//         {
//             return _pythonPath + "python" ;  // macOS Python.org installations use a framework binary named "Python"
//         }

//         throw new PlatformNotSupportedException("Unsupported platform for Python.NET");
//     }

//     /// <summary>
//     /// Gets information about the Python runtime
//     /// </summary>
//     public async Task<PythonRuntimeInfo> GetRuntimeInfoAsync()
//     {
//         var info = new PythonRuntimeInfo
//         {
//             IsInitialized = _isInitialized,
//             PythonPath = _pythonPath,
//             ScriptsPath = _scriptsPath
//         };

//         if (_isInitialized)
//         {
//             try
//             {
//                 await Task.Run(() =>
//                 {
//                     using (Py.GIL())
//                     {
//                         dynamic sys = Py.Import("sys");
//                         info.PythonVersion = sys.version?.ToString() ?? "Unknown";
//                         info.PythonExecutable = sys.executable?.ToString() ?? "Unknown";
//                     }
//                 });
//             }
//             catch (Exception ex)
//             {
//                 info.Error = ex.Message;
//             }
//         }

//         return info;
//     }

//     public void Dispose()
//     {
//         if (_disposed)
//             return;

//         _logger.LogDebug("Disposing PythonBridge...");

//         try
//         {
//             if (_isInitialized && PythonEngine.IsInitialized)
//             {
//                 // Only restore thread state if we're on the same thread that released it
//                 if (_mainThreadState != IntPtr.Zero)
//                 {
//                     try
//                     {
//                         // Check if we're on the initialization thread
//                         if (Environment.CurrentManagedThreadId == _initializationThreadId)
//                         {
//                             PythonEngine.EndAllowThreads(_mainThreadState);
//                             _logger.LogDebug("Python thread state restored successfully on initialization thread {ThreadId}", _initializationThreadId);
//                         }
//                         else
//                         {
//                             _logger.LogWarning("Skipping thread state restoration - current thread {CurrentThreadId} != initialization thread {InitThreadId}",
//                                 Environment.CurrentManagedThreadId, _initializationThreadId);
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         _logger.LogWarning(ex, "Failed to restore Python thread state during dispose (current thread {CurrentThreadId}, init thread {InitThreadId})",
//                             Environment.CurrentManagedThreadId, _initializationThreadId);
//                     }
//                     finally
//                     {
//                         _mainThreadState = IntPtr.Zero;
//                     }
//                 }

//                 // Give a moment for any active Python operations to complete
//                 try
//                 {
//                     Thread.Sleep(100);
//                 }
//                 catch
//                 {
//                     // Ignore interruption during shutdown
//                 }

//                 // Shutdown Python engine
//                 lock (_shutdownLock)
//                 {
//                     if (!_engineShutdownInitiated && PythonEngine.IsInitialized)
//                     {
//                         try
//                         {
//                             _engineShutdownInitiated = true;
//                             PythonEngine.Shutdown();
//                             _logger.LogDebug("Python engine shutdown completed");
//                         }
//                         catch (Exception ex)
//                         {
//                             _logger.LogWarning(ex, "Error during Python engine shutdown");
//                         }
//                     }
//                     else
//                     {
//                         _logger.LogDebug("Python engine shutdown skipped - already initiated or not initialized");
//                     }
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error during PythonBridge disposal");
//         }
//         finally
//         {
//             _disposed = true;
//             _logger.LogDebug("PythonBridge disposal completed");
//             GC.SuppressFinalize(this); // Suppress finalizer since we've already cleaned up
//         }
//     }
// }
