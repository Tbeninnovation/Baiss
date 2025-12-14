using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI.Providers;

public class DatabricksConnector
{
    private readonly HttpClient _httpClient;
    private readonly DatabricksConfig _config;
    private readonly ILogger<DatabricksConnector> _logger;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IModelRepository? _modelRepository;

    public DatabricksConnector(
        HttpClient httpClient,
        DatabricksConfig config,
        ILogger<DatabricksConnector> logger,
        ISettingsRepository? settingsRepository = null,
        IModelRepository? modelRepository = null)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _settingsRepository = settingsRepository;
        _modelRepository = modelRepository;
    }

    public async Task<AICompletionResponse> GetCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = CreateCompletionRequest(prompt, options, streaming: false);
            var response = await SendRequestAsync(request, cancellationToken);

            return new AICompletionResponse
            {
                Success = true,
                Content = ExtractContentFromResponse(response),
                Provider = AIProvider.Databricks,
                Usage = ExtractUsageFromResponse(response),
                FinishReason = ExtractFinishReasonFromResponse(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion from Databricks");
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = AIProvider.Databricks
            };
        }
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateCompletionRequest(prompt, options, streaming: true);

        await foreach (var token in StreamRequestAsync(request, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<AICompletionResponse> GetChatResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = CreateChatRequest(messages, systemPrompt, options, streaming: false);
            var response = await SendRequestAsync(request, cancellationToken);

            return new AICompletionResponse
            {
                Success = true,
                Content = ExtractContentFromResponse(response),
                Provider = AIProvider.Databricks,
                Usage = ExtractUsageFromResponse(response),
                FinishReason = ExtractFinishReasonFromResponse(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from Databricks");
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = AIProvider.Databricks
            };
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateChatRequest(messages, systemPrompt, options, streaming: true);

        await foreach (var token in StreamRequestAsync(request, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var servingEndpoint = await GetCurrentServingEndpointAsync();
            var fullUrl = BuildUrl($"/api/2.0/serving-endpoints/{servingEndpoint}");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);

            var response = await _httpClient.SendAsync(httpRequest);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAvailableModelsAsync()
    {
        try
        {
            var fullUrl = BuildUrl("/api/2.0/serving-endpoints");
            var response = await _httpClient.GetAsync(fullUrl);
            var content = await response.Content.ReadAsStringAsync();
            var endpoints = JsonSerializer.Deserialize<DatabricksEndpointsResponse>(content);

            return endpoints?.Endpoints?.Select(e => e.Name) ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models from Databricks");
            return Enumerable.Empty<string>();
        }
    }

    private object CreateCompletionRequest(string prompt, AIRequestOptions? options, bool streaming)
    {
        return new
        {
            inputs = new
            {
                prompt = prompt
            },
            parameters = new
            {
                temperature = options?.Temperature ?? 0.7,
                max_tokens = options?.MaxTokens ?? 1024,
                top_p = options?.TopP ?? 0.9,
                stream = streaming
            }
        };
    }

    private object CreateChatRequest(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt,
        AIRequestOptions? options,
        bool streaming)
    {
        var chatMessages = new List<object>();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            chatMessages.Add(new { role = "system", content = systemPrompt });
        }

        chatMessages.AddRange(messages.Select(m => new { role = m.Role, content = m.Content }));

        // Direct format that works with Databricks serving endpoints
        var request = new Dictionary<string, object>
        {
            ["messages"] = chatMessages
        };

        // Add optional parameters if provided
        if (options?.Temperature.HasValue == true)
            request["temperature"] = options.Temperature.Value;

        if (options?.MaxTokens.HasValue == true)
            request["max_tokens"] = options.MaxTokens.Value;

        if (options?.TopP.HasValue == true)
            request["top_p"] = options.TopP.Value;

        if (streaming)
            request["stream"] = true;

        return request;
    }

    private async Task<JsonDocument> SendRequestAsync(object request, CancellationToken cancellationToken)
    {
        var servingEndpoint = await GetCurrentServingEndpointAsync();
        
        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var fullUrl = BuildUrl($"/serving-endpoints/{servingEndpoint}/invocations");
        
        _logger.LogInformation("Databricks request - URL: {Url}, Endpoint: {Endpoint}", fullUrl, servingEndpoint);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, fullUrl)
        {
            Content = content
        };

        // Add authorization header
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(responseContent);
    }

    private async IAsyncEnumerable<string> StreamRequestAsync(
        object request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var servingEndpoint = await GetCurrentServingEndpointAsync();
        
        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var fullUrl = BuildUrl($"/serving-endpoints/{servingEndpoint}/invocations");
        
        _logger.LogInformation("Databricks stream request - URL: {Url}, Workspace: {Workspace}, Endpoint: {Endpoint}", 
            fullUrl, _config.WorkspaceUrl, servingEndpoint);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, fullUrl)
        {
            Content = content
        };

        // Add authorization header
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Databricks request failed with {StatusCode}: {Error}", response.StatusCode, errorContent);
        }
        
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
        {
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6).Trim();
                if (data == "[DONE]") yield break;

                if (TryParseStreamingResponse(data, out var token))
                {
                    yield return token;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current serving endpoint from the selected model.
    /// Falls back to the config if repositories are not available.
    /// </summary>
    private async Task<string> GetCurrentServingEndpointAsync()
    {
        // Try to get the selected model's name dynamically
        if (_settingsRepository != null && _modelRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAsync();
                if (settings != null && !string.IsNullOrWhiteSpace(settings.AIChatModelId))
                {
                    var model = await _modelRepository.GetModelByIdAsync(settings.AIChatModelId);
                    if (model != null && model.Provider == "databricks")
                    {
                        // The model's name IS the serving endpoint (chat-specific)
                        _logger.LogDebug("Using chat model name as serving endpoint: {Endpoint}", model.Name);
                        return model.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load selected Databricks model, falling back to config");
            }
        }

        // Fallback to the static config
        if (!string.IsNullOrWhiteSpace(_config.ServingEndpoint))
        {
            return _config.ServingEndpoint;
        }

        throw new InvalidOperationException("No Databricks serving endpoint configured. Please select a model in settings.");
    }

    private string BuildUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_config.WorkspaceUrl))
        {
            throw new InvalidOperationException("Databricks workspace URL is not configured (empty). Add credentials in settings.");
        }

        var baseUrl = _config.WorkspaceUrl.Trim();
        // If scheme missing, assume https
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "https://" + baseUrl;
        }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var _))
        {
            throw new InvalidOperationException($"Databricks workspace URL '{_config.WorkspaceUrl}' is invalid.");
        }
        if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;
        return baseUrl.TrimEnd('/') + relativePath;
    }

    private static bool TryParseStreamingResponse(string data, out string token)
    {
        token = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var content))
                {
                    token = content.GetString() ?? string.Empty;
                    return !string.IsNullOrEmpty(token);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractContentFromResponse(JsonDocument response)
    {
        try
        {
            var root = response.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static AIUsageMetrics? ExtractUsageFromResponse(JsonDocument response)
    {
        try
        {
            var root = response.RootElement;
            if (root.TryGetProperty("usage", out var usage))
            {
                return new AIUsageMetrics
                {
                    InputTokens = usage.TryGetProperty("prompt_tokens", out var promptTokens) ? promptTokens.GetInt32() : 0,
                    OutputTokens = usage.TryGetProperty("completion_tokens", out var completionTokens) ? completionTokens.GetInt32() : 0,
                    TotalTokens = usage.TryGetProperty("total_tokens", out var totalTokens) ? totalTokens.GetInt32() : 0
                };
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractFinishReasonFromResponse(JsonDocument response)
    {
        try
        {
            var root = response.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out var finishReason))
                {
                    return finishReason.GetString();
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}

public class DatabricksEndpointsResponse
{
    public DatabricksEndpoint[]? Endpoints { get; set; }
}

public class DatabricksEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}