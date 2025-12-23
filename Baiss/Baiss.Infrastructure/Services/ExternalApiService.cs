using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Baiss.Application.Interfaces;
using Baiss.Application.DTOs;
// using DotNetEnv;
using System.Net.WebSockets;
using Baiss.Application.UseCases;
using Baiss.Infrastructure.Extensions;
using System.Diagnostics.Contracts;


public class ExternalApiService : IExternalApiService, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<ExternalApiService> _logger;
	private readonly IModelRepository _modelRepository;
	private readonly ISettingsRepository _settingsRepository;
	private readonly ILaunchServerService _launchServerService;
	private readonly ILaunchPythonServerService _launchPythonServerService;
	private Uri _webSocketEndpoint;
	private readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
	private bool _disposed = false;
	private readonly string _releaseInfoUrl;

	public List<Baiss.Application.DTOs.PathScoreDto> LastReceivedPaths { get; private set; } = new List<Baiss.Application.DTOs.PathScoreDto>();


	private string _baseUrl;
	private static readonly string chatEndpoint = "chatv2/chatv2";

	private static readonly string updateEndpoint = "check-update/";

	private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
	private static readonly ILogger _staticLogger = _loggerFactory.CreateLogger<ExternalApiService>();

	public ExternalApiService(HttpClient httpClient, ILogger<ExternalApiService> logger, IModelRepository modelRepository, ISettingsRepository settingsRepository, ILaunchServerService launchServerService, ILaunchPythonServerService launchPythonServerService)
	{
		_httpClient = httpClient;
		_logger = logger;
		_modelRepository = modelRepository;
		_settingsRepository = settingsRepository;
		_launchServerService = launchServerService;
		_launchPythonServerService = launchPythonServerService;

		var port = _launchPythonServerService.PythonServerPort;
		_baseUrl = $"http://localhost:{port}/ai/api/v1/";
		// _configChatService = configChatService;
		_webSocketEndpoint = new Uri($"ws://localhost:{port}/api/v1/chatv2/pre_chat");

		// Configure release info URL from environment variable or use default
		_releaseInfoUrl = Environment.GetEnvironmentVariable("BAISS_RELEASE_INFO_URL")
			?? "https://cdn.baiss.ai/update/release.json";
	}


	#region Chat Streaming

	public async IAsyncEnumerable<string> SendChatMessageStreamAsync(string message, List<MessageItem>? conversationContext = null, List<string>? filePaths = null)
	{
		// Clear previous paths at the start of a new request
		LastReceivedPaths.Clear();

		// Setup streaming
		var setupResult = await SetupStreamingRequestAsync(message, conversationContext, filePaths);
		if (!setupResult.Success || setupResult.WebSocket == null)
		{
			yield return $"[ERROR: {setupResult.ErrorMessage ?? "Failed to setup streaming"}]";
			yield break;
		}

		// Process streaming responses using separate method
		await foreach (var chunk in ProcessStreamingResponsesRealTimeAsync(setupResult.WebSocket))
		{
			yield return chunk;
		}
	}

	public async IAsyncEnumerable<string> SendChatMessageStreamLlamaCppAsync(string message, List<MessageItem>? conversationContext = null, List<string>? filePaths = null)
	{
		await foreach (var chunk in SendChatMessageStreamAsync(message, conversationContext, filePaths))
		{
			yield return chunk;
		}
	}

	private async IAsyncEnumerable<string> ProcessStreamingResponsesRealTimeAsync(ClientWebSocket webSocket)
	{
		var responseBuffer = new byte[8192];
		var hasError = false;
		string? errorMessage = null;

		while (webSocket.State == WebSocketState.Open && !hasError)
		{
			WebSocketReceiveResult wsResult;

			// Handle WebSocket receive with error handling
			try
			{
				wsResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), CancellationToken.None);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "WebSocket receive error: {Message}", ex.Message);
				errorMessage = ex.Message;
				hasError = true;
				break;
			}

			if (wsResult.MessageType == WebSocketMessageType.Close)
			{
				_logger.LogInformation("WebSocket streaming connection closed by server");
				break;
			}

			if (wsResult.MessageType == WebSocketMessageType.Text)
			{
				var responseContent = System.Text.Encoding.UTF8.GetString(responseBuffer, 0, wsResult.Count);
				// _logger.LogDebug("Received streaming chunk: {Chunk}", responseContent);

				var processedChunks = ProcessStreamingChunk(responseContent);

				// Store paths if any were received
				if (processedChunks.Paths.Any())
				{
					LastReceivedPaths = processedChunks.Paths;
					_logger.LogInformation("Received {Count} paths", processedChunks.Paths.Count);
				}

				// Yield code execution status as special marker
				if (processedChunks.CodeExecutionStatus.HasValue)
				{
					var status = processedChunks.CodeExecutionStatus.Value ? "success" : "error";
					var error = processedChunks.CodeExecutionError ?? "";
					yield return $"[CODE_EXEC:{status}:{error}]";
					_logger.LogInformation("Code execution status: {Status}, Error: {Error}", status, error);
				}

				// Yield chunks immediately as they arrive
				foreach (var textChunk in processedChunks.TextChunks)
				{
					if (!string.IsNullOrEmpty(textChunk))
					{
						yield return textChunk;
					}
				}

				// Check if streaming is complete
				if (processedChunks.IsComplete)
				{
					_logger.LogInformation("Streaming completed");
					break;
				}
			}
		}

		// Handle error outside the loop if there was one
		if (hasError && !string.IsNullOrEmpty(errorMessage))
		{
			yield return $"[ERROR: {errorMessage}]";
		}

		// Cleanup
		try
		{
			await CleanupWebSocketAsync(webSocket);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error during WebSocket cleanup: {Message}", ex.Message);
		}
	}
	private async Task<StreamingSetupResult> SetupStreamingRequestAsync(string message, List<MessageItem>? conversationContext, List<string>? filePaths = null)
	{
		ClientWebSocket? streamWebSocket = null;
		try
		{
			var messages = new List<object>();

			// Add conversation history if provided
			if (conversationContext != null && conversationContext.Any())
			{
				foreach (var msg in conversationContext)
				{
					messages.Add(new
					{
						role = msg.Role,
						content = string.Join(" ", msg.Content.Select(c => c.Text))
					});
				}
			}

			// Add current user message
			// messages.Add(new
			// {
			// 	role = "user",
			// 	content = message
			// });

			string urlllamacpp = _launchServerService.GetServerUrl("chat") ?? "http://127.0.0.1:8080";
			string urlllamacppEmbedding = _launchServerService.GetServerUrl("embedding") ?? "http://127.0.0.1:8081";

			var request = new ChatRequest
			{
				messages = messages,
				url = urlllamacpp,
				embedding_url = urlllamacppEmbedding,
				paths = filePaths ?? new List<string>()

			};


			// Send the message to WebSocket
			var jsonContent = JsonSerializer.Serialize(request);
			var buffer = System.Text.Encoding.UTF8.GetBytes(jsonContent);


			streamWebSocket = new ClientWebSocket();
			_logger.LogInformation("Connecting to WebSocket for streaming: {Endpoint}", _webSocketEndpoint);

			await streamWebSocket.ConnectAsync(_webSocketEndpoint, CancellationToken.None);

			await streamWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			return new StreamingSetupResult
			{
				Success = true,
				WebSocket = streamWebSocket
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "WebSocket streaming setup error: {Message}", ex.Message);
			streamWebSocket?.Dispose();

			return new StreamingSetupResult
			{
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}

	private ChunkProcessingResult ProcessStreamingChunk(string responseContent)
	{
		var result = new ChunkProcessingResult();

		try
		{
			// First, try to parse as a direct chunk (Python websocket format)
			using var jsonDoc = JsonDocument.Parse(responseContent);
			var root = jsonDoc.RootElement;

			// Check if this is a direct chunk format: {"success":true,"error":null,"response":{"choices":[...]}}
			if (root.TryGetProperty("success", out var successProp) &&
				root.TryGetProperty("response", out var responseProp))
			{
				var isSuccess = successProp.GetBoolean();

				if (isSuccess && responseProp.TryGetProperty("choices", out var choices))
				{
					// Process direct chunk format
					foreach (var choice in choices.EnumerateArray())
					{
						// Try to get messages first (current format)
						if (choice.TryGetProperty("messages", out var messages))
						{
							foreach (var message in messages.EnumerateArray())
							{
								if (message.TryGetProperty("content", out var contentArray))
								{
									foreach (var contentItem in contentArray.EnumerateArray())
									{
										if (contentItem.TryGetProperty("type", out var typeProp) &&
											typeProp.GetString() == "text" &&
											contentItem.TryGetProperty("text", out var textProp))
										{
											var text = textProp.GetString();
											if (!string.IsNullOrEmpty(text))
											{
												result.TextChunks.Add(text);
											}
										}
									}
								}
							}
						}
						// Also support delta format for flexibility
						else if (choice.TryGetProperty("delta", out var delta))
						{
							if (delta.TryGetProperty("content", out var contentArray))
							{
								foreach (var contentItem in contentArray.EnumerateArray())
								{
									if (contentItem.TryGetProperty("type", out var typeProp) &&
										typeProp.GetString() == "text" &&
										contentItem.TryGetProperty("text", out var textProp))
									{
										var text = textProp.GetString();
										if (!string.IsNullOrEmpty(text))
										{
											result.TextChunks.Add(text);
										}
									}
								}
							}
						}

						// Check for paths in the choice
						if (choice.TryGetProperty("paths", out var paths))
						{
							foreach (var pathItem in paths.EnumerateArray())
							{
								if (pathItem.TryGetProperty("path", out var pathProp) &&
									pathItem.TryGetProperty("score", out var scoreProp))
								{
									var path = pathProp.GetString();
									var score = scoreProp.GetDouble();
									if (!string.IsNullOrEmpty(path))
									{
										result.Paths.Add(new Baiss.Application.DTOs.PathScoreDto
										{
											Path = path,
											Score = score
										});
									}
								}
							}
						}
					}
				}

				// Check for code_execution_status in response (before returning)
				if (responseProp.TryGetProperty("code_execution_status", out var codeExecStatus))
				{
					result.CodeExecutionStatus = codeExecStatus.GetBoolean();
					if (responseProp.TryGetProperty("error", out var codeExecError) &&
					    codeExecError.ValueKind != JsonValueKind.Null)
					{
						result.CodeExecutionError = codeExecError.GetString();
					}
				}

				return result;
			}

			// Fallback: Try wrapped format (StreamingResponse with Data.Chunks)
			var streamingResponse = JsonSerializer.Deserialize<StreamingResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			if (streamingResponse?.Data?.Chunks != null)
			{
				// Extract text content from chunks
				foreach (var chunk in streamingResponse.Data.Chunks)
				{
					if (chunk.Success && chunk.Response?.Choices != null)
					{
						foreach (var choice in chunk.Response.Choices)
						{
							foreach (var msg in choice.Messages)
							{
								foreach (var content in msg.Content)
								{
									if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
									{
										result.TextChunks.Add(content.Text);
									}
								}
							}
						}
					}
				}
			}

			// Check if this is the end of the stream
			if (streamingResponse != null && !streamingResponse.Success && streamingResponse.Message.Contains("complete", StringComparison.OrdinalIgnoreCase))
			{
				result.IsComplete = true;
			}
		}
		catch (JsonException ex)
		{
			_logger.LogWarning("Failed to parse streaming response: {Error}", ex.Message);
			// Continue processing other chunks
		}

		return result;
	}

	private async Task CleanupWebSocketAsync(ClientWebSocket? webSocket)
	{
		try
		{
			if (webSocket?.State == WebSocketState.Open)
			{
				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Streaming complete", CancellationToken.None);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error while closing streaming WebSocket connection");
		}
		finally
		{
			webSocket?.Dispose();
		}
	}

	private class StreamingSetupResult
	{
		public bool Success { get; set; }
		public ClientWebSocket? WebSocket { get; set; }
		public HttpResponseMessage? HttpResponse { get; set; }
		public string? ErrorMessage { get; set; }
	}

	private class ChunkProcessingResult
	{
		public List<string> TextChunks { get; set; } = new List<string>();
		public List<Baiss.Application.DTOs.PathScoreDto> Paths { get; set; } = new List<Baiss.Application.DTOs.PathScoreDto>();
		public bool IsComplete { get; set; }

		// Code execution status: null = not a code execution message, true = success, false = error
		public bool? CodeExecutionStatus { get; set; }
		public string? CodeExecutionError { get; set; }
	}

	#endregion

	#region Update App

	public async Task<ReleaseInfoResponse?> CheckForUpdatesAsync(string currentVersion = "")
	{
		try
		{
			_logger.LogInformation("Checking for updates from endpoint: {Endpoint}", _releaseInfoUrl);

			var response = await _httpClient.GetAsync(_releaseInfoUrl);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Update check failed. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return null;
			}

			var updateResponse = JsonSerializer.Deserialize<ReleaseInfoResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			if (updateResponse == null)
			{
				_logger.LogError("Failed to deserialize update response");
				return null;
			}

			// _logger.LogInformation("Successfully retrieved release info. Current version: {Version}", updateResponse.CurrentVersion);
			return updateResponse;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error checking for updates");
			return null;
		}
	}

	#endregion

	#region Download Models


	// Get list of available models for download from huggingface
	public async Task<List<ModelInfo>> DownloadAvailableModelsAsync()
	{
		try
		{
			var endpoint = "https://cdn.baiss.ai/update/models.json";

			_logger.LogInformation("Fetching available models from {Endpoint}", endpoint);

			var response = await _httpClient.GetAsync(endpoint);
			var responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to retrieve available models. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new List<ModelInfo>();
			}

			var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			// Check if response is an array (legacy format) or object (wrapped format)
			var trimmedContent = responseContent.TrimStart();

			if (trimmedContent.StartsWith("["))
			{
				// Legacy: direct array of model entries
				var legacyModels = JsonSerializer.Deserialize<List<ModelInfo>>(responseContent, jsonOptions);
				if (legacyModels == null || !legacyModels.Any())
				{
					_logger.LogWarning("No models returned from API or deserialization failed");
					return new List<ModelInfo>();
				}

				_logger.LogInformation("Successfully retrieved {Count} available models (legacy array)", legacyModels.Count);
				return legacyModels;
			}

			// New backend response wraps data inside status/success/message/data
			AvailableModelsResponse? wrapped = null;
			try
			{
				wrapped = JsonSerializer.Deserialize<AvailableModelsResponse>(responseContent, jsonOptions);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to deserialize available models using wrapped response.");
				return new List<ModelInfo>();
			}

			if (wrapped?.Data?.Models is { Count: > 0 })
			{
				_logger.LogInformation("Successfully retrieved {Count} available models (wrapped response)", wrapped.Data.Models.Count);
				return wrapped.Data.Models;
			}

			_logger.LogWarning("No models returned from API");
			return new List<ModelInfo>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error calling available models API: {Message}", ex.Message);
			return new List<ModelInfo>();
		}
	}

	// Start model download by model ID
	public async Task<StartModelsDownloadResponse> StartModelDownloadAsync(string modelId, string? downloadUrl = null)
	{
		try
		{
			var endpoint = _baseUrl + "models/start";

			var requestBody = new Dictionary<string, string>
			{
				["model_id"] = modelId
			};

			if (!string.IsNullOrWhiteSpace(downloadUrl))
			{
				requestBody["model_id"] = downloadUrl;
			}

			var jsonContent = JsonSerializer.Serialize(requestBody);
			_logger.LogInformation("Sending StartModelDownload request: {Json}", jsonContent);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync(endpoint, httpContent);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to start model download. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new StartModelsDownloadResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Message = "",
					Error = responseContent
				};
			}

			var downloadResponse = JsonSerializer.Deserialize<StartModelsDownloadResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return downloadResponse ?? new StartModelsDownloadResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling start model download API: {Message}", ex.Message);
			return new StartModelsDownloadResponse()
			{
				Success = false,
				Status = 500,
				Message = "",
				Error = "An unexpected error occurred"
			};
		}
	}

	// Get list of current model downloads and their status
	public async Task<ModelDownloadListResponse> GetModelDownloadListAsync()
	{
		try
		{
			var endpoint = _baseUrl + "models/list/progress";

			var response = await _httpClient.GetAsync(endpoint);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to retrieve model download list. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new ModelDownloadListResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Message = "",
					Error = responseContent
				};
			}

			var listResponse = JsonSerializer.Deserialize<ModelDownloadListResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return listResponse ?? new ModelDownloadListResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling model download list API: {Message}", ex.Message);
			return new ModelDownloadListResponse()
			{
				Success = false,
				Status = 500,
				Message = "",
				Error = "An unexpected error occurred"
			};
		}
	}

	public async Task<ModelsListResponse> GetModelsListExistsAsync()
	{
		try
		{
			await Task.Delay(5000);
			var endpoint = _baseUrl + "models/list";
			var requestBody = new { };
			var jsonContent = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync(endpoint, httpContent);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to retrieve existing model list. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new ModelsListResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Error = responseContent
				};
			}

			var listResponse = JsonSerializer.Deserialize<ModelsListResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return listResponse ?? new ModelsListResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling existing model list API: {Message}", ex.Message);
			return new ModelsListResponse()
			{
				Success = false,
				Status = 500,
				Error = "An unexpected error occurred"
			};
		}
	}


	public async Task<bool> DeleteModelAsync(string modelId)
	{
		try
		{
			var endpoint = _baseUrl + "models/delete";

			var requestBody = new
			{
				model_id = modelId,
			};
			var jsonContent = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

			// Some backends reject bodies on DELETE, so use an explicit request message
			var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
			{
				Content = httpContent
			};

			var response = await _httpClient.SendAsync(request);
			var responseContent = await response.Content.ReadAsStringAsync();

			// Fallback: some servers expose delete as POST for compatibility
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogDebug("DELETE returned {StatusCode}, retrying delete as POST", response.StatusCode);
				response = await _httpClient.PostAsync(endpoint, httpContent);
				responseContent = await response.Content.ReadAsStringAsync();
			}

			// Check if response content indicates model not found (returns -1)
			if (responseContent.Contains("-1"))
			{
				_logger.LogWarning("Model not found. Response: {Response}", responseContent);
				return false;
			}

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to delete model. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling delete model API: {Message}", ex.Message);
			return false;
		}
	}

	// Stop an ongoing model download by process ID
	public async Task<StopModelDownloadResponse> StopModelDownloadAsync(string processId)
	{
		try
		{
			var endpoint = _baseUrl + "models/stop";

			var requestBody = new { process_id = processId };
			var jsonContent = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync(endpoint, httpContent);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to stop model download. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new StopModelDownloadResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Message = "",
					Error = responseContent
				};
			}

			var stopResponse = JsonSerializer.Deserialize<StopModelDownloadResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return stopResponse ?? new StopModelDownloadResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling stop model download API: {Message}", ex.Message);
			return new StopModelDownloadResponse()
			{
				Success = false,
				Status = 500,
				Message = "",
				Error = "An unexpected error occurred"
			};
		}
	}

	// Get progress of a model download by process ID
	public async Task<ModelDownloadProgressResponse> GetModelDownloadProgressAsync(string processId)
	{
		try
		{
			var endpoint = _baseUrl + "models/progress";



			var requestBody = new { process_id = processId };
			var jsonContent = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");



			var response = await _httpClient.PostAsync(endpoint, httpContent);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to retrieve model download status. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new ModelDownloadProgressResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Message = "",
					Error = responseContent
				};
			}

			var statusResponse = JsonSerializer.Deserialize<ModelDownloadProgressResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return statusResponse ?? new ModelDownloadProgressResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling model download status API: {Message}", ex.Message);
			return new ModelDownloadProgressResponse()
			{
				Success = false,
				Status = 500,
				Message = "",
				Error = "An unexpected error occurred"
			};
		}
	}


	// Get model_info of a model download by model ID
	public async Task<ModelInfoResponse> GetModelInfoAsync(string modelId)
	{
		try
		{
			var endpoint = _baseUrl + $"models/model_info?model_id={Uri.EscapeDataString(modelId)}";

			var response = await _httpClient.PostAsync(endpoint, null);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to retrieve model info. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return new ModelInfoResponse
				{
					Success = false,
					Status = (int)response.StatusCode,
					Message = "",
					Error = responseContent
				};
			}

			var statusResponse = JsonSerializer.Deserialize<ModelInfoResponse>(responseContent, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

			return statusResponse ?? new ModelInfoResponse();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling model download status API: {Message}", ex.Message);
			return new ModelInfoResponse()
			{
				Success = false,
				Status = 500,
				Message = "",
				Error = "An unexpected error occurred"
			};
		}
	}
	#endregion

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed && disposing)
		{
			try
			{
				_connectionSemaphore?.Dispose();
				_disposed = true;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error during disposal: {Message}", ex.Message);
			}
		}
	}


	#region Call Tree WS

	public class CallTreeResponse
	{
		public bool Success { get; set; }
		public int Status { get; set; }
		public string? Error { get; set; }
		public object? Data { get; set; }
	}


	public class RequestCallTree
	{
		public List<string> paths { get; set; } = new List<string>();
		public List<string> extensions { get; set; } = new List<string>();

		public string url { get; set; } = string.Empty;
	}

	public async Task<bool> StartTreeStructureAsync(List<string> paths, List<string> extensions, string url, CancellationToken cancellationToken = default)
	{
		ClientWebSocket? streamWebSocket = null;
		try
		{
			Uri endpoint = new Uri($"ws://localhost:{_launchPythonServerService.PythonServerPort}/api/v1/files/tree-structure/start");

			var requestBody = new RequestCallTree
			{
				paths = paths,
				extensions = extensions,
				url = url,
			};

			_logger.LogInformation("Call Tree API Endpoint: {Endpoint}", endpoint);
			_logger.LogDebug("Sending request to Call Tree API with body: {Body}", JsonSerializer.Serialize(requestBody));

			var jsonContent = JsonSerializer.Serialize(requestBody);
			var buffer = System.Text.Encoding.UTF8.GetBytes(jsonContent);

			streamWebSocket = new ClientWebSocket();

			await streamWebSocket.ConnectAsync(endpoint, cancellationToken);
			await streamWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);

			// Receive messages continuously
			var receiveBuffer = new byte[8192];
			while (streamWebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
			{
				try
				{
					var result = await streamWebSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken);

					if (result.MessageType == WebSocketMessageType.Close)
					{
						_logger.LogInformation("WebSocket connection closed by server");
						await streamWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
						break;
					}

					if (result.MessageType == WebSocketMessageType.Text)
					{
						var responseJson = System.Text.Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
						_logger.LogDebug("Received message from Call Tree API: {Response}", responseJson);

						// Try to parse the response
						try
						{
							using var jsonDoc = JsonDocument.Parse(responseJson);
							var root = jsonDoc.RootElement;

							// Check if this is a completion message
							if (root.TryGetProperty("success", out var successProp))
							{
								var isSuccess = successProp.GetBoolean();

								if (root.TryGetProperty("message", out var messageProp))
								{
									var message = messageProp.GetString();
									_logger.LogInformation("Call Tree API message: {Message}", message);

									// If the message indicates completion, break the loop
									if (message?.Contains("complete", StringComparison.OrdinalIgnoreCase) == true)
									{
										break;
									}
								}
							}
						}
						catch (JsonException ex)
						{
							_logger.LogWarning("Failed to parse Call Tree response: {Error}", ex.Message);
						}
					}
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("Call Tree operation was cancelled");

					// Send cancellation message to server before closing
					if (streamWebSocket.State == WebSocketState.Open)
					{
						try
						{
							var cancelMessage = new
							{
								action = "cancel",
								message = "Operation cancelled by client"
							};
							var cancelJson = JsonSerializer.Serialize(cancelMessage);
							var cancelBuffer = System.Text.Encoding.UTF8.GetBytes(cancelJson);

							await streamWebSocket.SendAsync(
								new ArraySegment<byte>(cancelBuffer),
								WebSocketMessageType.Text,
								true,
								CancellationToken.None);

							_logger.LogDebug("Sent cancellation message to server");
						}
						catch (Exception ex)
						{
							_logger.LogWarning("Failed to send cancellation message: {Message}", ex.Message);
						}
					}
					break;
				}
			}

			return true;
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Call Tree operation was cancelled during setup");
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling call tree API: {Message}", ex.Message);
			return false;
		}
		finally
		{
			// Cleanup WebSocket
			if (streamWebSocket != null)
			{
				try
				{
					if (streamWebSocket.State == WebSocketState.Open)
					{
						await streamWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
					}
					streamWebSocket.Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogWarning("Error during WebSocket cleanup: {Message}", ex.Message);
				}
			}
		}
	#endregion
	}

	public async Task<bool> RemoveTreeStructureAsync(List<string> paths, List<string> extensions)
	{
		try
		{
			object requestBody;
			var endpoint = _baseUrl;

			if (paths != null || paths.Count > 0)
			{
				requestBody = new
				{
					paths = paths,
				};
				endpoint += "files/delete_from_tree_structure_with_paths";

			}
			else if (extensions != null || extensions.Count > 0)
			{
				requestBody = new
				{
					extensions = extensions,
				};
				endpoint += "files/delete_from_tree_structure_with_extensions";
			}
			else
			{
				_logger.LogWarning("No paths or extensions provided for removing tree structure");
				return false;
			}

			var jsonContent = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync(endpoint, httpContent);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to remove tree structure. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling remove tree structure API: {Message}", ex.Message);
			return false;
		}
	}



	public async Task<bool> CancelTree()
	{
		try
		{
			var endpoint = _baseUrl + "files/stop_tree_structure_operation";

			var response = await _httpClient.PostAsync(endpoint, null);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to cancel tree structure operation. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling cancel tree structure API: {Message}", ex.Message);
			return false;
		}
	}

	public async Task<bool> baiss_update()
	{
		try
		{
			var endpoint = _baseUrl + "baiss-app/update";

			// Create a dedicated HttpClient with infinite timeout for this long-running operation
			using var httpClient = new HttpClient
			{
				Timeout = Timeout.InfiniteTimeSpan
			};
			var response = await httpClient.PostAsync(endpoint, null);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to trigger Baiss update. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				return false;
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error calling Baiss update API: {Message}", ex.Message);
			return false;
		}
	}


	public async Task<bool> CheckServerStatus()
	{
		try
		{
			var endpoint = _baseUrl + "baiss-app/health";

			var response = await _httpClient.GetAsync(endpoint);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Health check failed. Status Code: {StatusCode}. Restarting Python server...", response.StatusCode);
				return await RestartPythonServer();
			}

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Health check failed: {Message}. Restarting Python server...", ex.Message);
			return false;
			// return await RestartPythonServer();
		}
	}

	private async Task<bool> RestartPythonServer()
	{
		try
		{
			await _launchPythonServerService.StopPythonServerAsync();
			var process = _launchPythonServerService.LaunchPythonServer();

			if (process != null)
			{
				var port = _launchPythonServerService.PythonServerPort;
				_baseUrl = $"http://localhost:{port}/ai/api/v1/";
				_webSocketEndpoint = new Uri($"ws://localhost:{port}/api/v1/chatv2/pre_chat");
				_logger.LogInformation("Python server restarted on port {Port}", port);
				return true;
			}

			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to restart Python server");
			return false;
		}
	}

	public async Task<ModelDetailsResponseDto> GetExternalModelDetailsAsync(string modelId, string? token = null)
	{
		try
		{
			var endpoint = _baseUrl + "models/model_details";
			var requestDto = new ModelDetailsRequestDto
			{
				ModelId = modelId,
				Token = token
			};

			var response = await _httpClient.PostAsJsonAsync(endpoint, requestDto);
			var responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to get external model details. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
				try
				{
					var errorResponse = JsonSerializer.Deserialize<ModelDetailsResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
					if (errorResponse != null) return errorResponse;
				}
				catch { }

				return new ModelDetailsResponseDto
				{
					Success = false,
					Status = (int)response.StatusCode,
					Error = $"HTTP Error {response.StatusCode}: {responseContent}"
				};
			}

			var result = JsonSerializer.Deserialize<ModelDetailsResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			return result ?? new ModelDetailsResponseDto { Success = false, Error = "Failed to deserialize response" };
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting external model details for {ModelId}", modelId);
			return new ModelDetailsResponseDto { Success = false, Error = ex.Message };
		}
	}
}
