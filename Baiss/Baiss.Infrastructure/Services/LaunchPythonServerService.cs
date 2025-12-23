
// import necessary namespaces
using Baiss.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Baiss.Application.DTOs;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;



namespace Baiss.Infrastructure.Services;

public class LaunchPythonServerService : ILaunchPythonServerService
{
	private readonly ILogger<LaunchPythonServerService> _logger;
	private Process? _pythonServerProcess;

	public int PythonServerPort { get; private set; } = 9911;

	public LaunchPythonServerService(ILogger<LaunchPythonServerService> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Process? LaunchPythonServer()
	{
		try
		{
			// Path to your Python executable
			string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string configFilePath = Path.Combine(appDirectory, "baiss_config.json");

			string jsonString = File.ReadAllText(configFilePath);
			var config = JsonSerializer.Deserialize<ConfigeDto>(jsonString);
			ChangePythonDirectoryPermissions(config?.PythonPath);

			string pythonPath;
			// check if mac or windows
			if (OperatingSystem.IsMacOS())
			{
				pythonPath = config?.PythonPath + "python3" ?? "python";
			}
			else if (OperatingSystem.IsLinux())
			{
				pythonPath = config?.PythonPath + "/python3" ?? "python3";
			}
			else if (OperatingSystem.IsWindows())
			{
				pythonPath = config?.PythonPath + "\\python.exe" ?? "python";
			}
			else
			{
				pythonPath = "python"; // Default fallback
			}

			// change path from
			var baissCorePath = config?.BaissPythonCorePath + "/baiss/shared/python/baiss_agents/run_local.py";
			if (OperatingSystem.IsWindows())
			{
				baissCorePath = config?.BaissPythonCorePath + "\\baiss\\shared\\python\\baiss_agents\\run_local.py";
			}

			_logger?.LogInformation($"Using Python executable at:       --- >>>> {pythonPath}");
			_logger?.LogInformation($"Starting Python server script at: --- >>>> {baissCorePath}");

			int actualPort = FindNextAvailablePort(9911);
			if (actualPort == -1)
			{
				_logger.LogError("No available port found for Python server.");
				return null;
			}
			else
			{
				PythonServerPort = actualPort;
				_logger.LogInformation("Starting Python server on port: {Port}", actualPort);
			}


			var processStartInfo = new ProcessStartInfo
			{
				FileName = pythonPath,
				Arguments = $"\"{baissCorePath}\" --port {PythonServerPort}",
				WorkingDirectory = Path.GetDirectoryName(baissCorePath),
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			_pythonServerProcess = Process.Start(processStartInfo);

			// Attach event handlers for output and error data "logs"
			if (_pythonServerProcess != null)
			{
				// Capture and log Python output
				_pythonServerProcess.OutputDataReceived += (sender, e) =>
				{
					if (!string.IsNullOrEmpty(e.Data))
					{
						_logger.LogInformation("Python Output: {Output}", e.Data);
					}
				};

				_pythonServerProcess.ErrorDataReceived += (sender, e) =>
				{
					if (!string.IsNullOrEmpty(e.Data))
					{
						// Check if it's actually an error or just uvicorn info logs
						if (e.Data.Contains("ERROR") || e.Data.Contains("Traceback") || e.Data.Contains("Exception"))
						{
							_logger.LogError("Python Error: {Error}", e.Data);
						}
						else
						{
							_logger.LogInformation("Python Info: {Info}", e.Data);
						}
					}
				};

				// Start asynchronous reading
				_pythonServerProcess.BeginOutputReadLine();
				_pythonServerProcess.BeginErrorReadLine();
			}

			// Log successful startup
			// Log the standard output and error streams
			_logger.LogInformation($"Python server started with PID: {_pythonServerProcess?.Id}");
			return _pythonServerProcess;
		}
		catch (Exception ex)
		{
			// Handle exceptions (app not found, etc.)
			_logger.LogError(ex, "Error launching Python server: {Message}", ex.Message);
			return null;
		}
	}



	private void ChangePythonDirectoryPermissions(string pythonDirectory)
	{
		try
		{
			var chmodProcess = new ProcessStartInfo
			{
				FileName = "/bin/chmod",
				Arguments = $"-R +x \"{pythonDirectory}\"",
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			using (var process = Process.Start(chmodProcess))
			{
				if (process != null)
				{
					process.WaitForExit();
					_logger.LogInformation($"Fixed permissions for Python directory: {pythonDirectory}");
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error changing permissions for Python directory: {Message}", ex.Message);
		}
	}

	public async Task<bool> StopPythonServerAsync()
	{
		if (_pythonServerProcess == null || _pythonServerProcess.HasExited)
		{
			return true;
		}

		try
		{
			_logger.LogInformation("Stopping Python server process with PID: {ProcessId}", _pythonServerProcess.Id);

			// Force kill it
			_pythonServerProcess.Kill(true); // true kills entire process tree

			if (await WaitForExitAsync(_pythonServerProcess, 1000))
			{
				_logger.LogInformation("Python server force-stopped successfully");
				return true;
			}

			_logger.LogError("Failed to stop Python server process");
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error stopping Python server process: {Message}", ex.Message);
			return false;
		}
		finally
		{
			_pythonServerProcess?.Dispose();
			_pythonServerProcess = null;
		}
	}

	public bool IsServerRunning()
	{
		return _pythonServerProcess != null && !_pythonServerProcess.HasExited;
	}

	private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs)
	{
		return await Task.Run(() => process.WaitForExit(timeoutMs));
	}

	private bool IsPortAvailable(int port)
	{
		try
		{
			TcpListener listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			listener.Stop();
			return true;
		}
		catch
		{
			return false;
		}
	}


	private int FindNextAvailablePort(int startPort, int maxAttempts = 100)
	{
		// Try to reclaim the start port if it's in use
		if (!IsPortAvailable(startPort))
		{
			_logger.LogInformation("Port {Port} is in use. Attempting to free it...", startPort);
			if (KillProcessOnPort(startPort))
			{
				// Give it a moment to release
				System.Threading.Thread.Sleep(1000);
				if (IsPortAvailable(startPort))
				{
					_logger.LogInformation("Successfully freed and claimed port {Port}", startPort);
				}
			}
			_logger.LogWarning("Could not free port {Port}. Searching for next available port.", startPort);
		}
		else
		{
			return startPort;
		}

		for (int port = startPort + 1; port < startPort + maxAttempts; port++)
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

	private bool KillProcessOnPort(int port)
	{
		try
		{
			if (OperatingSystem.IsWindows())
			{
				var processStartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = $"/c netstat -ano | findstr :{port}",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = Process.Start(processStartInfo);
				if (process == null) return false;

				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();

				if (string.IsNullOrWhiteSpace(output)) return false;

				var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var line in lines)
				{
					var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length > 4)
					{
						var pidStr = parts[parts.Length - 1];
						if (int.TryParse(pidStr, out int pid))
						{
							try
							{
								Process.GetProcessById(pid).Kill();
								return true;
							}
							catch { }
						}
					}
				}
			}
			else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
			{
				var processStartInfo = new ProcessStartInfo
				{
					FileName = "lsof",
					Arguments = $"-i :{port} -t",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = Process.Start(processStartInfo);
				if (process == null) return false;

				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();

				if (string.IsNullOrWhiteSpace(output)) return false;

				var pids = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var pidStr in pids)
				{
					if (int.TryParse(pidStr, out int pid))
					{
						try
						{
							Process.Start("kill", $"-9 {pid}").WaitForExit();
							return true;
						}
						catch { }
					}
				}
			}
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error killing process on port {Port}", port);
			return false;
		}
	}



}
