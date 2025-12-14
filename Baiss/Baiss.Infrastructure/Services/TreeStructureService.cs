// using Baiss.Infrastructure.Interop;
using Microsoft.Extensions.Logging;
using Baiss.Application.Interfaces;
using Baiss.Application.Models;
using System.Threading;
using System.Text.Json;
using Baiss.Domain.Entities;


namespace Baiss.Infrastructure.Services;

public class TreeStructureService : ITreeStructureService
{
	private static readonly SemaphoreSlim _threadLock = new SemaphoreSlim(1, 1);
	private readonly ILogger<TreeStructureService> _logger;
	// private readonly IPythonBridgeService _pythonBridgeService;

	private static ILaunchServerService? _launchServerService;

	private readonly IExternalApiService _externalApiService;


	// private static ISettingsRepository? _settingsRepository;
	// private static IModelRepository? _modelRepository;

	public TreeStructureService(ILogger<TreeStructureService> logger, ILaunchServerService launchServerService, IExternalApiService externalApiService)
	{
		_logger = logger;
		// _pythonBridgeService = pythonBridgeService;
		_launchServerService = launchServerService;
		_externalApiService = externalApiService;
		_logger.LogInformation("TreeStructureService initialized with dependencies");
	}



	public async Task UpdateTreeStructureAsync(List<string> paths, List<string> extensions, CancellationToken cancellationToken = default)
	{
		await _threadLock.WaitAsync();
		Settings? settings = null;

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Check if server is online before starting tree structure
			var isServerOnline = await _externalApiService.CheckServerStatus();
			if (!isServerOnline)
			{
				_logger.LogWarning("Cannot start tree structure: Server is offline");
				throw new InvalidOperationException("Server is starting, Please wait 30 seconds.");
			}

			var host = _launchServerService.GetServerUrl("embedding");
			var result = await _externalApiService.StartTreeStructureAsync(paths, extensions, host, cancellationToken);

		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Tree structure update was cancelled");
			throw; // Re-throw so caller knows it was cancelled
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Server is offline"))
		{
			_logger.LogWarning("Tree structure operation blocked: {Message}", ex.Message);
			throw; // Re-throw so caller can handle it appropriately
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected exception during tree structure operation: {Message}", ex.Message);
			throw;
		}
		finally
		{
			_threadLock.Release();
		}
	}
	public async Task DeleteFromTreeStructureAsync(List<string> paths)
	{
		try
		{
			var result = await _externalApiService.RemoveTreeStructureAsync(paths, new List<string>());
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected exception during delete operation: {Message}", ex.Message);
			return;
		}
	}

	public async Task DeleteFromTreeStructureWithExtensionsAsync(List<string> extensions)
	{
		try
		{

			var result = await _externalApiService.RemoveTreeStructureAsync(new List<string>(), extensions);

		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected exception during delete by extensions operation: {Message}", ex.Message);
			return;
		}
	}
}
