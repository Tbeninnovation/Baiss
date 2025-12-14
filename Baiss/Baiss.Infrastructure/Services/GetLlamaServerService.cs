using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Services;

public class GetLlamaServerService : IGetLlamaServerService
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<GetLlamaServerService> _logger;
	private readonly ConfigeDto _config;
	string llamaServerUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b7058/";
	string macosArmUrl = "llama-b7058-bin-macos-arm64.zip";
	string macosX64Url = "llama-b7058-bin-macos-x64.zip";
	string windowsArmUrl = "llama-b7058-bin-win-cpu-arm64.zip";
	string windowsX64Url = "llama-b7058-bin-win-cpu-x64.zip";
	string amdUrl = "llama-b7058-bin-win-hip-radeon-x64.zip";
	string nvidiaUrl = "llama-b7058-bin-win-cuda-12.4-x64.zip";

	public GetLlamaServerService(IHttpClientFactory httpClientFactory, ILogger<GetLlamaServerService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;

		// Load configuration from file
		string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
		string configFilePath = Path.Combine(appDirectory, "baiss_config.json");

		if (File.Exists(configFilePath))
		{
			string jsonString = File.ReadAllText(configFilePath);
			_config = JsonSerializer.Deserialize<ConfigeDto>(jsonString) ?? new ConfigeDto();
		}
		else
		{
			_logger.LogWarning("Configuration file not found at {ConfigPath}, using default config", configFilePath);
			_config = new ConfigeDto();
		}
	}


	public Task<string> GetLlamaServerInfoAsync()
	{
		string downloadUrl = string.Empty;

		if (OperatingSystem.IsMacOS())
		{
			if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
			{
				downloadUrl = llamaServerUrl + macosArmUrl;
			}
			else
			{
				downloadUrl = llamaServerUrl + macosX64Url;
			}
		}
		else if (OperatingSystem.IsWindows())
		{
			if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
			{
				downloadUrl = llamaServerUrl + windowsArmUrl;
			}
			else
			{
				string gpuType = DetermineGpuType(_config.GpuInfo);

				if (gpuType == "amd")
				{
					downloadUrl = llamaServerUrl + amdUrl;
				}
				else if (gpuType == "nvidia")
				{
					downloadUrl = llamaServerUrl + nvidiaUrl;
				}
				else
				{
					downloadUrl = llamaServerUrl + windowsX64Url;
				}
			}
		}
		else
		{
			throw new PlatformNotSupportedException("This operating system is not supported for Llama server downloads.");
		}

		_logger.LogInformation("Determined Llama server download URL: {DownloadUrl}", downloadUrl);
		return Task.FromResult(downloadUrl);
	}

	private string DetermineGpuType(object? gpuInfo)
	{
		if (gpuInfo == null)
		{
			_logger.LogWarning("GPU info is null, defaulting to CPU");
			return "cpu";
		}

		try
		{
			// Convert the object to JSON and then parse it
			string jsonString = JsonSerializer.Serialize(gpuInfo);
			_logger.LogDebug("GPU info JSON: {GpuJson}", jsonString);

			// Check if it's an array (multiple GPUs) or a single object
			if (jsonString.TrimStart().StartsWith("["))
			{
				// It's an array, parse as array and check the first GPU
				var gpuArray = JsonSerializer.Deserialize<JsonElement[]>(jsonString);
				if (gpuArray != null && gpuArray.Length > 0)
				{
					return ExtractGpuTypeFromElement(gpuArray[0]);
				}
			}
			else
			{
				// It's a single object
				var gpuElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
				return ExtractGpuTypeFromElement(gpuElement);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to determine GPU type from GPU info");
		}

		return "cpu"; // Default fallback
	}

	private string ExtractGpuTypeFromElement(JsonElement gpuElement)
	{
		try
		{
			if (gpuElement.TryGetProperty("Name", out JsonElement nameElement))
			{
				string gpuName = nameElement.GetString()?.ToLowerInvariant() ?? "";
				_logger.LogDebug("Analyzing GPU name: {GpuName}", gpuName);

				if (gpuName.Contains("nvidia") || gpuName.Contains("geforce") || gpuName.Contains("quadro") || gpuName.Contains("rtx") || gpuName.Contains("gtx"))
				{
					_logger.LogInformation("Detected NVIDIA GPU: {GpuName}", gpuName);
					return "nvidia";
				}
				else if (gpuName.Contains("amd") || gpuName.Contains("radeon") || gpuName.Contains("rx "))
				{
					_logger.LogInformation("Detected AMD GPU: {GpuName}", gpuName);
					return "amd";
				}
				// else if (gpuName.Contains("intel") && (gpuName.Contains("arc") || gpuName.Contains("xe")))
				// {
				// 	_logger.LogInformation("Detected Intel Arc GPU: {GpuName}, treating as CPU", gpuName);
				// 	return "cpu";
				// }
				else
				{
					_logger.LogInformation("Unknown GPU type: {GpuName}, defaulting to CPU", gpuName);
					return "cpu";
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to extract GPU type from GPU element");
		}

		return "cpu"; // Default fallback
	}

	/// <summary>
	/// Downloads the llama-server archive for the current platform
	/// </summary>
	/// <param name="destinationPath">The directory where the archive should be downloaded</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The path to the downloaded file</returns>
	public async Task<string> DownloadLlamaServerAsync(string destinationPath, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get the download URL for the current platform
			string downloadUrl = await GetLlamaServerInfoAsync();

			// Extract filename from URL
			string fileName = downloadUrl.Split('/').Last();
			string filePath = Path.Combine(destinationPath, fileName);

			// Create destination directory if it doesn't exist
			Directory.CreateDirectory(destinationPath);

			_logger.LogInformation("Starting download of llama-server from: {Url}", downloadUrl);

			using var httpClient = _httpClientFactory.CreateClient();

			// Set a reasonable timeout for large file downloads
			httpClient.Timeout = TimeSpan.FromMinutes(30);

			using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			response.EnsureSuccessStatusCode();

			// Get total file size for progress reporting
			var totalBytes = response.Content.Headers.ContentLength ?? 0;
			_logger.LogInformation("Download started. File size: {Size} bytes", totalBytes);

			using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);

			var buffer = new byte[8192];
			long totalBytesRead = 0;
			int bytesRead;
			var lastProgressReport = DateTime.UtcNow;

			while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
			{
				await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
				totalBytesRead += bytesRead;

				// Report progress every 5 seconds
				if (DateTime.UtcNow - lastProgressReport > TimeSpan.FromSeconds(5))
				{
					var progressPercent = totalBytes > 0 ? (double)totalBytesRead / totalBytes * 100 : 0;
					_logger.LogInformation("Download progress: {Progress:F1}% ({BytesRead}/{TotalBytes} bytes)",
						progressPercent, totalBytesRead, totalBytes);
					lastProgressReport = DateTime.UtcNow;
				}
			}

			_logger.LogInformation("Download completed successfully: {FilePath}", filePath);
			return filePath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to download llama-server");
			throw;
		}
	}

	/// <summary>
	/// Extracts the downloaded llama-server archive
	/// </summary>
	/// <param name="archivePath">Path to the downloaded zip archive</param>
	/// <param name="extractionPath">Directory where the archive should be extracted</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The path to the extracted directory</returns>
	public async Task<string> ExtractLlamaServerAsync(string archivePath, string extractionPath, CancellationToken cancellationToken = default)
	{
		try
		{
			if (!File.Exists(archivePath))
			{
				throw new FileNotFoundException($"Archive file not found: {archivePath}");
			}

			// Create extraction directory if it doesn't exist
			Directory.CreateDirectory(extractionPath);

			_logger.LogInformation("Starting extraction of archive: {ArchivePath} to {ExtractionPath}", archivePath, extractionPath);

			await Task.Run(() =>
			{
				using var archive = ZipFile.OpenRead(archivePath);

				foreach (var entry in archive.Entries)
				{
					cancellationToken.ThrowIfCancellationRequested();

					if (string.IsNullOrEmpty(entry.Name))
					{
						// This is a directory entry
						continue;
					}

					string destinationPath = Path.Combine(extractionPath, entry.FullName);

					// Ensure the destination directory exists
					string? destinationDir = Path.GetDirectoryName(destinationPath);
					if (!string.IsNullOrEmpty(destinationDir))
					{
						Directory.CreateDirectory(destinationDir);
					}

					// Extract the file
					entry.ExtractToFile(destinationPath, overwrite: true);

					// Set executable permissions on Unix systems for executables
					if (!OperatingSystem.IsWindows() && IsExecutableFile(entry.Name))
					{
						SetExecutablePermission(destinationPath);
					}

					_logger.LogDebug("Extracted: {EntryName}", entry.FullName);
				}
			}, cancellationToken);

			_logger.LogInformation("Extraction completed successfully to: {ExtractionPath}", extractionPath);
			return extractionPath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to extract llama-server archive");
			throw;
		}
	}

	/// <summary>
	/// Downloads and extracts the llama-server in one operation
	/// </summary>
	/// <param name="destinationBasePath">Base directory where both download and extraction will occur</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The path to the extracted llama-server directory</returns>
	public async Task<string> DownloadAndExtractLlamaServerAsync(string destinationBasePath, CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogInformation("Starting llama-server download and extraction process");
			string executableName = OperatingSystem.IsWindows() ? "llama-server.exe" : "llama-server";
			string? llamaServerExecutable = Directory.GetFiles(destinationBasePath, executableName, SearchOption.AllDirectories).FirstOrDefault();
			if (llamaServerExecutable != null)
			{
				_logger.LogInformation("Llama-server already exists at: {LlamaServerExecutable}", llamaServerExecutable);
				return Path.GetDirectoryName(llamaServerExecutable) ?? string.Empty;
			}

			// Create temporary download directory
			string downloadPath = Path.Combine(destinationBasePath, "downloads");
			string extractPath = Path.Combine(destinationBasePath, "llama-cpp");

			// Download the archive
			string archivePath = await DownloadLlamaServerAsync(downloadPath, cancellationToken);

			// Extract the archive
			string extractedPath = await ExtractLlamaServerAsync(archivePath, extractPath, cancellationToken);

			// Optionally clean up the downloaded archive
			try
			{
				File.Delete(archivePath);
				_logger.LogInformation("Cleaned up downloaded archive: {ArchivePath}", archivePath);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to clean up downloaded archive: {ArchivePath}", archivePath);
			}

			_logger.LogInformation("Llama-server download and extraction completed successfully: {ExtractedPath}", extractedPath);
			// set extracted path in baiss_config.json


			// search to llama-server executable file in extractedPath

			llamaServerExecutable = Directory.GetFiles(extractedPath, executableName, SearchOption.AllDirectories).FirstOrDefault();
			if (llamaServerExecutable != null)
			{
				_config.LlamaCppServerPath = llamaServerExecutable;
				string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
				string configFilePath = Path.Combine(appDirectory, "baiss_config.json");
				string jsonString = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(configFilePath, jsonString);
			}
			return extractedPath;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to download and extract llama-server");
			throw;
		}
	}

	/// <summary>
	/// Checks if a file is likely to be executable based on its name
	/// </summary>
	/// <param name="fileName">The name of the file</param>
	/// <returns>True if the file is likely executable</returns>
	private bool IsExecutableFile(string fileName)
	{
		var executableNames = new[]
		{
			"llama-server", "llama-cli", "llama-bench", "llama-quantize",
			"llama-perplexity", "llama-batched-bench", "llama-export-lora",
			"llama-cvector-generator", "llama-gguf-split", "llama-imatrix",
			"llama-llava-cli", "llama-minicpmv-cli", "llama-mtmd-cli",
			"llama-qwen2vl-cli", "llama-run", "llama-tokenize", "llama-tts",
			"rpc-server"
		};

		string fileNameLower = fileName.ToLowerInvariant();
		return executableNames.Any(exec => fileNameLower.Contains(exec)) ||
			   (!fileName.Contains('.') && !fileName.EndsWith(".txt") && !fileName.EndsWith(".md"));
	}

	/// <summary>
	/// Sets executable permission on Unix systems
	/// </summary>
	/// <param name="filePath">Path to the file</param>
	private void SetExecutablePermission(string filePath)
	{
		try
		{
			if (!OperatingSystem.IsWindows())
			{
				// Use chmod to set executable permission (755)
				var processStartInfo = new System.Diagnostics.ProcessStartInfo
				{
					FileName = "chmod",
					Arguments = $"+x \"{filePath}\"",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = System.Diagnostics.Process.Start(processStartInfo);
				process?.WaitForExit(5000); // 5 second timeout
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to set executable permission for: {FilePath}", filePath);
		}
	}

}
